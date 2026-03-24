#if WINDOWS
using Atrium.Logging;
using DataLayer.Utilities.Extensions;
using FlashCard.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
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
            try
            {
                // TODO: get logging working
                Log.Info("Starting web server.");
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

                webBuilder.Services.AddCascadingValue(sp => new ErrorBoundary());
                // Add device-specific services used by the FlashCard project
                webBuilder.Services.AddSingleton<IFormFactor, FormFactor>();
                webBuilder.Services.AddSingleton<ITitleService, TitleTrackerService>();
                webBuilder.Services.AddSingleton<IMenuService, MenuService>();
                webBuilder.Services.AddSingleton<IStudyService, StudyService>();
                webBuilder.Services.AddSingleton<ILoginService, LoginService>();
                webBuilder.Services.AddSingleton<ICourseService, CourseService>();
                webBuilder.Services.AddSingleton<IPageManager, PageManager>();
                webBuilder.Services.AddSingleton<IFileManager, FileManager>();
                webBuilder.Services.AddSingleton<IAnkiService, AnkiService>();
                webBuilder.Services.AddSingleton<IHostingService, HostingService>();
                webBuilder.Services.AddSingleton<IThemeService, ThemeService>();
                webBuilder.Services.AddSingleton<IChatService, ChatService>();
                webBuilder.Services.AddSingleton<DataLayer.Utilities.IQueryManager, DataLayer.Utilities.QueryManager>();
                webBuilder.Services.AddSingleton<IAuthService, AuthService>();
                webBuilder.Services.AddSingleton<NavigationTracker>();

                webBuilder.Services.AddAuthorizationCore();
                // Register your provider as the base class
                // TODO: change to AddSingleton?
                webBuilder.Services.AddSingleton<AuthenticationStateProvider, DatabaseStateProvider>();
                // "Alias" the concrete type to the same instance so MarkUserAsAuthenticated works
                webBuilder.Services.AddSingleton(sp => (DatabaseStateProvider)sp.GetRequiredService<AuthenticationStateProvider>());

                var authenticationBuilder = DatabaseStateProvider.BuildAuthentication(webBuilder);
                new AuthService(null).AddExternalLogins(authenticationBuilder);

                webBuilder.Services.AddScoped(sp => new HttpClient { });

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


                string myAllowSpecificOrigins = "_myAllowSpecificOrigins";

                webBuilder.Services.AddCors(options =>
                {
                    options.AddPolicy(name: myAllowSpecificOrigins,
                                      policy =>
                                      {
                                          policy.WithOrigins("https://study.pryor.games",
                                                              "http://localhost:8080") // Your local dev port
                                                .AllowAnyHeader()
                                                .AllowAnyMethod();
                                      });
                });

                var webApp = webBuilder.Build();

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
                webApp.UseAuthorization();

                // 2. Mapping happens AFTER routing is configured
                //webApp.MapBlazorHub();
                webApp.UseExceptionHandler("/error", createScopeForErrors: true);

                webApp.MapPost("/api/query", QueryService.RespondQuery);
                webApp.MapPost("/api/upload", FileManager.OnUploadFile);
                webApp.MapPost("/api/inspect", AnkiService.OnInspectFile);
                webApp.MapPost("/api/search", AnkiService.OnSearchAnki);
                webApp.MapPost("/api/download", AnkiService.OnDownloadAnki);
                webApp.MapPost("/api/status", HostingService.OnStatusCheck);
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
                webApp.RunAsync().Forget();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw new Exception("bs", ex);
            }
        }
    }
}
#endif
