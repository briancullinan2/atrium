
using Microsoft.Maui.Controls;

namespace Atrium;


public static class MauiProgram
{

    private static readonly MauiApp _myApp = CreateMauiApp();
    public static MauiApp Current => _myApp;



    public static void BuildSharedServiceList(IServiceCollection Services)
    {

        SharedRegistry.BuildSharedServiceList(Services);

        // add ourselves
        Services.AddSingleton<Lazy<Application?>>(sp => new Lazy<Application?>(App.Current));

        Services.AddSingleton<Lazy<MauiApp?>>(sp => new Lazy<MauiApp?>(Current));
        
        // TODO: make this optional for server only service mode
        Services.AddSingleton<MauiApp>(sp => sp.GetRequiredService<Lazy<MauiApp>>().Value);
#if WINDOWS
        Services.AddSingleton<Lazy<WebApplication?>>(sp => new Lazy<WebApplication?>(AtriumWebServer.Current));
#endif

        // Add device-specific services used by the FlashCard project
        Services.AddSingleton<IConnectionStateProvider, CircuitHandler>(sp => sp.GetRequiredService<CircuitHandler>());

        Services.AddSingleton<IFormFactor, FormFactor>();
        Services.AddSingleton<IFileManager, FileManager>();
        Services.AddSingleton<IAnkiService, AnkiService>();
        Services.AddSingleton<IHostingService, HostingService>();
        Services.AddSingleton<IChatService, ChatService>();


        Services.AddScoped<IAuthService, Services.AuthService>();
        Services.AddScoped(sp => (Services.AuthService)sp.GetRequiredService<IAuthService>());
        Services.AddScoped(sp => (FlashCard.Services.AuthService)sp.GetRequiredService<IAuthService>());

        Services.AddSingleton(sp => new HttpClient
        {
            BaseAddress = new Uri("https://0.0.0.1")
        });

        Services.AddDbContextFactory<DataLayer.EphemeralStorage>();
        Services.AddDbContextFactory<DataLayer.PersistentStorage>(options =>
            options.UseSqlite("Data Source=" + Path.Combine(AppContext.BaseDirectory, "Atrium.sqlite.db")));

#if DEBUG
        Services.AddBlazorWebViewDeveloperTools();
#endif
        // Inject the server instance into MAUI's DI
#if WINDOWS
        Atrium.Services.AuthService.BuildAuthentication(Services);
#endif

    }



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


        BuildSharedServiceList(builder.Services);

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
