using Microsoft.AspNetCore.Builder;
using FlashCard.Services;

namespace Atrium.Services
{

    public class LocalServer : ILocalServer
    {
#if WINDOWS
        private WebApplication app;

        public string BaseUrl => app.Urls.FirstOrDefault() ?? "http://localhost:8080";
        public Task StopAsync() => app.StopAsync();

        internal void Initialize(WebApplication _app)
        {
            app = _app;
        }

        public LocalServer()
        {

        }
        public LocalServer(WebApplication _app)
        {
            app = _app;
        }
#else
        public string BaseUrl => "http://localhost:8080";
        public async Task StopAsync() { }
#endif
    }
}
