using System.Reflection;
using static System.Net.Mime.MediaTypeNames;

namespace RazorSharp.Services
{
    public interface ITitleService
    {
        Task<string?> UpdateTitle(string? title);
        event Action<string?>? OnTitleChanged;
    }



    public class TitleService(IPageManager PageManager, Application? App = null) : ITitleService
    {
        public static readonly string? AppName = Assembly.GetEntryAssembly()?
                             .GetCustomAttribute<AssemblyProductAttribute>()?
                             .Product;

        public event Action<string?>? OnTitleChanged;
        public virtual async Task<string?> UpdateTitle(string? title)
        {
            if (title == null)
            {
                _title = AppName;
            }
            else
            {
                _title = title + " - " + AppName;
            }
            OnTitleChanged?.Invoke(title);

            if(OperatingSystem.IsBrowser())
            {
                await PageManager.ModuleInitialize;
                await PageManager.Runtime.InvokeVoidAsync("eval", "document.title = " + JsonSerializer.Serialize(_title));
            }
            else if (Context.IsSignalCircuit())
            {
                // don't update app title from web browsers?
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    foreach (var window in App?.Windows ?? [])
                    {
                        window.Title = _title; // This is now safe
                    }
                });
            }
            return title;
        }

        internal static string? _title;


    }

}
