using Interfacing.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace Atrium.Components;

public partial class MainLoader : ComponentBase, IHasCurrent<MainLoader>
{
    static MainLoader? SetCurrent = null;
    public static MainLoader Current { get => SetCurrent ?? throw new InvalidOperationException("Start the application first"); }
    public MainLoader() : base()
    {
        SetCurrent = this;
    }

    public Type? PermissionType { get; set; } = null;
    public Type? NotFoundControl { get; set; } = null;
    public Type? AuthWrapper { get; set; } = null;
    private Type? StoredDefaultLayout = null;
    public Type? DefaultLayout { 
        get => StoredDefaultLayout;
        set 
        {
            StoredDefaultLayout = value;
            Ready = "layout";
            InvokeAsync(StateHasChanged);
        }
    }
    public System.Reflection.Assembly? AppAssembly { get; set; } = null;

    private bool FirstTimeLoad = true;
    protected string Ready = "loading";

    protected override void OnInitialized()
    {
        base.OnInitialized();
        // Check local storage or your ILocalStore to see if this is the first run
        // or if we should skip the splash.
        var isFirstRun = true; // Use your ILocalStore service here
        FirstTimeLoad = isFirstRun;
        Trust.OnAssemblyLoaded += NotifyAssembly;
    }


    protected void NotifyAssembly(PluginContract file)
    {
        if (Ready == "loading")
        {
            Ready = "loaded";
            // TODO: rescan to get a list of MainLayouts and potential home pages
            InvokeAsync(StateHasChanged);
        }
    }

    public void Dispose()
    {
        Trust.OnAssemblyLoaded -= NotifyAssembly;
        GC.SuppressFinalize(this);
    }

    private RenderFragment RenderWithPermission() => __builder =>
    {
        if (AuthWrapper != null)
        {
            int seq = 0;
            // 1. Open CascadingAuthenticationState
            __builder.OpenComponent(seq++, AuthWrapper);

            // 2. Define the ChildContent for the Auth State
            __builder.AddAttribute(seq++, "ChildContent", (RenderFragment)((__builder2) =>
            {
                if (DefaultLayout != null)
                {
                    // Note: Ensure RenderWithLayout returns a RenderFragment
                    __builder2.AddContent(0, RenderWithLayout());
                }
                else
                {
                    __builder2.AddContent(0, RenderWithoutLayout());
                }
            }));

            __builder.CloseComponent();
        }
        else
        {
            if (DefaultLayout != null)
            {
                // Note: Ensure RenderWithLayout returns a RenderFragment
                __builder.AddContent(0, RenderWithLayout());
            }
            else
            {
                __builder.AddContent(0, RenderWithoutLayout());
            }
        }
    };


    private RenderFragment RebuildRouter() => __builder =>
    {
        __builder.OpenComponent<Router>(0);
        __builder.AddAttribute(1, "AppAssembly", AppAssembly);
        __builder.AddAttribute(2, "AdditionalAssemblies", Trust.LoadedAssemblies.Values);

        __builder.AddAttribute(3, "Found", (RenderFragment<RouteData>)((routeData) => (builder2) =>
        {
            builder2.OpenComponent<RouteView>(0);
            builder2.AddAttribute(1, "RouteData", routeData);
            builder2.AddAttribute(2, "DefaultLayout", DefaultLayout); // Your dynamic layout
            builder2.CloseComponent();
        }));
        // ... handle NotFound ...
        __builder.CloseComponent();
    };



    private RenderFragment CreatePermissionContent(RouteData routeData) => builder =>
    {
        if (PermissionType == null) throw new InvalidOperationException("CreatePermissionContent was called without auth setup");
        builder.OpenComponent(0, PermissionType);

        // Pass the standard parameters
        builder.AddAttribute(1, "RouteData", routeData);
        builder.AddAttribute(2, "NotAuthorizedLayout", DefaultLayout);
        builder.AddAttribute(3, "LoadingLayout", DefaultLayout);

        // Pass the nested content (RouteView and FocusOnNavigate)
        builder.AddAttribute(4, "ChildContent", (RenderFragment)((childBuilder) =>
        {
            childBuilder.OpenComponent<RouteView>(0);
            childBuilder.AddAttribute(1, "RouteData", routeData);
            childBuilder.AddAttribute(2, "DefaultLayout", DefaultLayout);
            childBuilder.CloseComponent();

            childBuilder.OpenComponent<FocusOnNavigate>(3);
            childBuilder.AddAttribute(4, "RouteData", routeData);
            childBuilder.AddAttribute(5, "Selector", "h1");
            childBuilder.CloseComponent();
        }));

        builder.CloseComponent();
    };

    protected IAuthService? FirstAuthService = null;
    protected IAuthService? AuthService
    {
        get => FirstAuthService ??= Service.GetService<IAuthService>();
        set => FirstAuthService = value;
    }

    protected System.Security.Claims.ClaimsPrincipal? State { get; set; } = null;

    /*
     * // TODO: need to reactivate upwards somehow when auth changes, Trust.OnAssemblyLoaded?
    public void Dispose()
    {
        Rendered.OnEmptied -= NotifyReload;
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        if(AuthService != null)
            NotifyReload();
        Rendered.OnEmptied += NotifyReload;
    }
   

    public void NotifyReload()
    {
        FirstAuthService = null;
        _ = AuthService?.GetUserClaimsAsync()
        .Then(async state =>
        {
            State = state;
            await InvokeAsync(StateHasChanged);
        });
    } 
    */

    protected RenderFragment BuildNotFound()
    {
        return __builder =>
        {
            if (NotFoundControl != null)
            {
                __builder.OpenComponent(0, NotFoundControl);
                __builder.CloseComponent();
                return;
            }
            // <div class="flash-card">
            __builder.OpenElement(0, "div");
            __builder.AddAttribute(1, "class", "flash-card");

            // <h3>Not Found</h3>
            __builder.OpenElement(2, "h3");
            __builder.AddContent(3, "Not Found");
            __builder.CloseElement();

            // <p>Sorry, the content you are looking for does not exist.</p>
            __builder.OpenElement(4, "p");
            __builder.AddContent(5, "Sorry, the content you are looking for does not exist.");
            __builder.CloseElement();

            __builder.CloseElement(); // Close div
        };
    }
}
