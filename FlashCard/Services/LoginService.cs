using DataLayer;
using DataLayer.Utilities;
using DataLayer.Utilities.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace FlashCard.Services
{
    public interface ILoginService
    {
        Task SetLoginMode(bool study);
        Task SetUser(DataLayer.Entities.User? user);
        bool Login { get; set; }
        DataLayer.Entities.User? User { get; set; }
        event Action<bool>? OnLoginChanged;
        event Action<DataLayer.Entities.User?>? OnUserChanged;
    }


    public class LoginService : ILoginService
    {
        public static bool FirstTime { get; set; } = true;
        public bool Login { get; set; } = false;
        public DataLayer.Entities.User? User { get; set; } = null;

        public event Action<bool>? OnLoginChanged;
        public event Action<DataLayer.Entities.User?>? OnUserChanged;

        public LoginService(IServiceProvider Services)
        {
            var queryManager = Services.GetService<IQueryManager>() ?? throw new InvalidOperationException("Couldn't resolve query manager.");

            // first time is static so really first time
            if (!FirstTime)
            {
                return;
            }
            FirstTime = false;

            _ = Task.Run(async () =>
            {
                // discard the current user

                var currentSetting = await queryManager.Query<DataLayer.Entities.Setting>(s =>
                    s.Name != null && s.Name == DefaultPermissions.ApplicationCurrentUser.ToString());
                if (currentSetting.FirstOrDefault()?.Value != null)
                {
                    currentSetting.First().Value = null;
                    await currentSetting.First().Save();
                }


                var autoLoginSetting = await queryManager.Query<DataLayer.Entities.Setting>(s =>
                    s.Name != null && s.Name == DefaultPermissions.ApplicationAutoLogin.ToString());

                if (autoLoginSetting.FirstOrDefault()?.Value != null)
                {
                    var automaticUser = await queryManager.Update<DataLayer.Entities.User>(u => new() { Guid = autoLoginSetting.First().Value });
                    await SetUser(automaticUser);
                }


                // TODO: fallback to default user
                var defaultUserSetting = await queryManager.Query<DataLayer.Entities.Setting>(s =>
                    s.Name != null && s.Name == DefaultPermissions.ApplicationDefaultUser.ToString());


                if (defaultUserSetting.FirstOrDefault()?.Value == null)
                {
                    return;
                }

                var defaultUser = await queryManager.Update<DataLayer.Entities.User>(u => new() { Guid = defaultUserSetting.First().Value });
                await SetUser(defaultUser);
            });
        }

        public async Task SetLoginMode(bool login)
        {
            Login = login;
            OnLoginChanged?.Invoke(login);
        }

        public async Task SetUser(DataLayer.Entities.User? user)
        {
            User = user;
            OnUserChanged?.Invoke(user);
        }

        private static AuthenticationState Anonymous() => new(new ClaimsPrincipal(new ClaimsIdentity()));


        public static AuthenticationState Guest()
        {
            var guestClaims = new List<Claim>
            {
                new(ClaimTypes.Name, "Guest User"),
                new(ClaimTypes.Role, "Guest"),
                new("urn:atrium:guest", "true")
            };

            // IMPORTANT: Providing "Guest" as the second argument makes IsAuthenticated = true
            var identity = new ClaimsIdentity(guestClaims, "Guest");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }

    }

}
