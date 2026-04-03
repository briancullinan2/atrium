using System.Reflection;

namespace RazorSharp.Services
{
    internal class TitleService(Application? App = null) : TitleService
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
