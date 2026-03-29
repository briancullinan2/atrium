using FlashCard.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Atrium.Services
{
    public class FormFactor(
        IHttpContextAccessor? httpContextAccessor = null
#if WINDOWS
        , WebApplication? App = null
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

        public string BaseUrl => App?.Urls.FirstOrDefault() ?? "http://localhost:8080";
        public async Task StopAsync()
        {
            App?.StopAsync();
        }

#else
        public string BaseUrl => "http://localhost:8080";
        public async Task StopAsync() { }
#endif
    }
}
