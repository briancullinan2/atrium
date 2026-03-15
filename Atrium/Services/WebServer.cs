using FlashCard.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Atrium.Services
{
    internal static class WebServer
    {
        private static KeepAlive? _keepAlive;

        public static async Task StartWebServer(string[] args)
        {
#if WINDOWS
            var webBuilder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                // Ensure the server looks in the actual folder where the assets live
                ContentRootPath = AppContext.BaseDirectory,
                ApplicationName = "Atrium"
            });
#if DEBUG
            webBuilder.Environment.EnvironmentName = Environments.Development;
#else
            webBuilder.Environment.EnvironmentName = Environments.Production;
#endif

            webBuilder.Services.AddDirectoryBrowser();
            webBuilder.Services.AddRazorComponents()
                .AddInteractiveServerComponents()
                .AddInteractiveWebAssemblyComponents();

            webBuilder.Services.AddServerSideBlazor(options =>
            {
                options.DetailedErrors = true;
            });

            // Add device-specific services used by the FlashCard project
            webBuilder.Services.AddSingleton<IFormFactor, FormFactor>();
            webBuilder.Services.AddSingleton<ITitleService, TitleTrackerService>();
            webBuilder.Services.AddSingleton<IMenuService, MenuService>();
            webBuilder.Services.AddSingleton<IStudyService, StudyService>();
            webBuilder.Services.AddSingleton<ILoginService, LoginService>();
            webBuilder.Services.AddSingleton<ICourseService, CourseService>();
            webBuilder.Services.AddSingleton<IJsonService, JsonService>();
            webBuilder.Services.AddSingleton<IFileManager, FileManager>();
            webBuilder.Services.AddSingleton<IAnkiService, AnkiService>();
            webBuilder.Services.AddSingleton<IStatusService, StatusService>();
            webBuilder.Services.AddSingleton<IThemeService, ThemeService>();
            webBuilder.Services.AddSingleton<IChatService, ChatService>();
            webBuilder.Services.AddScoped<HttpClient>(sp => new HttpClient
            {

            });

            // FUCK DI
            webBuilder.Services.AddSingleton<ILocalServer, LocalServer>();
            _keepAlive = new KeepAlive("Data Source=:memory:");
            _keepAlive.Open();
            webBuilder.Services.AddDbContextFactory<DataLayer.EphemeralStorage>(options =>
                options.UseSqlite(_keepAlive));

            webBuilder.Services.AddDbContextFactory<DataLayer.PersistentStorage>(options =>
                options.UseSqlite("Data Source=" + Path.Combine(AppContext.BaseDirectory, "Atrium.sqlite.db")));

            webBuilder.Environment.WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            webBuilder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(8080); // Open for business on port 8080
            });

            webBuilder.WebHost.UseStaticWebAssets();
            Microsoft.AspNetCore.Hosting.StaticWebAssets.StaticWebAssetsLoader.UseStaticWebAssets(
                webBuilder.Environment,
                webBuilder.Configuration);

            var webApp = webBuilder.Build();
            using (var scope = webApp.Services.CreateScope())
            {
                var ephemeralStore = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DataLayer.EphemeralStorage>>();
                using var memoryContext = ephemeralStore.CreateDbContext();
                var conn = memoryContext.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open) conn.Open();
                memoryContext.Database.EnsureCreated();

                var persistentStore = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DataLayer.PersistentStorage>>();
                using var persistentContext = persistentStore.CreateDbContext();
                var conn2 = persistentContext.Database.GetDbConnection();
                if (conn2.State != System.Data.ConnectionState.Open) conn.Open();
                persistentContext.Database.EnsureCreated();
                persistentContext.SaveChanges();

            }

            var localServer = (LocalServer)webApp.Services.GetRequiredService<ILocalServer>();
            localServer.Initialize(webApp);

            MauiProgram.ServerInstance = webApp;


            //webApp.MapGet("/api/status", () => new { Status = "Online", Machine = Environment.MachineName });
            webApp.Use((context, next) =>
            {
                context.Response.Headers.Append("Cache-Control", "no-store, no-cache, must-revalidate, max-age=0");
                context.Response.Headers.Append("Pragma", "no-cache");
                context.Response.Headers.Append("Expires", "0");
                return next();
            });

            if (webApp.Environment.IsDevelopment())
            {
                webApp.UseWebAssemblyDebugging();
            }
            else
            {
                webApp.UseHsts();
            }

            //webApp.UseHttpsRedirection();
            //webApp.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
            //webApp.UseAntiforgery();

            //webApp.UseDirectoryBrowser(new DirectoryBrowserOptions
            //{
            //    FileProvider = new PhysicalFileProvider(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot")),
            //    RequestPath = "/wwwroot" // Or just "" if you want it at the root
            //});
            //webApp.UseStaticFiles(new StaticFileOptions
            //{
            //    FileProvider = new PhysicalFileProvider(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "_framework")),
            //    RequestPath = "/_framework"
            //});

            webApp.UsePathBase("/");
            webApp.UseStaticFiles(); // Move this UP
            webApp.UseBlazorFrameworkFiles();
            webApp.UseAntiforgery();
            webApp.UseRouting();     // Move this UP

            // 2. Mapping happens AFTER routing is configured
            //webApp.MapBlazorHub();
            webApp.MapPost("/api/query", QueryService.RespondQuery);
            webApp.MapPost("/api/upload", FileManager.OnUploadFile);
            webApp.MapPost("/api/inspect", AnkiService.OnInspectFile);
            webApp.MapPost("/api/search", AnkiService.OnSearchAnki);
            webApp.MapPost("/api/download", AnkiService.OnDownloadAnki);
            webApp.MapPost("/api/status", StatusService.OnStatusCheck);
            webApp.MapPost("/api/chat/presets", ChatService.OnPresets);
            webApp.MapPost("/api/chat/ping", ChatService.OnPing);
            webApp.MapPost("/api/chat", ChatService.OnChat);

            webApp.MapRazorComponents<Components.App>()
                .AddInteractiveServerRenderMode()
                .AddInteractiveWebAssemblyRenderMode()
                .AddAdditionalAssemblies(
                typeof(FlashCard._Imports).Assembly,
                typeof(WebClient._Imports).Assembly)
                .DisableAntiforgery();


            // Run the Web Server in the background
            await Task.Run(async () =>
            {
                try
                {
                    await webApp.RunAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"!!! Kestrel Crash: {ex.Message}");
                }
            });
#endif
        }
    }
}
