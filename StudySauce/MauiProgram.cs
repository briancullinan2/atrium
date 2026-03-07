#if WINDOWS
#if DEBUG
using Microsoft.Extensions.Logging;
#endif
#endif
using DataLayer.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StudySauce.Services;
using StudySauce.Shared.Services;

namespace StudySauce
{
    public class KeepAlive : SqliteConnection
    {
        public KeepAlive(string conn) : base(conn)
        {

        }

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
        private static KeepAlive _keepAliveConnection;
        private static Pack? _pack;
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

            // Add device-specific services used by the StudySauce.Shared project
            builder.Services.AddSingleton<IFormFactor, FormFactor>();
            builder.Services.AddSingleton<ITitleService, TitleService>();
            builder.Services.AddSingleton<IMenuService, MenuService>();
            builder.Services.AddSingleton<IStudyService, StudyService>();
            builder.Services.AddSingleton<ILoginService, LoginService>();
            builder.Services.AddSingleton<ICourseService, CourseService>();
            builder.Services.AddSingleton<IJsonService, JsonService>();
            builder.Services.AddSingleton<IFileManager, FileManager>();
            _keepAliveConnection = new KeepAlive("Data Source=:memory:");
            _keepAliveConnection.Open(); // The DB is born
            builder.Services.AddDbContextFactory<DataLayer.EphemeralStorage>(options =>
                options.UseSqlite(_keepAliveConnection));

            builder.Services.AddDbContextFactory<DataLayer.PersistentStorage>(options =>
                options.UseSqlite("Data Source=" + Path.Combine(AppContext.BaseDirectory, "StudySauce.sqlite.db")));

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


            // 2. Now you can create a scope from the built app
            using (var scope = mauiApp.Services.CreateScope())
            {
                var ephemeralStore = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DataLayer.EphemeralStorage>>();
                using var memoryContext = ephemeralStore.CreateDbContext();
                var conn = memoryContext.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open) conn.Open();
                memoryContext.Database.EnsureCreated();
                //_pack = memoryContext.Packs.FirstOrDefault(p => p.Title == "something");

                var persistentStore = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DataLayer.PersistentStorage>>();
                using var persistentContext = persistentStore.CreateDbContext();
                var conn2 = persistentContext.Database.GetDbConnection();
                if (conn2.State != System.Data.ConnectionState.Open) conn.Open();
                persistentContext.Database.EnsureCreated();
                // TODO: add version setting and upgrades
                persistentContext.SaveChanges();
            }


            // 3. Return the built app
            return mauiApp;
        }

        //public static void RegisterPageConstraints(IHostApplicationBuilder builder)
        //{
        //    builder.Services.Configure<RouteOptions>(opt => opt.ConstraintMap.Add("pack", typeof(EnumConstraint<PackMode>)));
        //}

    }


}
