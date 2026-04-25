
#if !BROWSER
using Microsoft.Maui.Devices;
#endif

using Atrium.Services;

namespace Atrium;


// i am such a dumbass. i try to get this IHasCurrent thing working for extra singleton singletons, and 
//   i somehow manage to give the service container permission to create applications at will, that
//   absolute fucking opposite of what i intended is my default go to, imagine if this dumbass 
//   machine was allowed to run it's entire life this way.

public partial class App
#if !BROWSER
    : Microsoft.Maui.Controls.Application, IHasCurrent<Application>
#endif
{

    public static WebViewBridge? Bridge { get; set; }


#if !BROWSER
    public App()
    {
        InitializeComponent();
    }

    public const int SPLASH_HEIGHT = 350;
    public const int SPLASH_WIDTH = 550;


    // TODO: WINDOWS ONLY?
    protected override Microsoft.Maui.Controls.Window CreateWindow(IActivationState? activationState)
    {
        return CreateWindow();
    }


    public static Microsoft.Maui.Controls.Window CreateWindow()
    {
        var window = new Microsoft.Maui.Controls.Window(new MainPage()) { Title = "Atrium" };

        // Get display dimensions
        var displayInfo = DeviceDisplay.Current.MainDisplayInfo;

        // Calculate center (convert pixels to density-independent units)
        window.X = (displayInfo.Width / displayInfo.Density - SPLASH_WIDTH) / 2;
        window.Y = (displayInfo.Height / displayInfo.Density - SPLASH_HEIGHT) / 2;

        window.Width = SPLASH_WIDTH;
        window.Height = SPLASH_HEIGHT;
        return window;
    }
#endif

}