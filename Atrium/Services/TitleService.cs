using FlashCard.Services;
using System.Reflection;

namespace Atrium.Services
{
    internal class TitleService(Application? App = null) : FlashCard.Services.TitleService
    {
        internal static string? _title;

        public override async Task<string?> UpdateTitle(string? title)
        {
            _title = await base.UpdateTitle(title);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                foreach (var window in App?.Windows ?? [])
                {
                    window.Title = _title; // This is now safe
                }
            });
            return _title;
        }

    }
}
