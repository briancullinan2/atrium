using FlashCard.Services;
using Microsoft.AspNetCore.Authentication;

namespace WebClient.Services
{
    // this is necessary because when somebody input valid credentials it will go 1 way
    //   into an encrypted field and only the "****" will be returned here for display
    public class AuthService(IServiceProvider _service) : FlashCard.Services.AuthService(_service)
    {
        public override void RegisterBuiltIn(AuthenticationBuilder builder, AuthProviderMetadata p)
        {
            throw new NotImplementedException();
        }

        public override void RegisterOauth(AuthenticationBuilder builder, AuthProviderMetadata p)
        {
            throw new NotImplementedException();
        }

        public override void RegisterOpenId(AuthenticationBuilder builder, AuthProviderMetadata p)
        {
            throw new NotImplementedException();
        }
    }
}
