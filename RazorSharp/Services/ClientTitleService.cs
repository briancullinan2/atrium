using System.Text.Json;

namespace RazorSharp.Services
{
    public class TitleService(IJSRuntime js) : TitleService
    {
        private readonly IJSRuntime _js = js;
        private static string? _title;

        public override async Task<string?> UpdateTitle(string? title)
        {
            _title = await base.UpdateTitle(title);
            // Calling a built-in JS property via eval is the quickest 'dirty' way
            // json strings come with their own quotes
            await _js.InvokeVoidAsync("eval", "document.title = " + JsonSerializer.Serialize(_title));
            return _title;
        }
    }
}
