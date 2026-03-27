using FlashCard.Services;
using Microsoft.AspNetCore.Builder;

namespace Atrium.Services
{
    public class FormFactor(
#if WINDOWS
        WebApplication? App = null
#endif
        ) : IFormFactor
    {
        public string GetFormFactor()
        {
            return DeviceInfo.Idiom.ToString();
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
