using DataLayer.Utilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace FlashCard.Services
{
    public interface IAuthService
    {
        void RegisterOpenId(AuthenticationBuilder builder, AuthProviderMetadata p);
        void RegisterOauth(AuthenticationBuilder builder, AuthProviderMetadata p);
        void RegisterBuiltIn(AuthenticationBuilder builder, AuthProviderMetadata p);
    }



    public enum AuthType { BuiltIn, GenericOAuth, OpenIdConnect }

    // Statically typed IDs for all 12+ providers
    public enum AuthID
    {
        Google,
        GitHub,
        Microsoft,
        Facebook,
        Apple,
        Discord,
        Twitter,
        LinkedIn,
        Twitch,
        Reddit,
        Okta,
        Auth0,
        Patreon,
        Spotify,
        Trakt,
        BattleNet,
        Strava
    }

    public record AuthProviderMetadata(
        AuthID Id,
        string DisplayName,
        string Icon,
        AuthType Type = AuthType.BuiltIn,
        string? ClientId = null,
        string? Secret = null,
        string? Authority = null); // fuck the authority


    public abstract class AuthService : IAuthService
    {
        public static IQueryManager? QueryManager { get; set; }
        public static IServiceProvider? Service { get; set; }

        static AuthService()
        {
            QueryManager = Service?.GetService(typeof(IQueryManager)) as IQueryManager;
        }

        public AuthService(IServiceProvider _service)
        {
            Service = _service;
            QueryManager = Service?.GetService(typeof(IQueryManager)) as IQueryManager;
        }
        public static readonly List<AuthProviderMetadata> Providers =
        [
            new(AuthID.Google, "Google", "bi-google", AuthType.BuiltIn),
            new(AuthID.GitHub, "GitHub", "bi-github", AuthType.BuiltIn),
            new(AuthID.Microsoft, "Microsoft Account", "bi-windows", AuthType.BuiltIn),
            new(AuthID.Facebook, "Facebook", "bi-facebook", AuthType.BuiltIn),

            // Apple requires a specific DLL/Package for the specialized "Sign in with Apple" flow
            new(AuthID.Apple, "Apple", "bi-apple", AuthType.BuiltIn),
            new(AuthID.Discord, "Discord", "bi-discord", AuthType.GenericOAuth),
            new(AuthID.Twitter, "X (Twitter)", "bi-twitter-x", AuthType.BuiltIn),

            // LinkedIn and Twitch are most easily handled via Generic OAuth endpoints
            new(AuthID.LinkedIn, "LinkedIn", "bi-linkedin", AuthType.GenericOAuth),
            new(AuthID.Twitch, "Twitch", "bi-twitch", AuthType.GenericOAuth),
            new(AuthID.Reddit, "Reddit", "bi-reddit", AuthType.GenericOAuth),
    
            new(AuthID.Okta, "Okta Enterprise", "bi-circle-upside", AuthType.OpenIdConnect),
            new(AuthID.Auth0, "Auth0 Universal", "bi-shield-shaded", AuthType.OpenIdConnect),
    
            new(AuthID.Patreon, "Patreon", "bi-p-circle", AuthType.GenericOAuth),
            new(AuthID.Spotify, "Spotify", "bi-spotify", AuthType.GenericOAuth),

            // Trakt, BattleNet, and Strava are almost always Generic OAuth
            new(AuthID.Trakt, "Trakt", "bi-play-btn", AuthType.GenericOAuth),
            new(AuthID.BattleNet, "BattleNet", "bi-hurricane", AuthType.GenericOAuth),
            new(AuthID.Strava, "Strava", "bi-triangle-half", AuthType.GenericOAuth),
        ];

        public virtual AuthenticationBuilder AddExternalLogins(AuthenticationBuilder builder, IConfiguration config)
        {
            foreach (var p in Providers)
            {
                // We use p.Id.ToString() to find the config section
                var section = config.GetSection($"Authentication:{p.Id}");
                if (!section.Exists()) continue;

                switch (p.Type)
                {
                    case AuthType.BuiltIn:
                        RegisterBuiltIn(builder, p);
                        break;
                    case AuthType.OpenIdConnect:
                        RegisterOpenId(builder, p);
                        break;
                    case AuthType.GenericOAuth:
                        RegisterOauth(builder, p);
                        break;
                }
            }
            return builder;
        }


        public abstract void RegisterOpenId(AuthenticationBuilder builder, AuthProviderMetadata p);


        public abstract void RegisterOauth(AuthenticationBuilder builder, AuthProviderMetadata p);


        public abstract void RegisterBuiltIn(AuthenticationBuilder builder, AuthProviderMetadata p);


        protected virtual (string AuthUrl, string TokenUrl, string UserInfoUrl) GetOAuthEndpoints(AuthID id)
        {
            return id switch
            {
                AuthID.GitHub => (
                    "https://github.com/login/oauth/authorize",
                    "https://github.com/login/oauth/access_token",
                    "https://api.github.com/user"
                ),
                AuthID.Discord => (
                    "https://discord.com/api/oauth2/authorize",
                    "https://discord.com/api/oauth2/token",
                    "https://discord.com/api/users/@me"
                ),
                AuthID.Spotify => (
                    "https://accounts.spotify.com/authorize",
                    "https://accounts.spotify.com/api/token",
                    "https://api.spotify.com/v1/me"
                ),
                AuthID.Trakt => (
                    "https://api.trakt.tv/oauth/authorize",
                    "https://api.trakt.tv/oauth/token",
                    "https://api.trakt.tv/users/me"
                ),
                AuthID.Twitch => (
                    "https://id.twitch.tv/oauth2/authorize",
                    "https://id.twitch.tv/oauth2/token",
                    "https://api.twitch.tv/helix/users"
                ),
                AuthID.BattleNet => (
                    "https://oauth.battle.net/authorize",
                    "https://oauth.battle.net/token",
                    "https://us.battle.net/oauth/userinfo"
                ),
                AuthID.Patreon => (
                    "https://www.patreon.com/oauth2/authorize",
                    "https://www.patreon.com/api/oauth2/token",
                    "https://www.patreon.com/api/oauth2/api/current_user"
                ),
                AuthID.Reddit => (
                    "https://www.reddit.com/api/v1/authorize",
                    "https://www.reddit.com/api/v1/access_token",
                    "https://oauth.reddit.com/api/v1/me"
                ),
                AuthID.Strava => ( // In case you need to start tracking your runs from the cops
                    "https://www.strava.com/oauth/authorize",
                    "https://www.strava.com/oauth/token",
                    "https://www.strava.com/api/v3/athlete"
                ),
                _ => throw new NotImplementedException($"Endpoints for {id} missing.")
            };
        }
    }

}
