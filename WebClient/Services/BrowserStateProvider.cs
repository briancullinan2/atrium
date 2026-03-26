using DataLayer;
using DataLayer.Entities;
using DataLayer.Utilities;
using FlashCard.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using System.Text.Json;

namespace WebClient.Services
{
    public class BrowserStateProvider : AuthenticationStateProvider
    {

        private static readonly string SessionId = "AtriumSession";

        private static readonly Task<AuthenticationState> _unauthenticatedTask =
        Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));

        private readonly Task<AuthenticationState> _authenticationStateTask = _unauthenticatedTask;
        private readonly IQueryManager _query;

        public BrowserStateProvider(IQueryManager query)
        {
            _query = query;
            var user = new DataLayer.Entities.User();
            // Reconstruct the identity on the Client
            List<Claim> claims = [
                new Claim(ClaimTypes.Name, user.Username ?? ""),
            ];

            _authenticationStateTask = Task.FromResult(
                new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, SessionId))));
        }


        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            // 1. Get SessionID from Local Settings (Your source of truth)
            var currentSetting = await _query.Query<Setting>(s =>
                s.Name == DefaultPermissions.ApplicationCurrentUser.ToString())
                .FirstOrDefaultAsync();

            string? sessionId = currentSetting?.Value;

            if (string.IsNullOrEmpty(sessionId))
                return LoginService.Guest();

            // 2. Fetch from local/remote node
            var session = await _query.Query<Session>(s => s.Id == sessionId).FirstOrDefaultAsync();

            // Arizona: Check expiration locally
            if (session == null || session.Time.AddSeconds(session.Lifetime) < DateTime.UtcNow)
                return LoginService.Guest();

            // 3. Hydrate Claims
            var storedClaims = JsonSerializer.Deserialize<List<UserClaim>>(session.Value) ?? [];

            // ... Keep your GetFreshUserInfo Sync logic here ...

            var identity = new ClaimsIdentity(storedClaims.Select(c => new Claim(c.Type, c.Value)), SessionId);
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }

        public record UserClaim(string Type, string Value);
        public async Task MarkUserAsAuthenticated(ClaimsPrincipal user)
        {
            // 1. Prepare the Session record
            var claimsData = user.Claims.Select(c => new UserClaim(c.Type, c.Value)).ToList();

            // Ensure we have a provider claim for the "Office Mode" lookup
            if (!claimsData.Any(c => c.Type == "urn:atrium:provider"))
            {
                var provider = user.Identity?.AuthenticationType ?? "LocalNode";
                claimsData.Add(new UserClaim("urn:atrium:provider", provider));
            }


            // 4. Notify Blazor UI
            // We create a fresh principal to ensure all local claims are active
            var identity = new ClaimsIdentity(user.Claims, user.Identity?.AuthenticationType);
            var authState = Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));

            NotifyAuthenticationStateChanged(authState);
        }
    }
}
