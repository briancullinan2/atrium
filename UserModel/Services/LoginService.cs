

namespace UserModel.Services
{
    public interface ILoginService
    {
        Task SetLoginMode(bool study);
        Task SetUser(User? user);
        bool Login { get; set; }
        User? User { get; set; }
        event Action<bool>? OnLoginChanged;
        event Action<User?>? OnUserChanged;
        bool IsReady { get; }
    }


    public class LoginService : ILoginService, IDisposable
    {
        public static bool FirstTime { get; set; } = true;
        public bool Login { get; set; } = false;
        public User? User { get; set; } = null;
        public IRenderState Rendered { get; }
        public IAuthService Auth { get; private set; }
        public IQueryManager Query { get; private set; }

        public event Action<bool>? InternalLoginChanged;
        private event Action<User?>? InternalUserChanged;
        public event Action<User?>? OnUserChanged
        {
            add
            {
                InternalUserChanged += value;
                if (User != null)
                    value?.Invoke(User);
            }
            remove
            {
                InternalUserChanged -= value;
            }
        }
        public event Action<bool>? OnLoginChanged
        {
            add
            {
                InternalLoginChanged += value;
                if (User != null)
                    value?.Invoke(Login);
            }
            remove
            {
                InternalLoginChanged -= value;
            }
        }


        public bool IsReady => _restartRequired.Task.IsCompleted && _restartRequired.Task.Result == true;

        private TaskCompletionSource<bool> _restartRequired = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Dispose()
        {
            Rendered.OnEmptied -= NotifyEmptied;
            Auth.AuthenticationStateChanged -= OnAuthStateChanged;
            GC.SuppressFinalize(this);
        }


        private void OnAuthStateChanged(Task<AuthenticationState> task) => _ = SynchronizeUserAsync();


        public Task ModuleInitialize { get => _restartRequired.Task; }


        private async Task SynchronizeUserAsync()
        {
            var state = await Auth.GetAuthenticationStateAsync();
            var principal = state.User;

            if (principal.Identity?.IsAuthenticated == true)
            {
                Console.WriteLine("User is authenticated");
                Console.WriteLine("User is: " + principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier));
                if (principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier) is Claim claim)
                {
                    try
                    {
                        var usernameMatch = await Query
                            .Query<User>(u => u.Username == claim.Value)
                            .FirstOrDefaultAsync();
                        User = usernameMatch;
                    }
                    catch (Exception ex)
                    {
                        Log.Info("Login failed: " + ex.Message);
                    }


                    if (Users.Generate().FirstOrDefault(u => string.Equals(u.Username, claim.Value)) is User defaultUser)
                    {
                        User = defaultUser;
                    }
                }
                // Map your ClaimsPrincipal back to your User object
                //var results = 
            }
            else
            {
                // TODO: check if guest account is allowed and assign to a copy of that user instead?
                User = null; // Guest state
            }

            Console.WriteLine("Logged in: " + User?.Username);

            _restartRequired.TrySetResult(true);

            // Notify any UI components listening to LoginService
            _ = SetUser(User);
            // this is cool because if they login with QR code it will automatically reroute
            // this is still controlled by login page only
            //_ = SetLoginMode(false);


        }

        public LoginService(IAuthService _auth, IQueryManager _query, IRenderState _rendered)
        {
            Rendered = _rendered;
            Auth = _auth;
            Query = _query;
            Auth.AuthenticationStateChanged += OnAuthStateChanged;
            Rendered.OnEmptied += NotifyEmptied;
            // first time is static so really first time
            if (!FirstTime) return;
            FirstTime = false;

            _ = SynchronizeUserAsync();
        }



        private void NotifyEmptied()
        {
            if (_restartRequired.Task.IsCompleted)
                _restartRequired = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = SynchronizeUserAsync();
        }



        // this is a practically trivial setting that differentiates the desktop UI from web users
        public virtual async Task ResetCurrentUser()
        {
            // discard the current user

            var currentSetting = await Query.Query<Setting>(s =>
                s.Name == nameof(DefaultPermissions.ApplicationCurrentUser))
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

            var autoLoginSetting = await Query.Query<Setting>(s =>
                s.Name == nameof(DefaultPermissions.ApplicationAutoLogin))
                .FirstOrDefaultAsync();

            if (autoLoginSetting?.Value != null)
            {
                var automaticUser = await Query.Update<User>(u => new() { Guid = autoLoginSetting.Value });
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
            var defaultUserSetting = await Query.Query<Setting>(s =>
                    s.Name == nameof(DefaultPermissions.ApplicationDefaultUser))
                .FirstOrDefaultAsync();

            if (defaultUserSetting?.Value == null)
            {
                return;
            }

            // TODO: make a formal copy here?

            var defaultUser = await Query.Update<User>(u => new() { Guid = defaultUserSetting.Value });
            await SetUser(defaultUser);
        }


        public async Task SetLoginMode(bool login)
        {
            Login = login;
            InternalLoginChanged?.Invoke(login);
        }

        public async Task SetUser(User? user)
        {
            User = user;
            InternalUserChanged?.Invoke(user);
        }

        // TODO: introduce a 3rd Guest account that's like offline mode, app to local storage data only
        private static AuthenticationState Anonymous() => new(new ClaimsPrincipal(new ClaimsIdentity()));


        public static AuthenticationState Guest()
        {
            // TODO: check here for cached value if database allows Guest accounts
            //   otherwise sneakily change expectations and return anonymous
            var guest = Users.Generate().FirstOrDefault(u => u.Username == nameof(DefaultRoles.Guest));

            var guestClaims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, guest?.Username ?? string.Empty),
                //new(ClaimTypes.Sid, User.Guid),
                new(ClaimTypes.Surname, guest?.FirstName ?? string.Empty),
                new(ClaimTypes.GivenName, guest?.FirstName ?? string.Empty),
                new(ClaimTypes.NameIdentifier, guest?.Username ?? string.Empty),
                new("urn:atrium:guest", "true"),
            };

            foreach (var role in guest?.Roles ?? [])
            {
                if (role.Name == null) continue;
                guestClaims.Add(new Claim(ClaimTypes.Role, role.Name));
                break;
            }

            // IMPORTANT: Providing "Guest" as the second argument makes IsAuthenticated = true
            var identity = new ClaimsIdentity(guestClaims, "GuestAuth");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }




    }

}
