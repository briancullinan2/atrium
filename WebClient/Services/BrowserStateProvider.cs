using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace WebClient.Services
{
    public class BrowserStateProvider : AuthenticationStateProvider
    {

        private static readonly string SessionId = "AtriumSession";

        private static readonly Task<AuthenticationState> _unauthenticatedTask =
        Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));

        private readonly Task<AuthenticationState> _authenticationStateTask = _unauthenticatedTask;

        public BrowserStateProvider()
        {
            var user = new DataLayer.Entities.User();
            // Reconstruct the identity on the Client
            List<Claim> claims = [
                new Claim(ClaimTypes.Name, user.Username ?? ""),
            ];

            _authenticationStateTask = Task.FromResult(
                new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, SessionId))));
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            return _authenticationStateTask;
        }
    }
}
