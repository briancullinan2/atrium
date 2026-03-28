using DataLayer;
using DataLayer.Entities;
using DataLayer.Utilities;
using FlashCard.Services;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using System.Text.Json;

namespace WebClient.Services
{
    public class BrowserStateProvider(IQueryManager Query) : AuthService
    {

        private static readonly string SessionId = "AtriumSession";


        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {

                // 1. Get SessionID from Local Settings (Your source of truth)
                var currentSetting = await Query.Query<Setting>(s =>
                    s.Name == DefaultPermissions.ApplicationCurrentUser.ToString())
                    .FirstOrDefaultAsync();

                string? sessionId = currentSetting?.Value;

                if (string.IsNullOrEmpty(sessionId))
                    return LoginService.Guest();

                // 2. Fetch from local/remote node
                var session = await Query.Query<Session>(s => s.Id == sessionId).FirstOrDefaultAsync();

                // Arizona: Check expiration locally
                if (session == null || session.Time.AddSeconds(session.Lifetime) < DateTime.UtcNow)
                    return LoginService.Guest();

                // 3. Hydrate Claims
                var storedClaims = JsonSerializer.Deserialize<List<UserClaim>>(session.Value) ?? [];

                var identity = new ClaimsIdentity(storedClaims.Select(c => new Claim(c.Type, c.Value)), SessionId);
                return new AuthenticationState(new ClaimsPrincipal(identity));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Auth state check failed: " + ex);
            }
            return LoginService.Guest();
        }

        public record UserClaim(string Type, string Value);

        public override async Task MarkUserAsAuthenticated(ClaimsPrincipal user)
        {
            // 1. Prepare the Session record
            var claimsData = user.Claims.Select(c => new UserClaim(c.Type, c.Value)).ToList();

            // Ensure we have a provider claim for the "Office Mode" lookup
            if (!claimsData.Any(c => c.Type == "urn:atrium:provider"))
            {
                var provider = user.Identity?.AuthenticationType ?? "LocalNode";
                claimsData.Add(new UserClaim("urn:atrium:provider", provider));
            }

            Log.Info("Logging in: " + user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value);


            // 4. Notify Blazor UI
            // We create a fresh principal to ensure all local claims are active
            var identity = new ClaimsIdentity(user.Claims, user.Identity?.AuthenticationType);
            var authState = Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));

            NotifyAuthenticationStateChanged(authState);
        }
    }
}
