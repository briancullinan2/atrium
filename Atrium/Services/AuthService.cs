using DataLayer.Utilities.Extensions;
using FlashCard.Services;
#if WINDOWS || ANDROID
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.AspNetCore.Components.Authorization;
#endif
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace Atrium.Services
{
    internal abstract class AuthService : FlashCard.Services.AuthService
    {


#if WINDOWS || ANDROID
        public static void RegisterOpenId(AuthenticationBuilder builder, AuthProviderMetadata p)
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

        public static void RegisterOauth(AuthenticationBuilder builder, AuthProviderMetadata p)
        {
            if (p.ClientId == null || p.Secret == null)
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
                if (p.Id != null)
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

                if (p.Id == AuthID.Google)
                {
                    // Essential for OIDC and profile data
                    options.Scope.Add("openid");
                    options.Scope.Add("profile");
                    options.Scope.Add("email");
                }

                if (p.Id == AuthID.Facebook)
                {
                    // 'public_profile' is default, but 'email' is separate
                    options.Scope.Add("public_profile");
                    options.Scope.Add("email");
                }

                if (p.Id == AuthID.Microsoft)
                {
                    // Needs these for the Graph API / UserInfo endpoint
                    options.Scope.Add("openid");
                    options.Scope.Add("profile");
                    options.Scope.Add("email");
                    options.Scope.Add("User.Read");
                }

                if (p.Id == AuthID.Twitter)
                {
                    // OAuth 2.0 (X) requires these for basic identity
                    options.Scope.Add("users.read");
                    options.Scope.Add("tweet.read");
                }

                if (p.Id == AuthID.Apple)
                {
                    // Apple only shares name/email on first authorize
                    options.Scope.Add("name");
                    options.Scope.Add("email");
                }

                if (p.Id == AuthID.GitHub)
                {
                    // 'user' gives full access, 'read:user' is the bare minimum for profile
                    // 'user:email' is required to see private email addresses
                    options.Scope.Add("read:user");
                    options.Scope.Add("user:email");
                }

                if (p.Id == AuthID.Discord)
                {
                    // 'identify' gets ID/Avatar/Username, 'email' is separate
                    options.Scope.Add("identify");
                    options.Scope.Add("email");
                }

                if (p.Id == AuthID.Reddit)
                {
                    // 'identity' is the scope for profile info
                    options.Scope.Add("identity");
                }

                if (p.Id == AuthID.Spotify)
                {
                    // 'user-read-private' for name/id, 'user-read-email' for email
                    options.Scope.Add("user-read-private");
                    options.Scope.Add("user-read-email");
                    // You already had these for your player logic:
                    options.Scope.Add("user-read-currently-playing");
                    options.Scope.Add("user-read-playback-state");
                }

                if (p.Id == AuthID.Trakt)
                {
                    // Trakt is usually public, but 'public' scope is standard
                    // No specific scope usually needed for basic /users/settings
                }

                if (p.Id == AuthID.BattleNet)
                {
                    // 'openid' is required for the user info handshake
                    options.Scope.Add("openid");
                }


                if (p.Id != null)
                    ConfigureClaimActions((AuthID)p.Id, options.ClaimActions);
            });
        }


        public static void RegisterBuiltIn(AuthenticationBuilder builder, AuthProviderMetadata p)
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

        public static void AddBonusScopes(AuthID id, OAuthOptions options)
        {
            switch (id)
            {
                case AuthID.Google:
                    // Google bundles most under 'profile', but 'address' is separate
                    options.Scope.Add("openid");
                    options.Scope.Add("profile"); // Name, Surname, Gender, Picture
                    options.Scope.Add("email");
                    options.Scope.Add("https://www.googleapis.com/auth/user.addresses.read"); // Locality/PostalCode
                    options.Scope.Add("https://www.googleapis.com/auth/user.birthday.read");  // DateOfBirth
                    break;

                case AuthID.Facebook:
                    options.Scope.Add("public_profile");
                    options.Scope.Add("email");
                    options.Scope.Add("user_gender");    // ClaimTypes.Gender
                    options.Scope.Add("user_birthday");  // ClaimTypes.DateOfBirth
                    options.Scope.Add("user_location");  // ClaimTypes.Locality/StateOrProvince
                    options.Scope.Add("user_hometown");  // HomeAddress/Locality
                    break;

                case AuthID.Microsoft:
                    options.Scope.Add("openid");
                    options.Scope.Add("profile");
                    options.Scope.Add("email");
                    // Graph API specific scopes for extended claims
                    options.Scope.Add("User.Read");
                    options.Scope.Add("User.Read.All"); // Necessary for some Group/Sid claims
                    break;

                case AuthID.GitHub:
                    // GitHub doesn't have a 'birthday' or 'gender' scope
                    options.Scope.Add("read:user");
                    options.Scope.Add("user:email");
                    // No extra scopes needed; GitHub returns 'location' and 'blog' in the standard user object
                    break;

                case AuthID.Discord:
                    options.Scope.Add("identify");
                    options.Scope.Add("email");
                    // 'connections' allows you to see their linked accounts (YouTube, Twitch, etc.)
                    options.Scope.Add("connections");
                    break;

                case AuthID.LinkedIn:
                    options.Scope.Add("openid");
                    options.Scope.Add("profile"); // ClaimTypes.GivenName, ClaimTypes.Surname
                    options.Scope.Add("email");
                    break;

                case AuthID.Twitter:
                    // Twitter (X) v2 uses specific comma-separated fields rather than "scopes" 
                    // but for the OAuth handshake, these ensure access:
                    options.Scope.Add("users.read");
                    options.Scope.Add("tweet.read");
                    break;

                case AuthID.Twitch:
                    options.Scope.Add("user:read:email");
                    // This is how you'd get the 'Webpage' claim for Twitch streamers
                    options.Scope.Add("user:read:broadcast");
                    break;

                case AuthID.Spotify:
                    options.Scope.Add("user-read-private"); // ClaimTypes.Country
                    options.Scope.Add("user-read-email");
                    options.Scope.Add("user-birthdate");     // ClaimTypes.DateOfBirth
                    break;

                case AuthID.Strava:
                    // Strava is stingy; 'profile:read_all' is needed for full Locality data
                    options.Scope.Add("profile:read_all");
                    break;
            }
        }

        public static AuthenticationBuilder AddExternalLogins(AuthenticationBuilder builder)
        {
            foreach (var p in Providers)
            {
                if (p.ClientId == null || p.Secret == null)
                {
                    continue;
                }
                switch (p.Type)
                {
                    case AuthType.BuiltIn:
                        AuthService.RegisterBuiltIn(builder, p);
                        break;
                    case AuthType.OpenIdConnect:
                        AuthService.RegisterOpenId(builder, p);
                        break;
                    case AuthType.GenericOAuth:
                        RegisterOauth(builder, p);
                        break;
                }
            }
            return builder;
        }



        public static void ConfigureBonusClaims(AuthID id, ClaimActionCollection actions)
        {
            switch (id)
            {
                case AuthID.Google:
                    // Requires 'profile', 'birthday', and 'addresses' scopes
                    actions.MapJsonKey(ClaimTypes.Gender, "gender");
                    actions.MapJsonKey(ClaimTypes.DateOfBirth, "birthday");
                    actions.MapCustomJson(ClaimTypes.Locality, user =>
                        user.TryGetProperty("addresses", out var addr) && addr.GetArrayLength() > 0
                        ? addr[0].GetProperty("city").GetString() : null);
                    actions.MapCustomJson(ClaimTypes.Country, user =>
                        user.TryGetProperty("addresses", out var addr) && addr.GetArrayLength() > 0
                        ? addr[0].GetProperty("country").GetString() : null);
                    break;

                case AuthID.Facebook:
                    // Requires 'user_gender', 'user_birthday', 'user_location'
                    actions.MapJsonKey(ClaimTypes.Gender, "gender");
                    actions.MapJsonKey(ClaimTypes.DateOfBirth, "birthday"); // Format: MM/DD/YYYY
                    actions.MapJsonKey(ClaimTypes.GivenName, "first_name");
                    actions.MapJsonKey(ClaimTypes.Surname, "last_name");
                    actions.MapCustomJson(ClaimTypes.Locality, user =>
                        user.TryGetProperty("location", out var loc) ? loc.GetProperty("name").GetString() : null);
                    break;

                case AuthID.Microsoft:
                    // Standard OIDC / Graph claims
                    actions.MapJsonKey(ClaimTypes.GivenName, "given_name");
                    actions.MapJsonKey(ClaimTypes.Surname, "family_name");
                    actions.MapJsonKey(ClaimTypes.Locality, "city");
                    actions.MapJsonKey(ClaimTypes.StateOrProvince, "state");
                    actions.MapJsonKey(ClaimTypes.Country, "country");
                    actions.MapJsonKey(ClaimTypes.PostalCode, "postalCode");
                    actions.MapJsonKey(ClaimTypes.MobilePhone, "mobilePhone");
                    actions.MapJsonKey(ClaimTypes.Webpage, "businessPhones"); // Often returns a list
                    break;

                case AuthID.GitHub:
                    // GitHub returns these in the core user object if public
                    actions.MapJsonKey(ClaimTypes.Locality, "location");
                    actions.MapJsonKey(ClaimTypes.Webpage, "blog");
                    actions.MapJsonKey(ClaimTypes.UserData, "bio");
                    actions.MapJsonKey("urn:github:company", "company");
                    actions.MapJsonKey("urn:github:followers", "followers");
                    break;

                case AuthID.Discord:
                    // Requires 'identify' and 'email'
                    actions.MapJsonKey("urn:discord:locale", "locale");
                    actions.MapJsonKey("urn:discord:verified", "verified");
                    actions.MapCustomJson("urn:discord:mfa", user =>
                        user.GetProperty("mfa_enabled").GetBoolean().ToString());
                    break;

                case AuthID.Twitter:
                    // Assumes v2 API /users/me
                    actions.MapJsonKey(ClaimTypes.Locality, "location");
                    actions.MapJsonKey(ClaimTypes.UserData, "description");
                    actions.MapCustomJson(ClaimTypes.Webpage, user =>
                        user.TryGetProperty("entities", out var e) && e.TryGetProperty("url", out var u)
                        ? u.GetProperty("urls")[0].GetProperty("expanded_url").GetString() : null);
                    break;

                case AuthID.Spotify:
                    // Requires 'user-read-private' and 'user-birthdate'
                    actions.MapJsonKey(ClaimTypes.Country, "country");
                    actions.MapJsonKey(ClaimTypes.DateOfBirth, "birthdate");
                    actions.MapJsonKey("urn:spotify:product", "product"); // 'premium' or 'free'
                    break;

                case AuthID.Twitch:
                    // Requires 'user:read:email'
                    actions.MapJsonKey(ClaimTypes.UserData, "description");
                    actions.MapJsonKey("urn:twitch:type", "type"); // 'staff', 'admin', 'global_mod', or ''
                    actions.MapJsonKey("urn:twitch:view_count", "view_count");
                    break;

                case AuthID.Patreon:
                    // Requires 'identity' scope
                    actions.MapCustomJson("urn:patreon:is_email_verified", user =>
                        user.GetProperty("data").GetProperty("attributes").GetProperty("is_email_verified").GetBoolean().ToString());
                    actions.MapCustomJson("urn:patreon:thumb", user =>
                        user.GetProperty("data").GetProperty("attributes").GetProperty("image_url").GetString());
                    break;

                case AuthID.Strava:
                    // Requires 'profile:read_all'
                    actions.MapJsonKey(ClaimTypes.Gender, "sex");
                    actions.MapJsonKey(ClaimTypes.Locality, "city");
                    actions.MapJsonKey(ClaimTypes.StateOrProvince, "state");
                    actions.MapJsonKey(ClaimTypes.Country, "country");
                    break;
            }
        }



        public static void ConfigureClaimActions(AuthID id, ClaimActionCollection actions)
        {
            switch (id)
            {
                case AuthID.GitHub:
                    actions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                    actions.MapJsonKey(ClaimTypes.Name, "name"); // or "login"
                    actions.MapJsonKey(ClaimTypes.Email, "email");
                    actions.MapJsonKey("urn:github:avatar", "avatar_url");
                    break;

                case AuthID.Google:
                    actions.MapJsonKey(ClaimTypes.NameIdentifier, "sub");
                    actions.MapJsonKey(ClaimTypes.Name, "name");
                    actions.MapJsonKey(ClaimTypes.Email, "email");
                    actions.MapJsonKey("urn:google:avatar", "picture");
                    break;

                case AuthID.Discord:
                    actions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                    actions.MapJsonKey(ClaimTypes.Name, "global_name");
                    actions.MapJsonKey(ClaimTypes.Email, "email");

                    actions.MapCustomJson("urn:discord:avatar", user =>
                        user.TryGetProperty("avatar", out var av) && av.GetString() != null
                        ? $"https://cdn.discordapp.com/avatars/{user.GetProperty("id").GetString()}/{av.GetString()}.png"
                        : null);
                    break;

                case AuthID.LinkedIn:
                    {
                        actions.MapJsonKey(ClaimTypes.NameIdentifier, "sub");
                        actions.MapJsonKey(ClaimTypes.Name, "name");
                        actions.MapJsonKey(ClaimTypes.Email, "email");
                        actions.MapJsonKey("urn:linkedin:avatar", "picture");
                    }
                    break;

                case AuthID.Twitch:
                    {
                        actions.MapCustomJson(ClaimTypes.NameIdentifier, user => user.GetProperty("data")[0].GetProperty("id").GetString());
                        actions.MapCustomJson(ClaimTypes.Name, user => user.GetProperty("data")[0].GetProperty("display_name").GetString());
                        actions.MapCustomJson(ClaimTypes.Email, user => user.GetProperty("data")[0].GetProperty("email").GetString());
                        actions.MapCustomJson("urn:twitch:avatar", user => user.GetProperty("data")[0].GetProperty("profile_image_url").GetString());
                    }
                    break;

                case AuthID.Patreon:
                    {
                        actions.MapCustomJson(ClaimTypes.NameIdentifier, user => user.GetProperty("data").GetProperty("id").GetString());
                        actions.MapCustomJson(ClaimTypes.Name, user => user.GetProperty("data").GetProperty("attributes").GetProperty("full_name").GetString());
                        actions.MapCustomJson(ClaimTypes.Email, user => user.GetProperty("data").GetProperty("attributes").GetProperty("email").GetString());
                    }
                    break;

                case AuthID.Trakt:
                    {
                        actions.MapCustomJson(ClaimTypes.NameIdentifier, user => user.GetProperty("ids").GetProperty("slug").GetString());
                        actions.MapJsonKey(ClaimTypes.Name, "username");
                        actions.MapJsonKey("urn:trakt:vip", "vip");
                    }
                    break;

                case AuthID.BattleNet:
                    {
                        actions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                        actions.MapJsonKey(ClaimTypes.Name, "battletag");
                    }
                    break;

                case AuthID.Strava:
                    {
                        actions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                        actions.MapCustomJson(ClaimTypes.Name, user =>
                            $"{user.GetProperty("firstname").GetString()} {user.GetProperty("lastname").GetString()}");
                        actions.MapJsonKey("urn:strava:avatar", "profile_medium");
                    }
                    break;

                case AuthID.Reddit:
                    {
                        actions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                        actions.MapJsonKey(ClaimTypes.Name, "name");
                        actions.MapJsonKey("urn:reddit:avatar", "icon_img");
                    }
                    break;

                case AuthID.Spotify:
                    {
                        actions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                        actions.MapJsonKey(ClaimTypes.Name, "display_name");
                        actions.MapJsonKey(ClaimTypes.Email, "email");

                        // Spotify usually returns an array of images, this grabs the first one
                        actions.MapCustomJson("urn:spotify:avatar", user =>
                            user.TryGetProperty("images", out var images) && images.GetArrayLength() > 0
                            ? images[0].GetProperty("url").GetString()
                            : null);
                    }
                    break;

                case AuthID.Facebook:
                    // Facebook uses 'id' for the unique identifier
                    actions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                    actions.MapJsonKey(ClaimTypes.Name, "name");
                    actions.MapJsonKey(ClaimTypes.Email, "email");
                    actions.MapJsonKey(ClaimTypes.GivenName, "first_name");
                    actions.MapJsonKey(ClaimTypes.Surname, "last_name");
                    // Facebook avatars require a nested 'picture' -> 'data' -> 'url' mapping
                    actions.MapCustomJson("urn:facebook:avatar", user =>
                        user.TryGetProperty("picture", out var pic) &&
                        pic.GetProperty("data").TryGetProperty("url", out var url)
                        ? url.GetString()
                        : null);
                    break;

                case AuthID.Microsoft:
                    // Microsoft (Entra/Azure AD) uses 'sub' for OIDC or 'id' for Graph API
                    actions.MapJsonKey(ClaimTypes.NameIdentifier, "sub");
                    actions.MapJsonKey(ClaimTypes.Name, "name");
                    actions.MapJsonKey(ClaimTypes.Email, "email");
                    actions.MapJsonKey(ClaimTypes.GivenName, "given_name");
                    actions.MapJsonKey(ClaimTypes.Surname, "family_name");
                    break;

                case AuthID.Twitter:
                    // Twitter/X uses 'id_str' to avoid JavaScript integer precision issues
                    actions.MapJsonKey(ClaimTypes.NameIdentifier, "id_str");
                    actions.MapJsonKey(ClaimTypes.Name, "name");
                    actions.MapJsonKey(ClaimTypes.Email, "email");
                    actions.MapJsonKey("urn:twitter:screenname", "screen_name");
                    actions.MapJsonKey("urn:twitter:avatar", "profile_image_url_https");
                    break;

                case AuthID.Apple:
                    // Apple is strict: 'sub' is the identifier. 
                    // IMPORTANT: Email/Name are only sent on the VERY FIRST login.
                    actions.MapJsonKey(ClaimTypes.NameIdentifier, "sub");
                    actions.MapJsonKey(ClaimTypes.Email, "email");
                    // Apple names are often sent in a separate 'user' JSON object during the initial post
                    actions.MapCustomJson(ClaimTypes.Name, user =>
                        user.TryGetProperty("name", out var name) &&
                        name.TryGetProperty("firstName", out var f)
                        ? $"{f.GetString()} {name.GetProperty("lastName").GetString()}"
                        : null);
                    break;

            }
        }

#endif
    }
}
