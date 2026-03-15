using FlashCard.Services;
using System.Reflection;

namespace Atrium.Services
{
    internal class TitleService : ITitleService
    {
        internal static Action<string?> _setTitle = s => { };
        internal static string? _title;
        private string? _appName;

        public event Action<string?>? OnTitleChanged;

        public TitleService()
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
            _setTitle(_title);
            OnTitleChanged?.Invoke(title);
        }

    }
}
