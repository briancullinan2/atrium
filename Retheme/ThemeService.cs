using System.ComponentModel;
using System.Reflection;

namespace Retheme;


public class ThemeService : IThemeService
{

    public event Action<SidebarTheme?>? OnSidebarChanged;
    public async Task SetSidebar(SidebarTheme? theme)
    {
        OnSidebarChanged?.Invoke(theme);
    }

    public event Action<ApplicationTheme?>? OnApplicationChanged;

    public async Task SetApplication(ApplicationTheme? theme)
    {
        OnApplicationChanged?.Invoke(theme);
    }

    public event Action<AnimationMode?>? OnBackgroundChanged;

    public async Task SetBackground(AnimationMode? theme)
    {
        OnBackgroundChanged?.Invoke(theme);
    }
}

