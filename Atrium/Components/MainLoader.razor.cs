using Atrium.Extensions;
using Atrium.Services;
using Interfacing.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Atrium.Components;

public partial class MainLoader : ComponentBase, IHasCurrent<MainLoader>, IDisposable
{
    static MainLoader? SetCurrent = null;
    public static MainLoader Current { get => SetCurrent ?? throw new InvalidOperationException("Start the application first"); }


    private bool FirstTimeLoad = true;
    protected string Ready = "loading";

    protected override void OnInitialized()
    {
        base.OnInitialized();

        var isFirstRun = true; // Use your ILocalStore service here
        FirstTimeLoad = isFirstRun;
#if DEBUG
        IsDebug = true;
#endif
        SetCurrent = this;
        Nav.LocationChanged += Nav_LocationChanged;
        Trust.OnAssemblyLoaded += NotifyAssembly;


        var query = Query(Nav.Uri);
        if (Nav?.Uri.Contains("#mode=server") == true)
        {
            PreferredMode = RenderMode.InteractiveServer;
        }
        else if (query?.TryGetValue("mode", out var mode) == true)
        {
            PreferredMode = mode == "server"
                ? RenderMode.InteractiveServer
                : RenderMode.InteractiveWebAssembly;
        }

        Console.WriteLine($"Starting in {PreferredMode} mode");
    }


    public void Dispose()
    {
        Trust.OnAssemblyLoaded -= NotifyAssembly;
        Nav.LocationChanged -= Nav_LocationChanged;
        GC.SuppressFinalize(this);
    }

    private async Task ProbablyUpdateTitle()
    {
        await Task.Delay(400); // more than MainLayout card animations to insert the page
        var components = this.GetChildComponents().OrderBy(comp => comp.GetType() != typeof(PluginsPage) ? -1 : 0);
        var title = components.SelectMany(comp => comp.GetType().GetCustomAttributes<DisplayAttribute>())
            .FirstOrDefault();
        if (title is DisplayAttribute attr)
        {
            var Title = (Service.GetService<IComponentActivator>() as PluginActivator)?.Services.GetService<ITitleService>();
            Title?.UpdateTitle(attr.Name);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            Trust.OnSettledAsync += ProbablyUpdateTitle;
        }
    }

    private void Nav_LocationChanged(object? sender, LocationChangedEventArgs e)
    {
        Trust.OnSettledAsync += ProbablyUpdateTitle;
    }

    // Default to Wasm, but allow an override
    public IComponentRenderMode PreferredMode = RenderMode.InteractiveServer;
    public bool IsDebug = false;
    public Type? PermissionType { get; set; } = null;
    public Type? NotFoundControl { get; set; } = null;
    public Type? AuthWrapper { get; set; } = null;

    static Type? StoredDefaultRoot = null;
    public Type? DefaultRoot {
        get => StoredDefaultRoot;
        set
        {
            StoredDefaultRoot = value;
            if (StoredAppAssembly != null)
            {
                Ready = "layout";
                InvokeAsync(StateHasChanged);
                _ = ProbablyUpdateTitle();
            }
        }
    }

    static Type? StoredDefaultLayout = null;
    public Type? DefaultLayout {
        get => StoredDefaultLayout;
        set
        {
            StoredDefaultLayout = value;
            if (StoredAppAssembly != null)
            {
                Ready = "layout";
                InvokeAsync(StateHasChanged);
                _ = ProbablyUpdateTitle();
            }
        }
    }

    static System.Reflection.Assembly? StoredAppAssembly = null;
    public async Task SetAppAssembly(System.Reflection.Assembly? assembly)
    {
        StoredAppAssembly = assembly;
        if (StoredAppAssembly != null)
        {
            Ready = "layout"; // Explicitly move to layout
            await InvokeAsync(StateHasChanged);
            _ = ProbablyUpdateTitle();
        }
    }

    public static Dictionary<string, string> Query(string uri)
    {
        // Use a simple split/regex to avoid heavy libraries
        var query = uri.TrimStart('?');
        var parameters = query.Split('&')
                              .Select(p => p.Split('='))
                              .ToDictionary(p => p[0], p => p.Length > 1 ? Uri.UnescapeDataString(p[1]) : "");

        return parameters;
    }

    protected void NotifyAssembly(PluginContract file)
    {
        if (Ready == "loading")
        {
            Ready = "loaded";
            InvokeAsync(StateHasChanged);
        }
    }


    private RenderFragment RenderWithPermission() => __builder =>
    {
        if (AuthWrapper != null)
        {
            __builder.OpenComponent(0, AuthWrapper);

            __builder.AddAttribute(1, "ChildContent", (RenderFragment)((__builder2) =>
            {
                if (DefaultLayout != null)
                {
                    // TODO: Ensure RenderWithLayout returns a RenderFragment
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
                // TODO: Ensure RenderWithLayout returns a RenderFragment
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
        __builder.AddAttribute(1, "AppAssembly", StoredAppAssembly);
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
            if (DefaultRoot != null)
            {
                __builder.OpenComponent(0, DefaultRoot);
                __builder.CloseComponent();
                return;
            }
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

