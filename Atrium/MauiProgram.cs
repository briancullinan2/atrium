#if WINDOWS
#if DEBUG
using Microsoft.Extensions.Logging;
#endif
#endif
using Atrium.Services;
using DataLayer.Entities;
using FlashCard.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using DataLayer.Utilities;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using DataLayer;
using Microsoft.AspNetCore.Builder;
using Antlr4.Runtime.Misc;

namespace Atrium
{

    public static class MauiProgram
    {

        private static readonly MauiApp _myApp = CreateMauiApp();
        public static MauiApp Current
        {
            get => _myApp;
        }



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


            Services.AddScoped<IAuthService, DatabaseStateProvider>();
            Services.AddScoped(sp => (DatabaseStateProvider)sp.GetRequiredService<IAuthService>());

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
            DatabaseStateProvider.BuildAuthentication(Services);
#endif

        }



        private static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });


            BuildSharedServiceList(builder.Services);
            builder.Services.AddSingleton<ITitleService, TitleService>();
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


}
