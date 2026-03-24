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

namespace Atrium
{
    public partial class KeepAlive(string conn) : SqliteConnection(conn)
    {
        public override void Close()
        {

        }

        public override Task CloseAsync()
        {
            return Task.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {

        }
    }

    public static class MauiProgram
    {
        private static KeepAlive? _keepAliveConnection;

#if WINDOWS
        public static Microsoft.AspNetCore.Builder.WebApplication? ServerInstance { get; set; }
#endif
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddCascadingValue(sp => new ErrorBoundary());

            // Add device-specific services used by the FlashCard project
            builder.Services.AddSingleton<IFormFactor, FormFactor>();
            builder.Services.AddSingleton<ITitleService, TitleService>();
            builder.Services.AddSingleton<IMenuService, MenuService>();
            builder.Services.AddSingleton<IStudyService, StudyService>();
            builder.Services.AddSingleton<ILoginService, LoginService>();
            builder.Services.AddSingleton<ICourseService, CourseService>();
            builder.Services.AddSingleton<IPageManager, PageManager>();
            builder.Services.AddSingleton<IFileManager, FileManager>();
            builder.Services.AddSingleton<IAnkiService, AnkiService>();
            builder.Services.AddSingleton<IHostingService, HostingService>();
            builder.Services.AddSingleton<IThemeService, ThemeService>();
            builder.Services.AddSingleton<IChatService, ChatService>();
            builder.Services.AddSingleton<IQueryManager, QueryManager>();
            builder.Services.AddSingleton<IAuthService, Services.AuthService>();
            builder.Services.AddSingleton<NavigationTracker>();

            builder.Services.AddAuthorizationCore();
            builder.Services.AddCascadingAuthenticationState();
            // Register your provider as the base class
            builder.Services.AddSingleton<AuthenticationStateProvider, DatabaseStateProvider>();
            // "Alias" the concrete type to the same instance so MarkUserAsAuthenticated works
            builder.Services.AddSingleton(sp => (DatabaseStateProvider)sp.GetRequiredService<AuthenticationStateProvider>());

            builder.Services.AddScoped(sp => new HttpClient
            {
                BaseAddress = new Uri("https://0.0.0.1")
            });

            _keepAliveConnection = new KeepAlive("Data Source=:memory:");
            _keepAliveConnection.Open(); // The DB is born
            builder.Services.AddDbContextFactory<DataLayer.EphemeralStorage>(options =>
                options.UseSqlite(_keepAliveConnection));

            builder.Services.AddDbContextFactory<DataLayer.PersistentStorage>(options =>
                options.UseSqlite("Data Source=" + Path.Combine(AppContext.BaseDirectory, "Atrium.sqlite.db")));

            builder.Services.AddMauiBlazorWebView();
#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
#endif
            // Inject the server instance into MAUI's DI
#if WINDOWS
            if (ServerInstance != null)
            {
                builder.Services.AddSingleton<ILocalServer>(new LocalServer(ServerInstance));
            }
#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif
#endif

            //RegisterPageConstraints(builder);

            // 1. Build the app
            var mauiApp = builder.Build();
            MainPage._services = mauiApp.Services;
            FileManager._services = mauiApp.Services;
            AnkiService._services = mauiApp.Services;
            HostingService._services = mauiApp.Services;
            ChatService._services = mauiApp.Services;
            QueryManager.Service = mauiApp.Services;
            SimpleLogger.Services = mauiApp.Services;

            // 3. Return the built app
            return mauiApp;
        }

        //public static void RegisterPageConstraints(IHostApplicationBuilder builder)
        //{
        //    builder.Services.Configure<RouteOptions>(opt => opt.ConstraintMap.Add("pack", typeof(EnumConstraint<PackMode>)));
        //}

    }


}
