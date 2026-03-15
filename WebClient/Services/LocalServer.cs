using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using FlashCard.Services;

namespace WebClient.Services
{

    public class LocalServer : ILocalServer
    {
        public string BaseUrl => _navigation?.BaseUri ?? ""; // NavigationManager.Current.BaseUri.Absolute;
        public async Task StopAsync()
        {
            if (_runtime == null)
            {
                return;
            }
            await _runtime.InvokeVoidAsync("window.close", TimeSpan.FromSeconds(1));
        }

        WebAssemblyHost? app;
        private IJSRuntime? _runtime;
        private NavigationManager? _navigation;

        public LocalServer()
        {

        }

        public LocalServer(WebAssemblyHost _app)
        {
            app = _app;
        }

        public void Initialize(WebAssemblyHost _app, Microsoft.JSInterop.IJSRuntime runtime, Microsoft.AspNetCore.Components.NavigationManager navigation)
        {
            app = _app;
            _runtime = runtime;
            _navigation = navigation;
        }
    }
}
