using FlashCard.Services;
using System.Reflection;

namespace Atrium.Services
{
    // doesn't update window title because it's running as a web service, but still needs to generate html
    public class TitleTrackerService : ITitleService
    {
        internal static string? _title;
        private string? _appName;

        public event Action<string?>? OnTitleChanged;

        public TitleTrackerService()
        {
            _appName = Assembly.GetEntryAssembly()?
                             .GetCustomAttribute<AssemblyProductAttribute>()?
                             .Product;
        }
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
            OnTitleChanged?.Invoke(title);
        }

    }
}
