using FlashCard.Services;
using System.Reflection;

namespace Atrium.Services
{
    internal class TitleService(Application? App = null) : ITitleService
    {
        internal static string? _title;
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
            MainThread.BeginInvokeOnMainThread(() =>
            {
                foreach (var window in App?.Windows ?? [])
                {
                    window.Title = _title; // This is now safe
                }
            });

            OnTitleChanged?.Invoke(title);
        }

    }
}
