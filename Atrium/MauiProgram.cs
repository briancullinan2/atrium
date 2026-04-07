

namespace Atrium;


public static class MauiProgram
{

    private static readonly MauiApp _myApp = CreateMauiApp();
    public static MauiApp Current => _myApp;


    private static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        
        var args = Environment.GetCommandLineArgs();
        if (args.Any(a => a.StartsWith("app://")))
        {
            string protocolData = args.First(a => a.StartsWith("app://"));
            // Handle deep link / configuration inject here
        }

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });


        //BuildSharedServiceList(builder.Services);

        builder.Services.AddSingleton<ILocalStore, LocalStore>();
        builder.Services.AddSingleton<Lazy<ILocalStore?>>(sp => new Lazy<ILocalStore?>(sp.GetRequiredService<ILocalStore>()));
        builder.Services.AddSingleton<ITitleService, Services.TitleService>();
        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

#if WINDOWS
        // start the web server
        builder.Services.AddSingleton<WebApplication>(sp => AtriumWebServer.Current);
        // get shared circuit state from web server
        builder.Services.AddSingleton<CircuitHandler>(sp => AtriumWebServer.Current.Services.GetRequiredService<CircuitHandler>());
        // get a shared logger
        builder.Services.AddSingleton<SimpleLogger>();
#endif


        var mauiApp = builder.Build();



#if WINDOWS

        Microsoft.Maui.Handlers.WindowHandler.Mapper.AppendToMapping("FileDrop", (h, v) =>
        {
            (mauiApp.Services.GetService(typeof(IFileManager)) as FileManager)?.InitializeWndProc(h);
        });
        // start the web server
        _ = mauiApp.Services.GetRequiredService<WebApplication>();
#endif
        _ = mauiApp.Services.GetRequiredService<SimpleLogger>();



        return mauiApp;
    }


}
