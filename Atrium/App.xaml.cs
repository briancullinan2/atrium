#if WINDOWS
using Atrium.Platforms.Windows;
#endif
#if !BROWSER
using Atrium.Services;
using Interfacing.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
#endif

namespace Atrium;

public partial class App
#if !BROWSER
    : Microsoft.Maui.Controls.Application //, IHasCurrent<Application>
#endif
{
#if !BROWSER
    public App()
    {
        InitializeComponent();
    }

    // TODO: WINDOWS ONLY?
    protected override Microsoft.Maui.Controls.Window CreateWindow(IActivationState? activationState)
    {
        return WindowManager.CreateWindow();
    }
#endif

}