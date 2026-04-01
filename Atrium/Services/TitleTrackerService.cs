using FlashCard.Services;
using System.Reflection;

namespace Atrium.Services
{
    // doesn't update window title because it's running as a web service, but still needs to generate html
    public class TitleTrackerService : FlashCard.Services.TitleService
    {
        internal static string? _title;

        public override async Task<string?> UpdateTitle(string? title)
        {
            if (title == null)
            {
                _title = AppName;
            }
            else
            {
                _title = title + " - " + AppName;
            }
            return await base.UpdateTitle(title);
        }

    }
}
