//#if DEBUG
//using Microsoft.Extensions.Logging;
//#endif
using Interfacing.Services;

namespace Atrium;


public class MauiProgram : IHasService<MauiApp>
{

    private static readonly MauiApp _myApp = CreateMauiApp();
    public static MauiApp Current => _myApp;
    public static IServiceProvider Services => _myApp.Services;

    private static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        
        var args = Environment.GetCommandLineArgs();
        if (args.Any(a => a.StartsWith("app://")))
        {
            string protocolData = args.First(a => a.StartsWith("app://"));
            // TODO: Handle deep link / configuration inject here
        }

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        //builder.Services.AddSingleton<ILocalStore, LocalStore>();
        //builder.Services.AddSingleton<Lazy<ILocalStore?>>(sp => new Lazy<ILocalStore?>(sp.GetRequiredService<ILocalStore>()));
        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        //builder.Logging.AddDebug();
        //builder.Services.AddLogging(configure => configure.AddDebug());
#endif

#if !BROWSER
        // Inject the server instance into MAUI's DI
        //ServerAuthService.BuildAuthentication(builder.Services);
#endif
        //SharedRegistry.BuildSharedServiceList(builder.Services);

#if WINDOWS
        // get shared circuit state from web server
        //builder.Services.AddSingleton<CircuitHandler>(sp => AtriumWebServer.Current.Services.GetRequiredService<CircuitHandler>());
        // get a shared logger
        //builder.Services.AddSingleton<SimpleLogger>();
#endif


        var mauiApp = builder.Build();



#if WINDOWS

        Microsoft.Maui.Handlers.WindowHandler.Mapper.AppendToMapping("FileDrop", (h, v) =>
        {
            (mauiApp.Services.GetService(typeof(IFileManager)) as dynamic)?.InitializeWndProc(h);
        });
#endif


        return mauiApp;
    }


}
