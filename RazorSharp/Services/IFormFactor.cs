namespace RazorSharp.Services
{
    // TODO: designed to shut down both services at the same time
    public interface IFormFactor
    {
        string GetFormFactor();
        string GetPlatform();
        string BaseUrl { get; }
        Task StopAsync();

    }
    public class FormFactor(
        IServerState? IServerStateAccessor = null
        , Lazy<Application?>? Desktop = null
        , Lazy<MauiApp?>? Maui = null
#if WINDOWS
        , Lazy<WebApplication?>? App = null
#endif
    ) : IFormFactor
    {
        public bool IsBrowser => OperatingSystem.IsBrowser();
        public bool IsWebContext => IServerStateAccessor?.IServerState != null;
        public bool IsMauiContext => IServerStateAccessor == null || IServerStateAccessor.IServerState == null;

        public string GetFormFactor()
        {
            return Environment.OSVersion.ToString();

            return (IsBrowser ? "WebAssembly" : IsWebContext ? "Http " : "MAUI ") + DeviceInfo.Idiom.ToString();
        }

        public string GetPlatform()
        {
            return DeviceInfo.Platform.ToString() + " - " + DeviceInfo.VersionString;
        }



        public string BaseUrl => Nav.BaseUri ?? ""; // NavigationManager.Current.BaseUri.Absolute;

#if WINDOWS
        public string BaseUrl => App?.Value?.Urls.FirstOrDefault() ?? "http://localhost:8080";
#else
        public string BaseUrl => "http://localhost:8080";
#endif

        public async Task StopAsync()
        {
            if (App != null)
            {
                await App.DisposeAsync();
            }
            if (JS != null)
            {
                await JS.InvokeVoidAsync("window.close", TimeSpan.FromSeconds(1));
            }

#if WINDOWS
            try
            {
                _ = App?.Value?.StopAsync();
            }
            catch { }
#endif
            try
            {
                Desktop?.Value?.Quit();
            }
            catch { }
        }


    }
}
