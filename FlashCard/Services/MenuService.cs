using Microsoft.AspNetCore.Components;

namespace FlashCard.Services
{
    public interface IMenuService
    {
        Task SetMenu(RenderFragment? menu);
        event Action<RenderFragment?>? OnMenuChanged;

        Task SetHeader(bool? show);
        event Action<bool?>? OnHeaderChanged;

        Task SetSidebar(Pages.Admin.Settings.SidebarTheme? theme);
        event Action<Pages.Admin.Settings.SidebarTheme?>? OnSidebarChanged;

        Task SetApplication(Pages.Admin.Settings.ApplicationTheme? theme);
        event Action<Pages.Admin.Settings.ApplicationTheme?>? OnApplicationChanged;

        Task SetBackground(Pages.Admin.Settings.AnimationMode? theme);
        event Action<Pages.Admin.Settings.AnimationMode?>? OnBackgroundChanged;
    }

    public class MenuService : IMenuService
    {

        public event Action<RenderFragment?>? OnMenuChanged;

        public async Task SetMenu(RenderFragment? menu)
        {
            OnMenuChanged?.Invoke(menu);
        }

        public event Action<bool?>? OnHeaderChanged;

        public async Task SetHeader(bool? show)
        {
            OnHeaderChanged?.Invoke(show);
        }

        public event Action<Pages.Admin.Settings.SidebarTheme?>? OnSidebarChanged;
        public async Task SetSidebar(Pages.Admin.Settings.SidebarTheme? theme)
        {
            OnSidebarChanged?.Invoke(theme);
        }

        public event Action<Pages.Admin.Settings.ApplicationTheme?>? OnApplicationChanged;

        public async Task SetApplication(Pages.Admin.Settings.ApplicationTheme? theme)
        {
            OnApplicationChanged?.Invoke(theme);
        }

        public event Action<Pages.Admin.Settings.AnimationMode?>? OnBackgroundChanged;

        public async Task SetBackground(Pages.Admin.Settings.AnimationMode? theme)
        {
            OnBackgroundChanged?.Invoke(theme);
        }
    }
}
