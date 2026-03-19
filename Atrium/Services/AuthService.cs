using DataLayer.Utilities.Extensions;
using FlashCard.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace Atrium.Services
{
    internal class AuthService(IServiceProvider? _service) : FlashCard.Services.AuthService(_service)
    {
        public override void RegisterOpenId(AuthenticationBuilder builder, AuthProviderMetadata p)
        {
            _ = builder.AddOpenIdConnect(p.Id.ToString() ?? p.DisplayName ?? ("OpenId" + builder.Services.Count), o =>
            {
                o.ResponseType = "code";
                o.SaveTokens = true;
                o.Authority = p.Authority;
                o.ClientId = p.ClientId;
                o.ClientSecret = p.Secret;
            });
        }
        
        
        public override void RegisterOauth(AuthenticationBuilder builder, AuthProviderMetadata p)
        {
            if(p.ClientId == null || p.Secret == null)
            {
                throw new InvalidOperationException("Cannot register service without ClientId and Secret.");
            }
            //var section = config.GetSection($"Authentication:{p.Id}");
            _ = builder.AddOAuth(p.Id.ToString() ?? p.DisplayName ?? ("OAuth" + builder.Services.Count), options =>
            {
                options.ClientId = p.ClientId;
                options.ClientSecret = p.Secret;
                options.CallbackPath = new PathString($"/login/callback/{p.Id.ToString()?.ToLower() ?? p.DisplayName?.ToSafe()}");
                options.SaveTokens = true;

                // 1. You must route the endpoints manually
                if(p.Id != null)
                {
                    var (AuthUrl, TokenUrl, UserInfoUrl) = GetOAuthEndpoints(p.Id.Value);
                    options.AuthorizationEndpoint = AuthUrl;
                    options.TokenEndpoint = TokenUrl;
                    options.UserInformationEndpoint = UserInfoUrl;
                }
                else
                {

                }

                options.Events = new OAuthEvents
                {
                    OnCreatingTicket = async context =>
                    {
                        // This is where the actual "Handshake" happens
                        var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);

                        var response = await context.Backchannel.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.HttpContext.RequestAborted);
                        response.EnsureSuccessStatusCode();

                        using var user = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                        context.RunClaimActions(user.RootElement);
                    }
                };

                if (p.Id == AuthID.LinkedIn)
                {
                    // LinkedIn requires specific scopes for the newer Lite Profile / OpenID flow
                    options.Scope.Add("openid");
                    options.Scope.Add("profile");
                    options.Scope.Add("email");
                    options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "sub");
                    options.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
                    options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
                    options.ClaimActions.MapJsonKey("urn:linkedin:avatar", "picture");
                }

                if (p.Id == AuthID.Twitch)
                {
                    options.Scope.Add("user:read:email");
                    // Twitch puts user data inside a "data" array
                    options.ClaimActions.MapCustomJson(ClaimTypes.NameIdentifier, user => user.GetProperty("data")[0].GetProperty("id").GetString());
                    options.ClaimActions.MapCustomJson(ClaimTypes.Name, user => user.GetProperty("data")[0].GetProperty("display_name").GetString());
                    options.ClaimActions.MapCustomJson(ClaimTypes.Email, user => user.GetProperty("data")[0].GetProperty("email").GetString());
                    options.ClaimActions.MapCustomJson("urn:twitch:avatar", user => user.GetProperty("data")[0].GetProperty("profile_image_url").GetString());
                }

                if (p.Id == AuthID.Patreon)
                {
                    options.Scope.Add("identity");
                    options.Scope.Add("identity[email]");
                    // Patreon nests user data under a "data" -> "attributes" hierarchy
                    options.ClaimActions.MapCustomJson(ClaimTypes.NameIdentifier, user => user.GetProperty("data").GetProperty("id").GetString());
                    options.ClaimActions.MapCustomJson(ClaimTypes.Name, user => user.GetProperty("data").GetProperty("attributes").GetProperty("full_name").GetString());
                    options.ClaimActions.MapCustomJson(ClaimTypes.Email, user => user.GetProperty("data").GetProperty("attributes").GetProperty("email").GetString());
                }

                if (p.Id == AuthID.Trakt)
                {
                    // Trakt uses 'username' and 'ids' -> 'slug'
                    options.ClaimActions.MapCustomJson(ClaimTypes.NameIdentifier, user => user.GetProperty("ids").GetProperty("slug").GetString());
                    options.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");
                    options.ClaimActions.MapJsonKey("urn:trakt:vip", "vip");
                }

                if (p.Id == AuthID.BattleNet)
                {
                    // BattleNet is simple but usually provides a 'battletag'
                    options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                    options.ClaimActions.MapJsonKey(ClaimTypes.Name, "battletag");
                }

                if (p.Id == AuthID.Strava)
                {
                    options.Scope.Add("read");
                    // Strava calls the ID 'id' and uses 'firstname'/'lastname'
                    options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                    options.ClaimActions.MapCustomJson(ClaimTypes.Name, user =>
                        $"{user.GetProperty("firstname").GetString()} {user.GetProperty("lastname").GetString()}");
                    options.ClaimActions.MapJsonKey("urn:strava:avatar", "profile_medium");
                }

                // 2. You must manually map the incoming JSON to C# Claims
                if (p.Id == AuthID.GitHub)
                {
                    options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                    options.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
                    options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
                    options.ClaimActions.MapJsonKey("urn:github:avatar", "avatar_url");
                }

                if (p.Id == AuthID.Reddit)
                {
                    // Standardizing Reddit's weird JSON into normal C# Claims
                    options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                    options.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");

                    // Reddit doesn't guarantee an email, so we map their avatar just in case
                    options.ClaimActions.MapJsonKey("urn:reddit:avatar", "icon_img");
                }

                if (p.Id == AuthID.Spotify)
                {
                    options.Scope.Add("user-read-currently-playing");
                    options.Scope.Add("user-read-playback-state");

                    options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                    options.ClaimActions.MapJsonKey(ClaimTypes.Name, "display_name");
                    options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");

                    // Spotify usually returns an array of images, this grabs the first one
                    options.ClaimActions.MapCustomJson("urn:spotify:avatar", user =>
                        user.TryGetProperty("images", out var images) && images.GetArrayLength() > 0
                        ? images[0].GetProperty("url").GetString()
                        : null);
                }

                if (p.Id == AuthID.Discord)
                {
                    options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                    options.ClaimActions.MapJsonKey(ClaimTypes.Name, "global_name"); // New Discord standard
                    options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");

                    // Discord avatars require building a custom URL with their user ID and avatar hash
                    options.ClaimActions.MapCustomJson("urn:discord:avatar", user =>
                        user.TryGetProperty("avatar", out var avatar) && avatar.GetString() != null
                        ? $"https://cdn.discordapp.com/avatars/{user.GetProperty("id").GetString()}/{avatar.GetString()}.png"
                        : null);
                }
            });
        }


        public override void RegisterBuiltIn(AuthenticationBuilder builder, AuthProviderMetadata p)
        {
            if (p.ClientId == null || p.Secret == null)
            {
                throw new InvalidOperationException("Cannot register service without ClientId and Secret.");
            }

            _ = p.Id switch
            {
                AuthID.Google => builder.AddGoogle(o => { o.ClientId = p.ClientId; o.ClientSecret = p.Secret; }),
                AuthID.Facebook => builder.AddFacebook(o => { o.AppId = p.ClientId; o.AppSecret = p.Secret; }),
                AuthID.GitHub => builder.AddGitHub(o => { o.ClientId = p.ClientId; o.ClientSecret = p.Secret; }),
                AuthID.Microsoft => builder.AddMicrosoftAccount(o => { o.ClientId = p.ClientId; o.ClientSecret = p.Secret; }),
                AuthID.Twitter => builder.AddTwitter(o => { o.ConsumerKey = p.ClientId; o.ConsumerSecret = p.Secret; }),
                AuthID.Apple => builder.AddApple(o => { o.ClientId = p.ClientId; o.ClientSecret = p.Secret; }),
                _ => throw new NotSupportedException($"Provider {p.Id} is not yet implemented.")
            };
        }

    }
}
