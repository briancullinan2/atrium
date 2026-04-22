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


// i am such a dumbass. i try to get this IHasCurrent thing working for extra singleton singletons, and 
//   i somehow manage to give the service container permission to create applications at will, that
//   absolute fucking opposite of what i intended is my default go to, imagine if this dumbass 
//   machine was allowed to run it's entire life this way.

public partial class App
#if !BROWSER
    : Microsoft.Maui.Controls.Application //, IHasCurrent<App>
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