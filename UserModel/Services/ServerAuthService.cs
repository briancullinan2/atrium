using DataLayer.Entities;
using DataLayer.Utilities;
using DataLayer.Utilities.Extensions;
using FlashCard.Services;
#if WINDOWS || ANDROID
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
#endif
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Atrium.Services
{
    internal class AuthService(
        NavigationManager Navigation,
        IQueryManager Query,
        IPageManager Page,
        IHttpContextAccessor? _httpContextAccessor = null
    ) : FlashCard.Services.AuthService(Navigation, Query, Page, _httpContextAccessor) {


        public static void BuildAuthentication(IServiceCollection Services)
        {
            // Define a constant for the claim type to avoid naming mismatches
            const string SessionIdClaimType = "atrium_sid";

            var authenticationBuilder = Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/login";
                    options.LogoutPath = "/logout";
                    options.AccessDeniedPath = "/access-denied";

                    // Use a distinct name for the Auth Cookie vs your internal Session ID
                    var product = TitleService.AppName ?? "Atrium";
                    options.Cookie.Name = $"{product}_Auth";

                    options.Events = new CookieAuthenticationEvents
                    {
                        OnValidatePrincipal = async context =>
                        {
                            var query = context.HttpContext.RequestServices.GetRequiredService<IQueryManager>();

                            // Look for the specific claim we injected during MarkUserAsAuthenticated
                            var sessionId = context.Principal?.FindFirst(SessionIdClaimType)?.Value;

                            if (string.IsNullOrEmpty(sessionId))
                            {
                                // No session ID found in the claims? The cookie is invalid for our DB logic.
                                context.RejectPrincipal();
                                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                                return;
                            }

                            // Query the database to see if this session is still "Live"
                            var activeSession = await query.Query<Session>(s => s.Id == sessionId).FirstOrDefaultAsync();

                            // Validation Logic:
                            // 1. Does the session exist?
                            // 2. Has it expired based on the DB 'Time' + 'Lifetime'?
                            if (activeSession == null || (activeSession.Time.AddSeconds(activeSession.Lifetime)) < DateTime.UtcNow)
                            {
                                context.RejectPrincipal();
                                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                            }
                        }
                    };
                });

            AddExternalLogins(authenticationBuilder);

        }



        public static void RegisterOpenId(AuthenticationBuilder builder, AuthProviderMetadata p)
        {
            _ = builder.AddOpenIdConnect(p.Id.ToString() ?? p.DisplayName ?? ("OpenId" + builder.Services.Count), o =>
            {
                if(p.Id != null)
                    o.Scope.AddIdentityScopes(p.Id.Value);
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


                if (p.Id != null)
                {
                    options.Scope.AddIdentityScopes(p.Id.Value);

                    options.ClaimActions.ConfigureClaimActions(p.Id.Value);
                }
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


    }



    public static class AtriumAuthenticationExtensions
    {

        public static void ConfigureClaimActions(
            this ClaimActionCollection target,
            AuthID id
        ) {

            ICollection<Tuple<string, Func<JsonElement, string?>>> source = [];
            source.ConfigureClaimActions(id);
            foreach (var rule in source)
            {
                var claimType = rule.Item1;
                var mappingFunc = rule.Item2;

                // We use MapCustomJson as the "Universal Receiver" because 
                // your Func<JsonElement, string?> matches its signature perfectly.
                target.MapCustomJson(claimType, user => mappingFunc(user));
            }
        }

    }

}
