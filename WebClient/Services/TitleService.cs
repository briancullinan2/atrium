using Microsoft.JSInterop;
using FlashCard.Services;
using System.Reflection;
using System.Text.Json;

namespace WebClient.Services
{
    public class TitleService(IJSRuntime js) : ITitleService
    {
        private readonly IJSRuntime _js = js;
        private static string? _title;
        private readonly string? _appName = Assembly.GetEntryAssembly()?
                     .GetCustomAttribute<AssemblyProductAttribute>()?
                     .Product;
        public event Action<string?>? OnTitleChanged;

        public async Task UpdateTitle(string? title)
        {
            if (title == null)
            {
                _title = _appName;
            }
            else
            {
                _title = title + " - " + _appName;
            }
            // Calling a built-in JS property via eval is the quickest 'dirty' way
            // json strings come with their own quotes
            await _js.InvokeVoidAsync("eval", "document.title = " + JsonSerializer.Serialize(_title));

            OnTitleChanged?.Invoke(title);
        }
    }
}
