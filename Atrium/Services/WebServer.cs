#if WINDOWS
using Antlr4.Runtime.Misc;
using Atrium.Logging;
using DataLayer;
using DataLayer.Utilities;
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
        static WebServer()
        {
            Current = StartWebServer([]);
        }

        public static WebApplication Current { get; }

        public static WebApplication StartWebServer(string[] args)
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


                SharedRegistry.BuildSharedServiceList(webBuilder.Services);

                webBuilder.Services.AddSingleton<IFileManager, FileManager>();
                webBuilder.Services.AddSingleton<IAnkiService, AnkiService>();
                webBuilder.Services.AddSingleton<IHostingService, HostingService>();
                webBuilder.Services.AddSingleton<IChatService, ChatService>();
                // Add device-specific services used by the FlashCard project
                webBuilder.Services.AddSingleton<IFormFactor, FormFactor>();
                webBuilder.Services.AddSingleton<ITitleService, TitleTrackerService>();
                webBuilder.Services.AddSingleton<Application>(sp => App.Current!);


                webBuilder.Services.AddScoped<IAuthService, DatabaseStateProvider>();
                webBuilder.Services.AddScoped(sp => (DatabaseStateProvider)sp.GetRequiredService<IAuthService>());
                DatabaseStateProvider.BuildAuthentication(webBuilder.Services);

                webBuilder.Services.AddSingleton(sp => new HttpClient
                {
                    // TODO: insert our own address validated from settings and HostingService
                });

                webBuilder.Services.AddDbContextFactory<DataLayer.EphemeralStorage>();
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
                            policy.SetIsOriginAllowed(origin =>
                            {
                                // This is the "Per Request Callback"
                                var host = new Uri(origin).Host;

                                // Allow anything on your games domain or localhost
                                return host.EndsWith("pryor.games") ||
                                       host == "localhost" ||
                                       host == "127.0.0.1";
                            })
                            .AllowAnyHeader()
                            .AllowAnyMethod()
                            .AllowCredentials(); // Necessary if you want to send Cookies/Auth headers
                        });
                });


                WebApplication? webApp = null;
                webBuilder.Services.AddSingleton<WebApplication>(sp => (WebApplication)webApp!);

                webBuilder.Services.AddHttpContextAccessor();

                webApp = webBuilder.Build();
                _ = webApp.Services.GetRequiredService<SimpleLogger>();



                //webApp.MapGet("/api/status", () => new { Status = "Online", Machine = Environment.MachineName });
                webApp.Use((context, next) =>
                {
                    context.Response.Headers.Append("Cache-Control", "no-store, no-cache, must-revalidate, max-age=0");
                    context.Response.Headers.Append("Pragma", "no-cache");
                    context.Response.Headers.Append("Expires", "0");
                    // Check if the request is the browser asking for permission
                    if (context.Request.Method == "OPTIONS")
                    {
                        var origin = context.Request.Headers.Origin.ToString();
                        // Arizona Law: Verify the origin is one we trust
                        if (origin.Contains("pryor.games") || origin.Contains("localhost"))
                        {
                            context.Response.Headers.AccessControlAllowOrigin = origin;
                            context.Response.Headers.AccessControlAllowMethods = "GET, POST, OPTIONS";
                            context.Response.Headers.AccessControlAllowHeaders = "Content-Type, Authorization, Accept";
                            context.Response.Headers.AccessControlAllowCredentials = "true";
                            context.Response.StatusCode = 200; // Return OK immediately
                            return Task.CompletedTask;
                        }
                    }
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

                webApp.UseRouting();     // Move this UP

                webApp.UseCors("_myAllowSpecificOrigins");

                webApp.UseAntiforgery();
                webApp.UseAuthorization();

                // 2. Mapping happens AFTER routing is configured
                //webApp.MapBlazorHub();
                webApp.UseExceptionHandler("/error", createScopeForErrors: true);

                webApp.MapPost("/api/query", QueryService.RespondQuery).RequireCors(myAllowSpecificOrigins);
                webApp.MapPost("/api/upload", FileManager.OnUploadFile).RequireCors(myAllowSpecificOrigins);
                webApp.MapPost("/api/inspect", AnkiService.OnInspectFile).RequireCors(myAllowSpecificOrigins);
                webApp.MapPost("/api/search", AnkiService.OnSearchAnki).RequireCors(myAllowSpecificOrigins);
                webApp.MapPost("/api/download", AnkiService.OnDownloadAnki).RequireCors(myAllowSpecificOrigins);
                webApp.MapPost("/api/status", HostingService.OnStatusCheck).RequireCors(myAllowSpecificOrigins);
                webApp.MapPost("/api/chat/presets", ChatService.OnPresets).RequireCors(myAllowSpecificOrigins);
                webApp.MapPost("/api/chat/ping", ChatService.OnPing).RequireCors(myAllowSpecificOrigins);
                webApp.MapPost("/api/chat", ChatService.OnChat).RequireCors(myAllowSpecificOrigins);

                webApp.MapRazorComponents<Components.App>()
                    .AddInteractiveServerRenderMode()
                    .AddInteractiveWebAssemblyRenderMode()
                    .AddAdditionalAssemblies(
                    typeof(FlashCard._Imports).Assembly,
                    typeof(WebClient._Imports).Assembly)
                    .DisableAntiforgery();


                // Run the Web Server in the background
                _ = webApp.RunAsync().Forget();
                return webApp;
            }
            catch (Exception ex)
            {
                Log.Error("Web server failed to start: " + ex.Message, ex);
                throw new Exception("bs", ex);
            }
        }
    }
}
#endif
