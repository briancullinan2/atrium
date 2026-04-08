#if !BROWSER
using Hosting.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.WebAssembly.Server;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Hosting;
using UserModel.Services;
#endif

namespace Hosting.Services;

public class WebServer
#if !BROWSER
    : IHasService<WebApplication>
#endif
{

#if !BROWSER
    private static readonly WebApplication _private = StartWebServer([]);
    public static WebApplication Current => _private;
    public static IServiceProvider Services => _private.Services;


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


            webBuilder.Services.AddSingleton(sp => new HttpClient
            {
                BaseAddress = new Uri("https://0.0.0.1")
            });


#if !BROWSER
            webBuilder.Services.AddBlazorWebViewDeveloperTools();
            // Inject the server instance into MAUI's DI
            ServerAuthService.BuildAuthentication(webBuilder.Services);
#endif
            SharedRegistry.BuildSharedServiceList(webBuilder.Services);

            // always have to use the apps browser instance for the local store
            //   TODO: web server should be using SQLite anyways
            //webBuilder.Services.AddSingleton<Lazy<ILocalStore?>>(sp => new Lazy<ILocalStore?>(MauiProgram.Current?.Services.GetService<ILocalStore>()));
            //webBuilder.Services.AddSingleton<ILocalStore>(sp => MauiProgram.Current.Services.GetRequiredService<ILocalStore>());

            // get a shared logger
            webBuilder.Services.AddScoped<SimpleLogger>(sp => 
                //MauiProgram.Current.Services.GetService<SimpleLogger>() 
                sp.GetKeyedService<SimpleLogger>("web")
                ?? new SimpleLogger(sp));
            webBuilder.Services.AddKeyedScoped<SimpleLogger>("web");

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
                //webApp.UseWebAssemblyDebugging();
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

            webApp.MapFullCircuits();

            //webApp.MapHub<Hub>("/_blazor");

            webApp.MapRazorComponents<WebClient.Components.App>()
                .AddInteractiveServerRenderMode()
                .AddInteractiveWebAssemblyRenderMode()
                .AddAdditionalAssemblies(
                //typeof(FlashCard._Imports).Assembly,
                //typeof(Merchantry._Imports).Assembly,
                //typeof(UserModel._Imports).Assembly,
                typeof(WebClient._Imports).Assembly)
                .DisableAntiforgery();


            // Run the Web Server in the background
            _ = webApp.RunAsync().Forget();
            // don't do this here because we're hijacking mains
            //_ = webApp.Services.GetRequiredService<SimpleLogger>();

            return webApp;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Web server failed to start: " + ex.Message, ex);
            throw new Exception("bs", ex);
        }
    }

#endif

}

