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
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            SharedRegistry.BuildSharedServiceList(builder.Services);

            // Add device-specific services used by the FlashCard project
            builder.Services.AddSingleton<IFormFactor, FormFactor>();
            builder.Services.AddSingleton<ITitleService, TitleService>();
            builder.Services.AddSingleton<IFileManager, FileManager>();
            builder.Services.AddSingleton<IAnkiService, AnkiService>();
            builder.Services.AddSingleton<IHostingService, HostingService>();
            builder.Services.AddSingleton<IChatService, ChatService>();
            builder.Services.AddSingleton<Application>(sp => App.Current!);
#if WINDOWS
            builder.Services.AddSingleton<WebApplication>(sp => WebServer.Current);
#endif

            builder.Services.AddScoped<IAuthService, DatabaseStateProvider>();
            builder.Services.AddScoped(sp => (DatabaseStateProvider)sp.GetRequiredService<IAuthService>());

            builder.Services.AddSingleton(sp => new HttpClient
            {
                BaseAddress = new Uri("https://0.0.0.1")
            });

            builder.Services.AddDbContextFactory<DataLayer.EphemeralStorage>();
            builder.Services.AddDbContextFactory<DataLayer.PersistentStorage>(options =>
                options.UseSqlite("Data Source=" + Path.Combine(AppContext.BaseDirectory, "Atrium.sqlite.db")));

            builder.Services.AddMauiBlazorWebView();
#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
#endif
            // Inject the server instance into MAUI's DI
#if WINDOWS
            DatabaseStateProvider.BuildAuthentication(builder.Services);
#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif
#endif

            //RegisterPageConstraints(builder);

            // 1. Build the app
            var mauiApp = builder.Build();
            _ = mauiApp.Services.GetRequiredService<SimpleLogger>();
#if WINDOWS
            Microsoft.Maui.Handlers.WindowHandler.Mapper.AppendToMapping("FileDrop", (h, v) =>
            {
                (mauiApp.Services.GetService(typeof(IFileManager)) as FileManager)?.InitializeWndProc(h);
            });
            _ = mauiApp.Services.GetRequiredService<WebApplication>();
#endif


            // 3. Return the built app
            return mauiApp;
        }

        //public static void RegisterPageConstraints(IHostApplicationBuilder builder)
        //{
        //    builder.Services.Configure<RouteOptions>(opt => opt.ConstraintMap.Add("pack", typeof(EnumConstraint<PackMode>)));
        //}

    }


}
