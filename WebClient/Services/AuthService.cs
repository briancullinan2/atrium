using FlashCard.Services;
using System.Security.Claims;

namespace WebClient.Services
{
    // this is necessary because when somebody input valid credentials it will go 1 way
    //   into an encrypted field and only the "****" will be returned here for display
    public class AuthService(IServiceProvider _service) : FlashCard.Services.AuthService(_service)
    {
        public override Task MarkUserAsAuthenticated(ClaimsPrincipal user)
        {
            throw new NotImplementedException();
        }
    }
}
