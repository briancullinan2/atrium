// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.


using Atrium.Platforms.Windows;
using System.Diagnostics;

namespace Atrium.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{


    [STAThread]
    public static void Main(string[] args)
    {
        log4net.Config.XmlConfigurator.Configure(new FileInfo(Path.Combine(AppContext.BaseDirectory, "log4net.xml")));

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            Console.WriteLine(e.ExceptionObject as Exception);

        // 2. Catch exceptions in 'set and forget' tasks (Async)
        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            Console.WriteLine(e.Exception.InnerException ?? e.Exception);
            e.SetObserved(); // Prevents process crash if you want, but logs it
        };

        // TODO: insert webserver activation here if running in windows service mode
        System.AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
        {

            var name = (new AssemblyName(args.Name).Name
                ?? args.Name.Split(',')[0]) + ".dll";
            
            if (name.Contains(".WebAssembly") || name.Contains(".Browser"))
            {
                return null;
            }

            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name);
            if (File.Exists(path))
            {
                return Assembly.LoadFrom(path);
            }
            string sharedPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                @"dotnet\shared\Microsoft.AspNetCore.App\10.0.0",
                name
            );
            if (File.Exists(sharedPath))
            {
                return Assembly.LoadFrom(sharedPath);
            }
            return null;
        };

        //Bootstrap.Initialize(0x00010004);

        WinRT.ComWrappersSupport.InitializeComWrappers();

        Start((p) =>
        {
            var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
    }


    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        EnsureSingleInstance();

        base.OnLaunched(args);

        // Get the handle from the first window in the MAUI application
        var mauiWindow = Microsoft.Maui.Controls.Application.Current?.Windows[0];
        var nativeWindow = mauiWindow?.Handler.PlatformView as Microsoft.UI.Xaml.Window;

        if (nativeWindow != null)
        {
            nativeWindow.ExtendsContentIntoTitleBar = true;

            var handle = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            Shell32.DragAcceptFiles(handle, 1);
            User32.AllowDrops(handle);

            appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            appWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
            appWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
            // This path looks in your bin output folder for the icon
            // Ensure "appicon.ico" is actually being copied there by our MSBuild target
            appWindow.SetIcon("appicon.ico");
        }
    }

    public static void EnsureSingleInstance()
    {
        Process current = Process.GetCurrentProcess();

        // Find processes with the same name, excluding the current one
        Process? other = Process.GetProcessesByName(current.ProcessName)
            .FirstOrDefault(p => p.Id != current.Id);

        if (other != null)
        {
            // 1. Switch to the existing window
            User32.FocusProcess(other);

            // 2. Exit the current instance
            Environment.Exit(0);
        }
    }
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>

    public App()
    {
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp()
    {
        return MauiProgram.Current;
    }



}
