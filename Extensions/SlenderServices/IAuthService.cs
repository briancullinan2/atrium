using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace Extensions.SlenderServices
{
    public interface IAuthService
    {
        Task MarkUserAsAuthenticated(ClaimsPrincipal user);
        event AuthenticationStateChangedHandler? AuthenticationStateChanged;
        Task<AuthenticationState> GetAuthenticationStateAsync();
        Task<JsonDocument?> GetFreshUserInfo(AuthID providerId, string accessToken);
        Task<string?> TryResponseToken();
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
}
