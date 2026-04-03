using FlashCard.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

namespace WebClient.Services
{
    public class FormFactor(NavigationManager Nav, WebAssemblyHost? App = null, IJSRuntime? JS = null) : IFormFactor
    {
        public string GetFormFactor()
        {
            return "WebAssembly";
        }

        public string GetPlatform()
        {
            return Environment.OSVersion.ToString();
        }

        public string BaseUrl => Nav.BaseUri ?? ""; // NavigationManager.Current.BaseUri.Absolute;
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
        }


    }
}
