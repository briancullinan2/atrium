using DataLayer;
using DataLayer.Entities;
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


    public class LoginService : ILoginService, IDisposable
    {
        public static bool FirstTime { get; set; } = true;
        public bool Login { get; set; } = false;
        public DataLayer.Entities.User? User { get; set; } = null;
        public AuthenticationStateProvider Auth { get; private set; }
        public IQueryManager Query { get; private set; }

        public event Action<bool>? OnLoginChanged;
        public event Action<DataLayer.Entities.User?>? OnUserChanged;


        public void Dispose()
        {
            Auth.AuthenticationStateChanged += OnAuthStateChanged;
            GC.SuppressFinalize(this);
        }


        private void OnAuthStateChanged(Task<AuthenticationState> task) => _ = SynchronizeUserAsync();



        private async Task SynchronizeUserAsync()
        {
            var state = await Auth.GetAuthenticationStateAsync();
            var principal = state.User;

            if (principal.Identity?.IsAuthenticated == true)
            {
                if(principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier) is Claim claim)
                {
                    var usernameMatch = await Query
                        .Query<User>(u => u.Username != null && u.Username.Equals(claim.Value))
                        .FirstOrDefaultAsync();
                    User = usernameMatch;
                }
                // Map your ClaimsPrincipal back to your User object
                //var results = 
            }
            else
            {
                // TODO: check if guest account is allowed and assign to a copy of that user instead?
                User = null; // Guest state
            }


            // Notify any UI components listening to LoginService
            _ = SetUser(User);
            // this is cool because if they login with QR code it will automatically reroute
            _ = SetLoginMode(false);

        }

        public LoginService(AuthenticationStateProvider _auth, IQueryManager _query)
        {
            Auth = _auth;
            Query = _query;
            Auth.AuthenticationStateChanged += OnAuthStateChanged;

            // first time is static so really first time
            if (!FirstTime) return;
            FirstTime = false;

            _ = SynchronizeUserAsync();
        }



        // this is a practically trivial setting that differentiates the desktop UI from web users
        public virtual async Task ResetCurrentUser()
        {
            // discard the current user

            var currentSetting = await Query.Query<DataLayer.Entities.Setting>(s =>
                s.Name != null && s.Name == DefaultPermissions.ApplicationCurrentUser.ToString())
                .FirstOrDefaultAsync();
            if (currentSetting?.Value != null)
            {
                currentSetting.Value = null;
                await currentSetting.Save();
            }

        }


        // this is a trivial setting to automatically log users back in, for web clients, this is like 
        //   returning to a page after the browser closes and still being logged in with a cookie
        public virtual async Task AuthLoginUser()
        {

            var autoLoginSetting = await Query.Query<DataLayer.Entities.Setting>(s =>
                s.Name != null && s.Name == DefaultPermissions.ApplicationAutoLogin.ToString())
                .FirstOrDefaultAsync();

            if (autoLoginSetting?.Value != null)
            {
                var automaticUser = await Query.Update<DataLayer.Entities.User>(u => new() { Guid = autoLoginSetting.Value });
                await SetUser(automaticUser);
            }
        }



        // this is something a added to previous versions of study sauce, basically instead of using
        //   the anonymous identity, you get a fully authenticated "Guest" copy, from the guest setting
        //   this full account can associate data temporary and then a monthly service can clean up old
        //   accounts that never converted, but UX and data-wise it allows us to collect a lot more 
        //   information about an "anonymous" individual if their assumed to be a logged in user while
        //   visiting. I wish modern video games worked this way, logging in is boring as shit
        public virtual async Task LoginDefaultUser()
        {
            var defaultUserSetting = await Query.Query<DataLayer.Entities.Setting>(s =>
                    s.Name != null && s.Name == DefaultPermissions.ApplicationDefaultUser.ToString())
                .FirstOrDefaultAsync();

            if (defaultUserSetting?.Value == null)
            {
                return;
            }

            // TODO: make a formal copy here?

            var defaultUser = await Query.Update<DataLayer.Entities.User>(u => new() { Guid = defaultUserSetting.Value });
            await SetUser(defaultUser);
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

        // TODO: introduce a 3rd Guest account that's like offline mode, app to local storage data only
        private static AuthenticationState Anonymous() => new(new ClaimsPrincipal(new ClaimsIdentity()));


        public static AuthenticationState Guest()
        {
            // TODO: check here for cached value if database allows Guest accounts
            //   otherwise sneakily change expectations and return anonymous


            var guestClaims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "Guest"),
                //new(ClaimTypes.Sid, User.Guid),
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
