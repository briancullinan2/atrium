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
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Atrium.Services
{
    internal static class AtriumWebServer
    {

        private static readonly WebApplication _private = StartWebServer([]);
        public static WebApplication Current {
            get => _private;
        }

        public static WebApplication StartWebServer(string[] args)
        {
            try
            {
                // TODO: get logging working
                Console.WriteLine("Starting web server.");
                var webBuilder = WebApplication.CreateBuilder(new WebApplicationOptions
                {
                    Args = args,
                    // Ensure the server looks in the actual folder where the assets live
                    ContentRootPath = AppDomain.CurrentDomain.BaseDirectory,
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


                MauiProgram.BuildSharedServiceList(webBuilder.Services);
                webBuilder.Services.AddSingleton<CircuitHandler>();
                webBuilder.Services.AddScoped<Microsoft.AspNetCore.Components.Server.Circuits.CircuitHandler>(sp => sp.GetRequiredService<CircuitHandler>());

                webBuilder.Services.AddSingleton<ITitleService, TitleTrackerService>();

                webBuilder.Services.AddSignalR()
                    .AddJsonProtocol()
                    .AddMessagePackProtocol();
                webBuilder.Services.AddHttpContextAccessor();
                

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



                var webApp = webBuilder.Build();

                //var options = new DefaultFilesOptions();
                //options.DefaultFileNames.Clear(); // Remove index.html from the search list
                //options.DefaultFileNames.Add("index.html"); // Make app.html the new default

                //webApp.UseDefaultFiles(options);
                //webApp.MapFallbackToFile("app.html");

                webApp.UseStaticFiles();
                webApp.UseBlazorFrameworkFiles();

                // 2. Security & Routing
                webApp.UseRouting();
                webApp.UseCors("_myAllowSpecificOrigins");
                webApp.UseAntiforgery(); // Keep this enabled!
                webApp.UseAuthorization();


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

                // 2. Mapping happens AFTER routing is configured
                //webApp.MapBlazorHub();
                webApp.UseExceptionHandler("/error", createScopeForErrors: true);

                webApp.MapGet("/version.json", HostingService.OnVersionCheck);
                webApp.MapPost("/api/query", QueryService.RespondQuery).RequireCors(myAllowSpecificOrigins);
                webApp.MapPost("/api/upload", FileManager.OnUploadFile).RequireCors(myAllowSpecificOrigins);
                webApp.MapPost("/api/inspect", AnkiService.OnInspectFile).RequireCors(myAllowSpecificOrigins);
                webApp.MapPost("/api/search", AnkiService.OnSearchAnki).RequireCors(myAllowSpecificOrigins);
                webApp.MapPost("/api/download", AnkiService.OnDownloadAnki).RequireCors(myAllowSpecificOrigins);
                webApp.MapPost("/api/status", HostingService.OnStatusCheck).RequireCors(myAllowSpecificOrigins);
                webApp.MapPost("/api/chat/presets", ChatService.OnPresets).RequireCors(myAllowSpecificOrigins);
                webApp.MapPost("/api/chat/ping", ChatService.OnPing).RequireCors(myAllowSpecificOrigins);
                webApp.MapPost("/api/chat", ChatService.OnChat).RequireCors(myAllowSpecificOrigins);

                //webApp.MapHub<Hub>("/_blazor");

                webApp.MapRazorComponents<Components.App>()
                    .AddInteractiveServerRenderMode()
                    .AddInteractiveWebAssemblyRenderMode()
                    .AddAdditionalAssemblies(
                    typeof(FlashCard._Imports).Assembly,
                    typeof(WebClient._Imports).Assembly)
                    .DisableAntiforgery();


                // Run the Web Server in the background
                _ = webApp.RunAsync().Forget();
                _ = webApp.Services.GetRequiredService<SimpleLogger>();

                return webApp;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Web server failed to start: " + ex.Message, ex);
                throw new Exception("bs", ex);
            }
        }
    }
}
#endif
