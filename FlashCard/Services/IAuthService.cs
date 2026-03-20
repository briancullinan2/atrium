using DataLayer;
using DataLayer.Utilities;
using DataLayer.Utilities.Extensions;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Xml.Linq;

namespace FlashCard.Services
{
    public interface IAuthService
    {
        Task MarkUserAsAuthenticated(ClaimsPrincipal user);
        (string AuthUrl, string TokenUrl, string UserInfoUrl) GetOAuthEndpoints(AuthID id);
    }



    public enum AuthType { BuiltIn, GenericOAuth, OpenIdConnect }

    // Statically typed IDs for all 12+ providers
    public enum AuthID
    {
        Unset,
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

    public class AuthProviderMetadata(
        AuthID _id,
        string? _displayName,
        string? _icon,
        AuthType _type = AuthType.BuiltIn
        )
    {
        public AuthID? Id = _id;
        public string? DisplayName = _displayName;
        public string? Icon = _icon;
        public AuthType Type = _type;
        public string? ClientId = null;
        public string? Secret = null;
        public string? Authority = null; // fuck the authority
        [NotMapped]
        public AuthID? DefaultProvider { get => Id ?? DisplayName?.TryParse<AuthID>(); }

    }


    public abstract class AuthService : IAuthService
    {
        public static IQueryManager? QueryManager { get; set; }
        public static IServiceProvider? Service { get; set; }

        static AuthService()
        {
        }

        public AuthService(IServiceProvider? _service)
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

            new(AuthID.Okta, "Okta Enterprise", "bi-circle", AuthType.OpenIdConnect),
            new(AuthID.Auth0, "Auth0 Universal", "bi-shield-shaded", AuthType.OpenIdConnect),

            new(AuthID.Patreon, "Patreon", "bi-p-circle", AuthType.GenericOAuth),
            new(AuthID.Spotify, "Spotify", "bi-spotify", AuthType.GenericOAuth),

            // Trakt, BattleNet, and Strava are almost always Generic OAuth
            new(AuthID.Trakt, "Trakt", "bi-play-btn", AuthType.GenericOAuth),
            new(AuthID.BattleNet, "BattleNet", "bi-hurricane", AuthType.GenericOAuth),
            new(AuthID.Strava, "Strava", "bi-triangle-half", AuthType.GenericOAuth),
        ];



        public abstract Task MarkUserAsAuthenticated(ClaimsPrincipal user);

        public virtual (string AuthUrl, string TokenUrl, string UserInfoUrl) GetOAuthEndpoints(AuthID id)
        {
            return id switch
            {
                AuthID.LinkedIn => (
                    "https://www.linkedin.com/oauth/v2/authorization",
                    "https://www.linkedin.com/oauth/v2/accessToken",
                    "https://api.linkedin.com/v2/userinfo"
                ),
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
                AuthID.Strava => (
                    "https://www.strava.com/oauth/authorize",
                    "https://www.strava.com/oauth/token",
                    "https://www.strava.com/api/v3/athlete"
                ),
                _ => throw new NotImplementedException($"Endpoints for {id} missing.")
            };
        }
    }

}
