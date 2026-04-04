namespace RazorSharp.Services
{
    // TODO: designed to shut down both services at the same time

    public class FormFactor(
        IHttpContextAccessor? httpContextAccessor = null
        , Lazy<Application?>? Desktop = null
        , Lazy<MauiApp?>? Maui = null
#if WINDOWS
        , Lazy<WebApplication?>? App = null
#endif
    ) : IFormFactor
    {
        public bool IsWebContext => httpContextAccessor?.HttpContext != null;
        public bool IsMauiContext => httpContextAccessor == null || httpContextAccessor.HttpContext == null;

        public string GetFormFactor()
        {
            return (IsWebContext ? "Http " : "MAUI ") + DeviceInfo.Idiom.ToString();
        }

        public string GetPlatform()
        {
            return DeviceInfo.Platform.ToString() + " - " + DeviceInfo.VersionString;
        }


#if WINDOWS
        public string BaseUrl => App?.Value?.Urls.FirstOrDefault() ?? "http://localhost:8080";
#else
        public string BaseUrl => "http://localhost:8080";
#endif

        public async Task StopAsync()
        {
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
