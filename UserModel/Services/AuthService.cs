
using System.Net.Http;

namespace UserModel.Services
{
    

    public abstract class AuthService(
        NavigationManager Navigation,
        IQueryManager Query,
        IPageManager Page,
        ICircuitProvider Context,
        IFormFactor Form
    ) : AuthenticationStateProvider, IAuthService {

        public record UserClaim(string Type, string Value);


        public static readonly string CookieName = "AtriumCookie";


        public async Task<Session> GenerateSession(List<Claim> claims)
        {
            var newSession = new Session();
            claims.Add(new Claim(nameof(CookieName), newSession.Id)); // Add the "Bridge"
            var sessionValue = JsonSerializer.Serialize(claims.Select(c => new UserClaim(c.Type, c.Value)), JsonExtensions.Default);
            newSession.Value = sessionValue;
            await Query.Save(newSession);


            return newSession;
        }



        public async Task<Setting> SaveCurrentUser(List<Claim> claims)
        {
            var sessionId = claims.First(c => c.Type == nameof(CookieName)).Value;
            var userGuid = claims.First(c => c.Type == ClaimTypes.Sid).Value;
            var userEntity = await Query.Query<UserData.Entities.User>(u => u.Guid == userGuid).FirstOrDefaultAsync<User>();
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
                        && UserData.Generators.Roles.Generate().Any(u => string.Equals(u.Name, role)))
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
            if (Context.IsSignalCircuit || OperatingSystem.IsBrowser())
            {
                // set and forget, so way we're holding up for this
                _ = Page.SetSessionCookie(CookieName, newSession.Id, newSession.Lifetime / 86400);
            }
            else
            {
                
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
                return Form.QueryParameters
                    ?.Where(q => q.Key == "access_token" || q.Key == "refresh_token")
                    .Select(q => q.Value)
                    .FirstOrDefault();
            }
            return null;
        }




        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            // 1. Get the SessionID from the browser's cookie

            string? sessionId = await TryGetSessionFromCookie() ?? await TryGetSessionFromAuto();
            if (string.IsNullOrEmpty(sessionId))
                return LoginService.Guest();

            // 2. Fetch the session from your Session table
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
            if (Context.IsSignalCircuit || OperatingSystem.IsBrowser())
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






        public async Task<JsonDocument?> GetFreshUserInfo(AuthID providerId, string accessToken)
        {

            var (_, _, userInfoUrl) = AuthenticationExtensions.GetOAuthEndpoints(providerId);

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

}
