#if !BROWSER
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Hosting;
#else
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
#endif

namespace Hosting.Services;

// TODO: designed to shut down both services at the same time

public abstract class BaseFormFactor : IFormFactor, IDisposable, ITitleService
{
    public virtual Dictionary<string, string>? QueryParameters { get; protected set; }
    public IPageManager Page { get; }
    public NavigationManager Navigation { get; }

    public abstract bool IsBrowser { get; }
    public abstract bool IsWebContext { get; }
    public abstract bool IsMauiContext { get; }
    public abstract string GetFormFactor();
    public abstract string GetPlatform();
    public abstract Task StopAsync();
    public abstract string BaseUrl { get; }
    public abstract string ConnectionId { get; }
    public abstract List<IFile> Files { get; }

    public BaseFormFactor(NavigationManager nav, IPageManager page)
    {
        Page = page;
        Navigation = nav;
        Navigation.LocationChanged += Nav_LocationChanged;
        QueryParameters = Navigation.Uri.Query();
    }

    protected virtual void Nav_LocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
    {
        QueryParameters = Navigation.Uri.Query();
    }

    public static string? AppName
    {
        get => Assembly.GetEntryAssembly()?
            .GetCustomAttributes<AssemblyProductAttribute>().FirstOrDefault()
            ?.Product;
    }

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

    public virtual void Dispose()
    {
        Navigation.LocationChanged -= Nav_LocationChanged;
        GC.SuppressFinalize(this);
    }


    public virtual async Task SetSessionCookie(string name, string value, int days)
    {
        await Page.SetSessionCookie(name, value, days);
    }


    public virtual async Task<string?> GetSessionCookie(string name)
    {
        return await Page.GetSessionCookie(name);
    }


}


#if BROWSER

public partial class FormFactor : BaseFormFactor
{
    public override bool IsBrowser => true;
    public override bool IsWebContext => true;
    public override bool IsMauiContext => false;
    public override string GetPlatform() => Environment.OSVersion.ToString();
    public override string BaseUrl => "http://localhost:8080";
    public override string GetFormFactor() => "WebAssembly";
    public override string ConnectionId => "Browser";

    public List<IFile> CurrentFormFiles = [];
    private readonly Lazy<WebAssemblyHost?>? App;
    private readonly IJSRuntime JS;

    public override List<IFile> Files { get => CurrentFormFiles; }

    public FormFactor(
    NavigationManager nav
    , IPageManager page
    , IJSRuntime js 
    , Lazy<WebAssemblyHost?>? app = null
    ) : base(nav, page)
    {
        App = app;
        JS = js;
        Page.Subscribe((PageAction.Upload, "window"), SwapFileListasync);
    }


    protected async Task SwapFileListasync(InputFileChangeEventArgs args)
    {
        CurrentFormFiles = [..CurrentFormFiles, ..args.GetMultipleFiles().Select(f => new BrowserFile(f) as IFile)];
    }


    public override void Dispose()
    {
        Page.Unsubscribe((PageAction.Upload, "window"), SwapFileListasync);
        base.Dispose();
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
    NavigationManager nav
    , IPageManager page
    , HttpContext? Context = null
    , Lazy<Application?>? Desktop = null
    , Lazy<MauiApp?>? Maui = null
    , Lazy<WebApplication?>? App = null
    
) : BaseFormFactor(nav, page)
{
    public override bool IsBrowser => OperatingSystem.IsBrowser();
    public override bool IsWebContext => Context != null;
    public override bool IsMauiContext => (Context == null || App == null) && (Maui != null || Desktop != null);
    public override string GetPlatform() => DeviceInfo.Platform.ToString() + " - " + DeviceInfo.VersionString;
    public override string BaseUrl => App?.Value?.Urls.FirstOrDefault() ?? "http://localhost:8080";
    public override string GetFormFactor() => (IsWebContext ? "Http " : "MAUI ") + DeviceInfo.Idiom.ToString();
    public override string ConnectionId => Context?.Connection.Id ?? "Internal";

    public override List<IFile> Files => [
        ..Context?.Request.Form.Files.Select(f => new FormFile(f) as IFile) ?? [], 
        new BodyBag(Context?.Request) ];


    protected override void Nav_LocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
    {
        QueryParameters = Navigation.Uri.Query().ToList()
            .Concat(Context?.Request.Query.ToList().Select(kvp => new KeyValuePair<string, string>(kvp.Key, kvp.Value.FirstOrDefault() ?? string.Empty)) ?? [])
            .Concat(Context?.Request.Form.ToList().Select(kvp => new KeyValuePair<string, string>(kvp.Key, kvp.Value.FirstOrDefault() ?? string.Empty)) ?? [])
            .ToDictionary();
    }

    public override async Task SetSessionCookie(string name, string value, int days)
    {
        if(Context?.Response.HasStarted != true)
            Context?.Response.Cookies.Append(name, value, new CookieOptions
            {
                HttpOnly = true,
                Secure = true, // Arizona: Always use Secure in production
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(days)
            });
        if (Page == null) return;
        await base.SetSessionCookie(name, value, days);
    }


    public override async Task<string?> GetSessionCookie(string name)
    {
        if(Context?.Request.Cookies.TryGetValue(name, out var cookie) == true) return cookie;
        if (Page == null) return null;
        return await base.GetSessionCookie(name);
    }

    public override async Task<string?> UpdateTitle(string? title)
    {
        if (Desktop?.Value is IHasWindow growable)
            _ = growable.ExpandWindow(true); // don't wait on animations
        var _title = await base.UpdateTitle(title);
        MainThread.BeginInvokeOnMainThread(() =>
        {
            foreach (var window in Desktop?.Value?.Windows ?? [])
            {
                window.Title = _title; // This is now safe
            }
        });
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
public class FormFile(IFormFile File) : IFile
{
    public string FileName => File.FileName;

    public string Name => File.Name;

    public long Size => File.Length;

    public string ContentType => ContentType;

    public Stream OpenReadStream()
       => File.OpenReadStream();
}

public class BodyBag(HttpRequest? Request) : IFile
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

public class BrowserFile(IBrowserFile File) : IFile
{
    public string FileName => File.Name;

    public string Name => File.Name;

    public long Size => File.Size;

    public string ContentType => ContentType;

    public Stream OpenReadStream()
       => File.OpenReadStream();
}

