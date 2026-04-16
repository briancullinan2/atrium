#if WINDOWS
using Atrium.Platforms.Windows;

#endif
using Atrium.Services;
using Interfacing.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;

namespace Atrium;

public partial class App : Microsoft.Maui.Controls.Application //, IHasCurrent<Application>
{
    public App()
    {
        InitializeComponent();
    }

    // TODO: WINDOWS ONLY?
    protected override Microsoft.Maui.Controls.Window CreateWindow(IActivationState? activationState)
    {
        return WindowManager.CreateWindow();
    }


}