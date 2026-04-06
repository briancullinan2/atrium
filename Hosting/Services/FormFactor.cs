#if !BROWSER
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Hosting;
#else
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
#endif

namespace Hosting.Services
{
    // TODO: designed to shut down both services at the same time
   
    public abstract class BaseFormFactor : IFormFactor, IDisposable
    {
        private Dictionary<string, string>? SetQueryParameters { get; set; }
        public Dictionary<string, string>? QueryParameters { get => SetQueryParameters; }
        public NavigationManager Navigation { get; }

        public abstract bool IsBrowser { get; }
        public abstract bool IsWebContext { get; }
        public abstract bool IsMauiContext { get; }
        public abstract string GetFormFactor();
        public abstract string GetPlatform();
        public abstract Task StopAsync();
        public abstract string BaseUrl { get; }
        public abstract string ConnectionId { get; }

        public BaseFormFactor(NavigationManager nav)
        {
            Navigation = nav;
            Navigation.LocationChanged += Nav_LocationChanged;
            SetQueryParameters = Navigation.Uri.Query();
        }

        private void Nav_LocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
        {
            SetQueryParameters = Navigation.Uri.Query();
        }

        public void Dispose()
        {
            Navigation.LocationChanged -= Nav_LocationChanged;
            GC.SuppressFinalize(this);
        }
    }


#if BROWSER

    public partial class FormFactor(
        NavigationManager nav
        , IJSRuntime? JS = null
        , Lazy<WebAssemblyHost?>? App = null
    ) : BaseFormFactor(nav)
    {
        public override bool IsBrowser => true;
        public override bool IsWebContext => true;
        public override bool IsMauiContext => false;
        public override string GetPlatform() => Environment.OSVersion.ToString();
        public override string BaseUrl => "http://localhost:8080";
        public override string GetFormFactor() => "WebAssembly";
        public override string ConnectionId => "Browser";

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
        , HttpContext? Context = null
        , Lazy<Application?>? Desktop = null
        , Lazy<MauiApp?>? Maui = null
        , Lazy<WebApplication?>? App = null
    ) : BaseFormFactor(nav)
    {
        public override bool IsBrowser => OperatingSystem.IsBrowser();
        public override bool IsWebContext => Context != null;
        public override bool IsMauiContext => (Context == null || App == null) && (Maui != null || Desktop != null);
        public override string GetPlatform() => DeviceInfo.Platform.ToString() + " - " + DeviceInfo.VersionString;
        public override string BaseUrl => App?.Value?.Urls.FirstOrDefault() ?? "http://localhost:8080";
        public override string GetFormFactor() => (IsWebContext ? "Http " : "MAUI ") + DeviceInfo.Idiom.ToString();
        public override string ConnectionId => Context?.Connection.Id ?? "Internal";

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

}
