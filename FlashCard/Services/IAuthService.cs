using DataLayer;
using DataLayer.Entities;
using DataLayer.Generators;
using DataLayer.Utilities;
using DataLayer.Utilities.Extensions;
using FlashCard.Services.Logging;
using FlashCard.Utilities.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace FlashCard.Services
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
        public AuthID? DefaultProvider => Id ?? DisplayName?.TryParse<AuthID>();

    }


    public abstract class AuthService(
        NavigationManager Navigation,
        IQueryManager Query,
        IPageManager Page,
        IHttpContextAccessor? _httpContextAccessor = null
    ) : AuthenticationStateProvider, IAuthService {



        public record UserClaim(string Type, string Value);


        public static readonly string CookieName = TitleService.AppName ?? "AtriumSession";
        protected HttpContext? Context { get => _httpContextAccessor?.HttpContext; }

        public async Task<Session> GenerateSession(List<Claim> claims)
        {
            var newSession = new Session();
            claims.Add(new Claim(nameof(CookieName), newSession.Id)); // Add the "Bridge"
            var sessionValue = JsonSerializer.Serialize(claims.Select(c => new UserClaim(c.Type, c.Value)), JsonHelper.Default);
            newSession.Value = sessionValue;
            await Query.Save(newSession);


            return newSession;
        }



        public async Task<Setting> SaveCurrentUser(List<Claim> claims)
        {
            var sessionId = claims.First(c => c.Type == nameof(CookieName)).Value;
            var userGuid = claims.First(c => c.Type == ClaimTypes.Sid).Value;
            var userEntity = await Query.Query<User>(u => u.Guid == userGuid).FirstOrDefaultAsync<User>();
            var currentSetting = await Query.Query<Setting>(s =>
                s.Name == nameof(DefaultPermissions.ApplicationCurrentUser))
                .FirstOrDefaultAsync()
                ?? new Setting
                {
                    Name = nameof(DefaultPermissions.ApplicationCurrentUser)
                };
            currentSetting.Value = sessionId;
            currentSetting.Guid = userEntity?.Guid;
            currentSetting.User = userEntity;

            // save the built ins on first login
            try
            {
                var roleClaim = claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
                if (roleClaim != null) {
                    var role = await new Role { Name = roleClaim }.Update(Query);
                    if (role.CanonicalFingerprint == null
                        && DataLayer.Generators.Roles.Generate().Any(u => string.Equals(u.Name, role)))
                    {
                        await Query.Save(role);
                    }
                    currentSetting.Role = role;
                    currentSetting.RoleId = currentSetting.Role.Name;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Role init failed." + ex.Message);
            }

            return await Query.Save(currentSetting);
        }



        public void SaveSessionData(Session newSession)
        {
            if (Context.IsSignalCircuit() || OperatingSystem.IsBrowser())
            {
                // set and forget, so way we're holding up for this
                _ = Page.SetSessionCookie(CookieName, newSession.Id, newSession.Lifetime / 86400);
            }
            else
            {
                Context?.Response.Cookies.Append(CookieName, newSession.Id, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true, // Arizona: Always use Secure in production
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddSeconds(newSession.Lifetime)
                });
            }
        }



        public async Task<string?> TryResponseToken()
        {
            var uri = Navigation.ToAbsoluteUri(Navigation.Uri);
            var query = uri.Query.Query().FirstOrDefault(q => q.Key == "access_token" || q.Key == "refresh_token").Value;

            if (query != null)
                return query;

            if (Context != null)
            {
                // Use reflection or a conditional check to avoid build errors in WASM
                // or just use the string-based 'GetTokenAsync' if on Server.
                return Context.Request.Query.Where(q => q.Key == "access_token" || q.Key == "refresh_token").SelectMany(q => q.Value).FirstOrDefault();
            }
            return null;
        }




        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            // 1. Get the SessionID from the browser's cookie

            string? sessionId = await TryGetSessionFromCookie() ?? await TryGetSessionFromAuto();
            if (string.IsNullOrEmpty(sessionId))
                return LoginService.Guest();

            // 2. Fetch the session from your DataLayer.Entities.Session table
            var session = await Query.Query<Session>(s => s.Id == sessionId).FirstOrDefaultAsync();

            if (session == null || session.Time.AddSeconds(session.Lifetime) <= DateTime.UtcNow)
                return LoginService.Guest();

            var storedClaims = await RestoreSessionClaim(session);

            var claims = storedClaims?.Select(c => new Claim(c.Type, c.Value));
            var providerName = storedClaims?.FirstOrDefault(c => c.Type == "urn:atrium:provider")?.Value ?? GetType().Name;
            var identity = new ClaimsIdentity(claims, providerName);
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }





        public virtual async Task MarkUserAsAuthenticated(ClaimsPrincipal user)
        {
            var claimsData = user.Claims.ToList();

            var provider = claimsData.FirstOrDefault(c => c.Type == "urn:atrium:provider");
            if (provider == null)
            {
                provider = new Claim("urn:atrium:provider", user.Identity?.AuthenticationType ?? GetType().Name); ;
                claimsData.Add(provider);
            }

            // TODO: save web client authentication pretending to be offline server
            //   as if they are authenticating to their own local data
            var token = await TryResponseToken();
            if (token != null)
            {
                claimsData.Add(new Claim("access_token", token));
            }

            Log.Info("Logging in: " + user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value);

            // there is no newSession.User on purpose
            // TODO: set newSession.Lifetime to token lifetime

            var identity = new ClaimsIdentity([..user.Claims], provider.Value);
            var authState = Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));

            NotifyAuthenticationStateChanged(authState);
        }


        public async Task<string?> TryGetSessionFromCookie() {
            if (Context.IsSignalCircuit() || OperatingSystem.IsBrowser())
            {
                return await Page.GetSessionCookie(CookieName);
            }
            else
            {
                return Context?.Request.Cookies[CookieName];
            }
        }


        public async Task<string?> TryGetSessionFromAuto()
        {
            var currentSetting = await Query.Query<Setting>(s =>
                s.Name == nameof(DefaultPermissions.ApplicationCurrentUser))
                .FirstOrDefaultAsync();
            var sessionId = currentSetting?.Value;

            if (sessionId == null)
            {
                var autoLoginSetting = await Query.Query<Setting>(s =>
                    s.Name == nameof(DefaultPermissions.ApplicationAutoLogin))
                    .FirstOrDefaultAsync();
                sessionId = autoLoginSetting?.Value;
            }

            return sessionId;
        }




        public async Task<List<UserClaim>> RestoreSessionClaim(Session sessionEntity)
        {
            var storedClaims = JsonSerializer.Deserialize<List<UserClaim>>(sessionEntity.Value) ?? [];

            // Calculate how long since the last sync
            var needsSync = (DateTime.UtcNow - sessionEntity.Time).TotalMinutes > 1;
            var needsProfile = (DateTime.UtcNow - sessionEntity.ProfileTime).TotalMinutes > 30;


            // update the session with a timestamp once every minute to show they are active users
            if (needsSync)
            {
                if(needsProfile)
                {
                    sessionEntity.Value = JsonSerializer.Serialize(storedClaims);

                }
                sessionEntity.Time = DateTime.UtcNow;
                await Query.Save(sessionEntity);
            }


            return storedClaims;
        }



        public async Task<List<UserClaim>> TriggerAccountUpdate(List<UserClaim> storedClaims)
        {
            var token = storedClaims.FirstOrDefault(c => c.Type == "access_token")?.Value;
            var providerStr = storedClaims.FirstOrDefault(c => c.Type == "urn:atrium:provider")?.Value;


            if (string.IsNullOrEmpty(token) || !Enum.TryParse<AuthID>(providerStr, out var providerId))
            {
                return storedClaims;
            }

            try
            {
                using var json = await GetFreshUserInfo(providerId, token);

                if (json != null)
                {
                    var freshName = json.RootElement.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (freshName != null)
                    {
                        storedClaims.RemoveAll(c => c.Type == ClaimTypes.Name);
                        storedClaims.Add(new UserClaim(ClaimTypes.Name, freshName));
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't kill the session; let them use the cached claims
                // to prevent an auth loop if the external provider is down.
                Console.WriteLine($"Sync failed: {ex.Message}");
            }


            return storedClaims;
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




        public static (string AuthUrl, string TokenUrl, string UserInfoUrl) GetOAuthEndpoints(AuthID id)
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





        public async Task<JsonDocument?> GetFreshUserInfo(AuthID providerId, string accessToken)
        {

            var (_, _, userInfoUrl) = GetOAuthEndpoints(providerId);

            using var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, userInfoUrl);
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            // GitHub and others require a User-Agent
            var userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/144.0.0.0 Safari/537.36";
            request.Headers.UserAgent.ParseAdd(userAgent);
            request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
            request.Headers.Add("Sec-Ch-Ua", "\"Not(A:Brand\";v=\"99\", \"Google Chrome\";v=\"144\", \"Chromium\";v=\"144\"");
            request.Headers.Add("Sec-Ch-Ua-Mobile", "?0");
            request.Headers.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
            request.Headers.Add("Sec-Fetch-Dest", "document");
            request.Headers.Add("Sec-Fetch-Mode", "navigate");
            request.Headers.Add("Sec-Fetch-Site", "none");
            request.Headers.Add("Sec-Fetch-User", "?1");
            request.Headers.Add("Upgrade-Insecure-Requests", "1");

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        }



    }


    public static class AuthenticationExtentions
    {




        public static void ConfigureClaimActions(
            this ICollection<Tuple<string, Func<JsonElement, string?>>> actions,
            AuthID id)
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


        public static void ConfigureBonusClaims(
            this ICollection<Tuple<string, Func<JsonElement, string?>>> actions,
            AuthID id)
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


        public static void MapJsonKey(this ICollection<Tuple<string, Func<JsonElement, string?>>> actions, string claimType, string jsonKey)
        {
            actions.Add(new Tuple<string, Func<JsonElement, string?>>(claimType, user =>
                user.TryGetProperty(jsonKey, out var prop) ? prop.GetString() : null));
        }

        public static void MapJsonKey(this ICollection<Tuple<string, Func<JsonElement, string?>>> actions, string claimType, Func<JsonElement, string?> jsonKey)
        {
            actions.Add(new Tuple<string, Func<JsonElement, string?>>(claimType, jsonKey));
        }

        public static void MapCustomJson(this ICollection<Tuple<string, Func<JsonElement, string?>>> actions, string claimType, Func<JsonElement, string?> jsonKey)
        {
            actions.Add(new Tuple<string, Func<JsonElement, string?>>(claimType, jsonKey));
        }




        public static void AddBonusScopes(this ICollection<string> scopes, AuthID id)
        {
            switch (id)
            {
                case AuthID.Google:
                    // Google bundles most under 'profile', but 'address' is separate
                    scopes.Add("openid");
                    scopes.Add("profile"); // Name, Surname, Gender, Picture
                    scopes.Add("email");
                    scopes.Add("https://www.googleapis.com/auth/user.addresses.read"); // Locality/PostalCode
                    scopes.Add("https://www.googleapis.com/auth/user.birthday.read");  // DateOfBirth
                    break;

                case AuthID.Facebook:
                    scopes.Add("public_profile");
                    scopes.Add("email");
                    scopes.Add("user_gender");    // ClaimTypes.Gender
                    scopes.Add("user_birthday");  // ClaimTypes.DateOfBirth
                    scopes.Add("user_location");  // ClaimTypes.Locality/StateOrProvince
                    scopes.Add("user_hometown");  // HomeAddress/Locality
                    break;

                case AuthID.Microsoft:
                    scopes.Add("openid");
                    scopes.Add("profile");
                    scopes.Add("email");
                    // Graph API specific scopes for extended claims
                    scopes.Add("User.Read");
                    scopes.Add("User.Read.All"); // Necessary for some Group/Sid claims
                    break;

                case AuthID.GitHub:
                    // GitHub doesn't have a 'birthday' or 'gender' scope
                    scopes.Add("read:user");
                    scopes.Add("user:email");
                    // No extra scopes needed; GitHub returns 'location' and 'blog' in the standard user object
                    break;

                case AuthID.Discord:
                    scopes.Add("identify");
                    scopes.Add("email");
                    // 'connections' allows you to see their linked accounts (YouTube, Twitch, etc.)
                    scopes.Add("connections");
                    break;

                case AuthID.LinkedIn:
                    scopes.Add("openid");
                    scopes.Add("profile"); // ClaimTypes.GivenName, ClaimTypes.Surname
                    scopes.Add("email");
                    break;

                case AuthID.Twitter:
                    // Twitter (X) v2 uses specific comma-separated fields rather than "scopes" 
                    // but for the OAuth handshake, these ensure access:
                    scopes.Add("users.read");
                    scopes.Add("tweet.read");
                    break;

                case AuthID.Twitch:
                    scopes.Add("user:read:email");
                    // This is how you'd get the 'Webpage' claim for Twitch streamers
                    scopes.Add("user:read:broadcast");
                    break;

                case AuthID.Spotify:
                    scopes.Add("user-read-private"); // ClaimTypes.Country
                    scopes.Add("user-read-email");
                    scopes.Add("user-birthdate");     // ClaimTypes.DateOfBirth
                    break;

                case AuthID.Strava:
                    // Strava is stingy; 'profile:read_all' is needed for full Locality data
                    scopes.Add("profile:read_all");
                    break;
            }
        }


        public static void AddIdentityScopes(this ICollection<string> scopes, AuthID id)
        {

            if (id == AuthID.Google)
            {
                // Essential for OIDC and profile data
                scopes.Add("openid");
                scopes.Add("profile");
                scopes.Add("email");
            }

            if (id == AuthID.Facebook)
            {
                // 'public_profile' is default, but 'email' is separate
                scopes.Add("public_profile");
                scopes.Add("email");
            }

            if (id == AuthID.Microsoft)
            {
                // Needs these for the Graph API / UserInfo endpoint
                scopes.Add("openid");
                scopes.Add("profile");
                scopes.Add("email");
                scopes.Add("User.Read");
            }

            if (id == AuthID.Twitter)
            {
                // OAuth 2.0 (X) requires these for basic identity
                scopes.Add("users.read");
                scopes.Add("tweet.read");
            }

            if (id == AuthID.Apple)
            {
                // Apple only shares name/email on first authorize
                scopes.Add("name");
                scopes.Add("email");
            }

            if (id == AuthID.GitHub)
            {
                // 'user' gives full access, 'read:user' is the bare minimum for profile
                // 'user:email' is required to see private email addresses
                scopes.Add("read:user");
                scopes.Add("user:email");
            }

            if (id == AuthID.Discord)
            {
                // 'identify' gets ID/Avatar/Username, 'email' is separate
                scopes.Add("identify");
                scopes.Add("email");
            }

            if (id == AuthID.Reddit)
            {
                // 'identity' is the scope for profile info
                scopes.Add("identity");
            }

            if (id == AuthID.Spotify)
            {
                // 'user-read-private' for name/id, 'user-read-email' for email
                scopes.Add("user-read-private");
                scopes.Add("user-read-email");
                // You already had these for your player logic:
                scopes.Add("user-read-currently-playing");
                scopes.Add("user-read-playback-state");
            }

            if (id == AuthID.Trakt)
            {
                // Trakt is usually public, but 'public' scope is standard
                // No specific scope usually needed for basic /users/settings
            }

            if (id == AuthID.BattleNet)
            {
                // 'openid' is required for the user info handshake
                scopes.Add("openid");
            }

        }





    }

}
