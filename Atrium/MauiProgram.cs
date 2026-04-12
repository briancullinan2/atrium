//#if DEBUG
//using Microsoft.Extensions.Logging;
//#endif
using Atrium.Components;
using Atrium.Services;
using Interfacing.Services;
using Microsoft.AspNetCore.Components;

namespace Atrium;


public class MauiProgram : IHasCurrent<MauiApp>
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
            // TODO: Handle deep link / configuration inject here
        }

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddSingleton<TrustedLoader>();
        builder.Services.AddSingleton<ITrustProvider, TrustedLoader>(sp => sp.GetRequiredService<TrustedLoader>());
        builder.Services.AddSingleton<PluginActivator>();
        builder.Services.AddSingleton<IServiceProvider>(sp => sp.GetRequiredService<PluginActivator>().Services);
        builder.Services.AddSingleton<IServiceScopeFactory>(sp => (CompositeServiceProvider)sp.GetRequiredService<PluginActivator>().Services);
        builder.Services.AddSingleton<IComponentActivator>(sp => sp.GetRequiredService<PluginActivator>());
        builder.Services.AddSingleton<IServiceProviderIsService>(sp => sp.GetRequiredService<PluginActivator>());
        builder.Services.AddSingleton<Lazy<MainLoader?>>(sp => new Lazy<MainLoader?>(() => MainLoader.Current));
        builder.Services.AddSingleton<Lazy<Application?>>(sp => new Lazy<Application?>(() => Microsoft.Maui.Controls.Application.Current));
        //builder.Services.AddSingleton<Lazy<ILocalStore?>>(sp => new Lazy<ILocalStore?>(sp.GetRequiredService<ILocalStore>()));
        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        //builder.Logging.AddDebug();
        //builder.Services.AddLogging(configure => configure.AddDebug());
#endif

        // TODO: replace these with registering themselves
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
