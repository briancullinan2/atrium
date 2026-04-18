#if !BROWSER
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Storage;
#endif

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using System.Runtime.InteropServices.JavaScript;

namespace Hosting.Services;

// TODO: designed to shut down both services at the same time

public abstract class BaseFormFactor(
    ICompositeProvider _service,
    NavigationManager? nav = null)
{
    public virtual ICompositeProvider Service { get; } = _service;
    public virtual NavigationManager? Navigation { get; } = nav;
    public virtual Dictionary<string, string>? QueryParameters { get => Navigation?.Uri.Query(); }
    //public IPageManager? Page { get; }

    public abstract bool IsBrowser { get; }
    public abstract bool IsWebContext { get; }
    public abstract bool IsMauiContext { get; }
    public abstract string GetFormFactor();
    public abstract string GetPlatform();
    public abstract Task StopAsync();
    public abstract string BaseUrl { get; }
    public abstract string ConnectionId { get; }
    public abstract List<IFile> Files { get; }

    public abstract Type? RequestControl
    {
        get;
    }

    /*
    public virtual async Task SetState()
    {

    }
    */

    public static string? AppName
    {
        get => Assembly.GetEntryAssembly()?
            .GetCustomAttributes<AssemblyProductAttribute>().FirstOrDefault()
            ?.Product;
    }
    public int OffsetInMinutes { get; private set; }

    internal static string? _title;

    public event Action<string?>? OnTitleChanged;
    public virtual async Task<string?> UpdateTitle(string? title)
    {
        if (title == null)
        {
            _title = AppName;
        }
        else
        {
            _title = title + " - " + AppName;
        }
        OnTitleChanged?.Invoke(title);
        return _title;
    }


    public async ValueTask Clipboard(string text)
    {
        var Rendered = Service.GetService<IRenderState>();
        if (Rendered == null) return;
        await Rendered.EnsureInitialized();
        await (Rendered.Runtime as IJSRuntime)!.InvokeVoidAsync("navigator.clipboard.writeText", text);
    }

    public async Task<int> GetTimezoneOffset()
    {
        var Rendered = Service.GetService<IRenderState>();
        if (Rendered == null) return 0;
        await Rendered.EnsureInitialized();
        OffsetInMinutes = await (Rendered.Runtime as IJSRuntime)!.InvokeAsync<int>("eval", "new Date().getTimezoneOffset()");
        return OffsetInMinutes;
    }


    // TODO: ?
    //#if BROWSER
    //    [JSExport]
    //#else
    //    [JSInvokable]
    //#endif
    public virtual async Task<Dictionary<string, string?>?> RestoreState(object? runtime = null)
    {
        //var Manager = Service.GetService<PageManager>();
        //if (Manager == null) return null;
        //await Manager.EnsureInitialized();
        //var Module = Manager.Module as IJSObjectReference;
        //if (Module == null) return null;
        //var state = await Module.InvokeAsync<Dictionary<string, string?>>("restoreState");
        try
        {
            // TODO: fix this for desktop?
            var provider = Service.GetService<IServiceProvider>();
            var JS = (runtime as IJSRuntime) ?? provider?.GetService<IJSRuntime>();
            if (JS == null) return null;
            var config = await JS.InvokeAsync<Dictionary<string, string?>?>("window.myCustomState");
            return config;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
        return null;
    }


    public virtual async Task SetSessionCookie(string name, string value, int days)
    {
        //await Page.SetSessionCookie(name, value, days);
    }


    public virtual async Task<string?> GetSessionCookie(string name)
    {
        return null;
        //return await Page.GetSessionCookie(name);
    }

    public abstract Task SaveSetting(string key, string value);

    public abstract Task<string> GetSetting(string key, string value);
}


#if BROWSER

public partial class FormFactor : BaseFormFactor
    , IFormFactor, ITitleService, IPageState, ISettings, IDisposable
{
    public override bool IsBrowser => true;
    public override bool IsWebContext => true;
    public override bool IsMauiContext => false;
    public override string GetPlatform() => Environment.OSVersion.ToString();
    public override string BaseUrl => "http://localhost:8080";
    public override string GetFormFactor() => "WebAssembly";
    public override string ConnectionId => "Browser";
    
    public override async Task SaveSetting(string key, string value)
        => await JS.InvokeVoidAsync("localStorage.setItem", key, value);

    public override async Task<string> GetSetting(string key, string value)
        => await JS.InvokeAsync<string>("localStorage.getItem", key) ?? value;

    public List<IFile> CurrentFormFiles = [];
    private readonly Lazy<WebAssemblyHost?>? App;
    private readonly IJSRuntime JS;
    private readonly IPageEvents Page;

    public override Type? RequestControl
    {
        get
        {
            try
            {
                var nav = Service.GetRequiredService<NavigationManager>();
                return TypeExtensions.IdentifyNavigation(Navigation?.Uri).ComponentType;
            }
            catch { }
            return null;
        }
    }

    public override List<IFile> Files { get => CurrentFormFiles; }

    public FormFactor(
    NavigationManager nav
    , ICompositeProvider service
    , IJSRuntime js 
    , IPageEvents page
    , Lazy<WebAssemblyHost?>? app = null
    ) : base(service, nav)
    {
        App = app;
        JS = js;
        Page = page;
        Page.Subscribe((PageAction.Upload, "window"), SwapFileListAsync);
    }
    

    protected async Task SwapFileListAsync(InputFileChangeEventArgs args)
    {
        CurrentFormFiles = [..CurrentFormFiles, ..args.GetMultipleFiles().Select(f => new BrowserFile(f) as IFile)];
    }


    public void Dispose()
    {
        Page.Unsubscribe((PageAction.Upload, "window"), SwapFileListAsync);
        GC.SuppressFinalize(this);
    }


    public override async Task<string?> UpdateTitle(string? title)
    {
        var _title = await base.UpdateTitle(title);
        Page?.SetPageTitle(_title);
        return _title;
    }

    public override async Task StopAsync()
    {
        if (App != null && App.Value != null)
        {
            await App.Value.DisposeAsync();
        }
        if (JS != null)
        {
            await JS.InvokeVoidAsync("window.close", TimeSpan.FromSeconds(1));
        }
    }
}
#else

public partial class FormFactor(
    ICompositeProvider service,
    NavigationManager nav,
    IHttpContextAccessor? Current = null
    , IWindowManager? Windows = null
    , Lazy<Application?>? Desktop = null
    , Lazy<MauiApp?>? Maui = null
    , Lazy<WebApplication?>? App = null

) : BaseFormFactor(service, nav)
    , IFormFactor, ITitleService, IPageState, ISettings
{
    public override bool IsBrowser => OperatingSystem.IsBrowser();
    public override bool IsWebContext => Current?.HttpContext != null;
    public override bool IsMauiContext => (Current?.HttpContext == null || App == null) && (Maui != null || Windows != null);
    public override string GetPlatform() => DeviceInfo.Platform.ToString() + " - " + DeviceInfo.VersionString;
    public override string BaseUrl => App?.Value?.Urls.FirstOrDefault() ?? "http://localhost:8080";
    public override string GetFormFactor() => (IsWebContext ? "Http " : "MAUI ") + DeviceInfo.Idiom.ToString();
    public override string ConnectionId => Current?.HttpContext?.Connection.Id ?? "Internal";

    public override Type? RequestControl
    {
        get
        {
            try
            {
                return TypeExtensions.IdentifyNavigation(Navigation?.Uri).ComponentType;
            }
            catch { }

            try
            {
                var uri = Current?.HttpContext?.Request.Path.Value;
                return TypeExtensions.IdentifyNavigation(uri).ComponentType;
            }
            catch { }
            return null;
        }
    }

    public override async Task SaveSetting(string key, string value)
        => Preferences.Default.Set(key, value);

    public override async Task<string> GetSetting(string key, string value)
        => Preferences.Default.Get(key, value);

    public override Dictionary<string, string>? QueryParameters
    {
        get
        {
            var queryParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // 1. Prioritize NavigationManager (The Interactive Source of Truth)
            try
            {
                if (Navigation != null)
                {
                    var uri = new Uri(Navigation.Uri);
                    var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
                    foreach (var kvp in query)
                    {
                        queryParams[kvp.Key] = kvp.Value.FirstOrDefault() ?? string.Empty;
                    }
                }
            }
            catch { /* Fallback to HttpContext if Nav is uninitialized during Prerender */ }

            // 2. Safe-check the HttpContext (The Static/Initial Source of Truth)
            var context = Current?.HttpContext;
            if (context != null)
            {
                // Only touch Query if our dictionary is still light
                foreach (var kvp in context.Request.Query)
                {
                    queryParams.TryAdd(kvp.Key, kvp.Value.FirstOrDefault() ?? string.Empty);
                }

                // 3. Form is high-risk. Only check if it's a POST with the right content type.
                if (context.Request.HasFormContentType)
                {
                    try
                    {
                        foreach (var kvp in context.Request.Form)
                        {
                            queryParams.TryAdd(kvp.Key, kvp.Value.FirstOrDefault() ?? string.Empty);
                        }
                    }
                    catch (InvalidOperationException) { /* Form may have already been disposed */ }
                }
            }

            return queryParams.Count > 0 ? queryParams : null;
        }
    }

    public override List<IFile> Files => Current?.HttpContext?.Request.Headers
        .ContentType.FirstOrDefault() == "multipart/form-data" ?
        [..Current?.HttpContext?.Request.Form.Files.Select(f => new FormFile(f) as IFile) ?? [],
        new BodyBag(Current?.HttpContext?.Request) ] : [];

    /*
    public override async Task SetState()
    {
        var context = Service.GetRequiredService<IHttpContextAccessor>();
        var http = context.HttpContext;
        var store = http?.RequestServices.GetService<IPersistentComponentStateStore>();
        var manager = Service.GetRequiredService<ComponentStatePersistenceManager>();
        var renderer = Service.GetRequiredService<Renderer>();
        if (store != null)
            _ = manager.PersistStateAsync(store, renderer);
    }
    */


    public override async Task SetSessionCookie(string name, string value, int days)
    {
        if (Current?.HttpContext?.Response.HasStarted != true)
            Current?.HttpContext?.Response.Cookies.Append(name, value, new CookieOptions
            {
                HttpOnly = true,
                Secure = true, // Arizona: Always use Secure in production
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(days)
            });
        //if (Page == null) return;
        //await base.SetSessionCookie(name, value, days);
    }


    public override async Task<string?> GetSessionCookie(string name)
    {
        if (Current?.HttpContext?.Request.Cookies.TryGetValue(name, out var cookie) == true) return cookie;
        //if (Page == null) 
        return null;
        //return await base.GetSessionCookie(name);
    }

    public override async Task<string?> UpdateTitle(string? title)
    {
        var _title = await base.UpdateTitle(title); // sets title bar on html page

        var Page = Service.GetService<IPageEvents>();
        if (Page != null)
            await Page.SetPageTitle(_title);

        if (IsWebContext) return _title; // dont update app container from web context... yet.

        if (Windows != null)
            _ = Windows.ExpandWindow(true); // don't wait on animations

        if (Windows != null && Windows.IsSplashMode != true)
            await Windows.UpdateTitle(_title);

        return _title;
    }

    public override async Task StopAsync()
    {

        try
        {
            _ = App?.Value?.StopAsync();
        }
        catch { }
        try
        {
            Desktop?.Value?.Quit();
        }
        catch { }
    }
}

#endif

#if !BROWSER
public class FormFile(IFormFile File) : IFile, IHasNoService
{
    public string FileName => File.FileName;

    public string Name => File.Name;

    public long Size => File.Length;

    public string ContentType => ContentType;

    public Stream OpenReadStream()
       => File.OpenReadStream();
}

public class BodyBag(HttpRequest? Request) : IFile, IHasNoService
{
    public string FileName => Request?.Path ?? "";

    public string Name => Request != null ? "Form" : "";

    public long Size => Request?.ContentLength ?? -1;

    public string ContentType => ContentType;

    public Stream OpenReadStream()
       => Request?.Body!;
}
#else

#endif

public class BrowserFile(IBrowserFile File) : IFile, IHasNoService
{
    public string FileName => File.Name;

    public string Name => File.Name;

    public long Size => File.Size;

    public string ContentType => ContentType;

    public Stream OpenReadStream()
       => File.OpenReadStream();
}

