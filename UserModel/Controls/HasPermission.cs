using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Rendering;
using RazorSharp.Controls;

namespace UserModel.Controls;


public class HasPermission : ComponentBase, IDisposable
{
    ILoginService? StoredLoginService { get; set; }
    [Inject] IServiceProvider? Service { get; set; }
    public ILoginService? LoginService
    {
        get
        {
            return StoredLoginService ??= Service?.GetService<ILoginService>();
                //?? throw new InvalidOperationException("LoginService is not available. Ensure that you have registered ILoginService in your DI container and that this component is used within a context where it can be injected."); ;
        }
        set => StoredLoginService = value;
    }

    private bool IsAllowAnonymous =>
        RouteData?.PageType.GetCustomAttributes<AllowAnonymousAttribute>().FirstOrDefault() != null;

    private IEnumerable<AuthorizeAttribute> AuthorizeAttributes =>
        RouteData?.PageType.GetCustomAttributes<AuthorizeAttribute>(inherit: true) ?? [];

    private bool IsAuthorized
    {
        get
        {
            // 1. AllowAnonymous always wins
            if (IsAllowAnonymous) return true;

            if (AuthorizeAttributes.Any() && LoginService?.User == null) return false;


            // 3. Admin Bypass (The "God Mode" check)
            if (LoginService?.Roles?.Any(r => r == nameof(DefaultRoles.Admin)) == true) return true;

            foreach (var attr in AuthorizeAttributes)
            {
                // 2. Check Roles (Support for '!' prefix)
                if (!string.IsNullOrEmpty(attr.Roles))
                {
                    var roles = attr.Roles.Split(',').Select(x => x.Trim());
                    foreach (var role in roles)
                    {
                        bool isNegated = role.StartsWith('!');
                        string actualRole = isNegated ? role[1..] : role;
                        bool hasRole = LoginService?.Roles?.Any(r => string.Equals(r, actualRole, StringComparison.InvariantCultureIgnoreCase)) == true;

                        // If it starts with ! and user HAS it, or no ! and user LACKS it... fail.
                        if ((isNegated && hasRole) || (!isNegated && !hasRole))
                            return false;
                    }
                }

                // 3. Map "Policy" to your "Permissions" (User.Settings)
                if (!string.IsNullOrEmpty(attr.Policy))
                {
                    // Check if the user has a setting that matches the Policy name
                    if (LoginService?.Permissions?.Any(s => string.Equals(s.Key, attr.Policy, StringComparison.InvariantCultureIgnoreCase)) != true)
                        return false;
                }
            }


            // Final check against User Settings/Permissions
            return RequiredPermission;
        }
    }

    // The "Main" content or tagged <Authorized> content
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public RenderFragment? Authorized { get; set; }

    // The templates for other states
    [Parameter] public RenderFragment? NotAuthorized { get; set; }
    [Parameter] public RenderFragment? Authorizing { get; set; }

    // Logic for loading state (e.g., if LoginService is still initializing)
    private bool IsLoading => LoginService?.User == null && LoginService?.IsReady != true;

    private bool RequiredPermission =>
        Permission == null ||
        Permission.TryParse<DefaultPermissions>() == DefaultPermissions.Unset ||
        LoginService?.Roles?.Any(r => r == nameof(DefaultRoles.Admin)) == true ||
        LoginService?.Permissions?.Any(s => string.Equals(s.Key, nameof(DefaultPermissions.Unrestricted), StringComparison.InvariantCultureIgnoreCase) ||
            string.Equals(s.Key, Permission, StringComparison.InvariantCultureIgnoreCase)) == true;


    [Parameter] public string? Permission { get; set; }
    [Parameter] public RouteData? RouteData { get; set; } = default!;
    [Parameter] public Type? DefaultLayout { get; set; }
    [Parameter] public Type? Layout { get; set; }
    [Parameter] public Type? NotAuthorizedLayout { get; set; }
    [Parameter] public Type? LoadingLayout { get; set; }
    [Parameter] public Type? AuthorizedLayout { get; set; }
    [Parameter] public AuthenticationState? Context { get; set; }


    private static RenderFragment DefaultAuthorizing => __builder =>
    {
        __builder.OpenElement(0, "div");
        __builder.AddAttribute(1, "class", "flash-card hip-error-trace animate-in");

        __builder.OpenElement(2, "h3");
        __builder.AddAttribute(3, "class", "loading-shimmer");
        __builder.AddContent(4, "Verifying Identity...");
        __builder.CloseElement(); // Close h3

        __builder.OpenElement(5, "p");
        __builder.AddContent(6, "If you are not redirected automatically, ");

        __builder.OpenElement(7, "a");
        __builder.AddAttribute(8, "href", TypeExtensions.GetUri<ILogin>());
        __builder.AddContent(9, "Click here");
        __builder.CloseElement(); // Close a

        __builder.AddContent(10, ".");
        __builder.CloseElement(); // Close p

        __builder.CloseElement(); // Close div
    };

    private static RenderFragment DefaultNotAuthorized => __builder =>
    {
        __builder.OpenComponent<RedirectToLogin>(0);
        __builder.CloseComponent();
    };

    private RenderFragment RenderPageWithUrlParameters => __builder =>
    {
        // Check ChildContent or Authorized first
        if (ChildContent != null || Authorized != null)
        {
            if (ChildContent != null)
            {
                __builder.AddContent(0, ChildContent);
            }

            if (Authorized != null)
            {
                // We use a different sequence to avoid collision if both exist
                __builder.AddContent(1, Authorized);
            }
            return;
        }

        // Fallback to RouteData
        if (RouteData == null)
        {
            __builder.OpenComponent<NotFoundControl>(2);
            __builder.CloseComponent();
            return;
        }

        // Dynamic Component Rendering
        __builder.OpenComponent(3, RouteData.PageType);

        // Map RouteValues to Attributes
        int sequence = 4;
        foreach (var kvp in RouteData.RouteValues)
        {
            __builder.AddAttribute(sequence++, kvp.Key, kvp.Value);
        }

        __builder.CloseComponent();
    };



    protected override void BuildRenderTree(RenderTreeBuilder __builder)
    {
        if (IsAuthorized)
        {
            HasPermission.RenderWithLayout(__builder, AuthorizedLayout ?? Layout ?? DefaultLayout, RenderPageWithUrlParameters);
        }
        else if (IsLoading)
        {
            HasPermission.RenderWithLayout(__builder, LoadingLayout ?? Layout ?? DefaultLayout, Authorizing ?? HasPermission.DefaultAuthorizing);
        }
        else
        {
            HasPermission.RenderWithLayout(__builder, NotAuthorizedLayout ?? Layout ?? DefaultLayout, NotAuthorized ?? DefaultNotAuthorized);
        }
    }

    private static void RenderWithLayout(RenderTreeBuilder __builder, Type? layoutType, RenderFragment? content)
    {
        if (layoutType != null)
        {
            __builder.OpenComponent<LayoutView>(0);
            __builder.AddAttribute(1, nameof(LayoutView.Layout), layoutType);
            __builder.AddAttribute(2, nameof(LayoutView.ChildContent), content);
            __builder.CloseComponent();
        }
        else
        {
            __builder.AddContent(3, content);
        }
    }



    protected override void OnInitialized()
    {
        base.OnInitialized();
        LoginService?.OnUserChanged += NotifyUserChanged;
    }

    public void NotifyUserChanged(object? user)
    {
        InvokeAsync(StateHasChanged);
    }


    public void Dispose()
    {
        LoginService?.OnUserChanged -= NotifyUserChanged;
        GC.SuppressFinalize(this);
    }

}

