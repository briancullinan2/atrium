using FlashCard.Services;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace WebClient.Services
{
    // this is necessary because when somebody input valid credentials it will go 1 way
    //   into an encrypted field and only the "****" will be returned here for display
    public class AuthService(IServiceProvider _service) : FlashCard.Services.AuthService(_service)
    {
        public override async Task MarkUserAsAuthenticated(ClaimsPrincipal user)
        {
            var task = (Service?.GetService<AuthenticationStateProvider>() as BrowserStateProvider)?.MarkUserAsAuthenticated(user);
            if (task != null) await task;
        }
    }
}
