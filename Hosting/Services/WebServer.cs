#if WINDOWS
using DataShared.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
#endif

namespace Hosting.Services;

// lol, just had an idea to easily slide this into a service-worker like my http cache, no reason i cant
//  use that worker as a background task for actually generating the pages i need.

public class WebServer(
#if WINDOWS
    ITrustProvider trust
#endif
) : IHasModule //, IHasCurrent<WebApplication>
{
#if WINDOWS
    private static WebApplication? _private;
    public static WebApplication? Current => _private;
#endif

    internal static bool IsStarting = false;
    private static TaskCompletionSource<bool> _renderTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool IsReady => _renderTcs.Task.IsCompleted && _renderTcs.Task.Result == true;

    //public static IServiceProvider Services => _private.Services;
    private static readonly Func<string?, bool> FILTER_MICROSOFT_DLLS_BY_NAME;
    private static readonly MethodInfo serviceBuilder;
    private static readonly Type? mainLoader;
    private static readonly List<Type> builtIn;

    static WebServer()
    {
        FILTER_MICROSOFT_DLLS_BY_NAME = title => string.IsNullOrEmpty(title) || title.StartsWith("System.") || title.StartsWith("Microsoft.") || title.StartsWith("WinRT.");

        var atrium = Assembly.GetExecutingAssembly()
            .GetAssemblies().FirstOrDefault(ass => ass.ToName() == "Atrium");

        var extensions = atrium?.GetType("Atrium.Extensions.BuilderExtensions")
            ?? throw new InvalidOperationException("Can't find BuilderExtensions, this probably won't work.");

        serviceBuilder = extensions.GetMethods("BuildServices", null, [typeof(IServiceCollection), typeof(List<Type>), typeof(string), typeof(IServiceProviderIsService), typeof(bool)]).FirstOrDefault()
            ?? throw new InvalidOperationException("Can't find BuilderExtensions.BuildServices, this probably won't work.");

        mainLoader = atrium.GetType("Atrium.Components.RootComponent")
            ?? throw new InvalidOperationException("Can't find RootComponent, this probably won't work.");

        var componentBuilder = atrium.GetType("Atrium.Services.CompositeServiceProvider")
            ?? throw new InvalidOperationException("Can't find CompositeServiceProvider, this probably won't work.");

        builtIn = componentBuilder.GetProperty("BuiltIn", BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null) as List<Type>
            ?? throw new InvalidOperationException("Can't find CompositeServiceProvider.BuiltIn, this probably won't work.");

    }

    public async ValueTask EnsureInitialized()
    {
        if (IsStarting) await _renderTcs.Task;
        if (_renderTcs.Task.IsCompleted)
            _renderTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
#if WINDOWS
        StartWebServer(trust);
#else
        // WAKE ON LAN?
        IsStarting = true;
        _renderTcs.SetResult(false);
         IsStarting = false;
#endif
        await _renderTcs.Task;
    }




#if WINDOWS


    public static WebApplication? StartWebServer(ITrustProvider? Trust)
    {
        try
        {
            if (Current != null || IsStarting ||
                (_private?.Lifetime.ApplicationStarted.IsCancellationRequested == true
                && _private?.Lifetime.ApplicationStopped.IsCancellationRequested != true))
                return _private;

            IsStarting = true;
            // TODO: get logging working
            Console.WriteLine("Starting web server.");
            var webBuilder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = [],
                // Ensure the server looks in the actual folder where the assets live
                ContentRootPath = AppDomain.CurrentDomain.BaseDirectory,
                ApplicationName = "Atrium"
            });

            //#if DEBUG
            //            webBuilder.Environment.EnvironmentName = Environments.Development;
            //#else
            webBuilder.Environment.EnvironmentName = Environments.Production;
            //#endif

            //TODO: make this optional webBuilder.Services.AddDirectoryBrowser();
            webBuilder.Services.AddRazorComponents()
                .AddInteractiveServerComponents()
                .AddInteractiveWebAssemblyComponents();

            webBuilder.Services.AddServerSideBlazor(options =>
            {
                options.DetailedErrors = true;
            });


            webBuilder.Services.AddSingleton(sp => new HttpClient
            {
                BaseAddress = new Uri("https://127.0.0.1:8080") //TODO: make this dynamic
            });


#if !BROWSER
            //webBuilder.Services.AddBlazorWebViewDeveloperTools();
            // Inject the server instance into MAUI's DI
            //ServerAuthService.BuildAuthentication(webBuilder.Services);
#endif

            // TODO: try to get every service from the existing container instead of building a new one:
            // TODO: this disctinction will become the multi-tenant feature

            serviceBuilder?.Invoke(null, [webBuilder.Services, builtIn.Concat([typeof(FormFactor)]).ToList(), null, null, false]);

            DatabaseBuilder.BuildServices(webBuilder.Services);

            //webBuilder.Services.AddScoped<IPersistentComponentStateStore, PrerenderComponentStateStore>();
            // always have to use the apps browser instance for the local store
            //   TODO: web server should be using SQLite anyways
            //webBuilder.Services.AddSingleton<Lazy<ILocalStore?>>(sp => new Lazy<ILocalStore?>(MauiProgram.Current?.Services.GetService<ILocalStore>()));
            //webBuilder.Services.AddSingleton<ILocalStore>(sp => MauiProgram.Current.Services.GetRequiredService<ILocalStore>());
            webBuilder.Services.AddSingleton<Lazy<WebApplication?>>(sp => new Lazy<WebApplication?>(() => Current));


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
            webBuilder.WebHost.UseSetting("Microsoft.AspNetCore.Hosting.HotReload.Enabled", "false");

            //webBuilder.WebHost.UseStaticWebAssets();
            //Microsoft.AspNetCore.Hosting.StaticWebAssets.StaticWebAssetsLoader.UseStaticWebAssets(
            //    webBuilder.Environment,
            //    webBuilder.Configuration);


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


            // TODO: add brotli caching to all this

            var webApp = _private = webBuilder.Build();

            //var options = new DefaultFilesOptions();
            //options.DefaultFileNames.Clear(); // Remove index.html from the search list
            //options.DefaultFileNames.Add("index.html"); // Make app.html the new default

            //webApp.UseDefaultFiles(options);
            //webApp.MapFallbackToFile("app.html");
            webApp.Use((context, next) =>
            {
                // i hate that i have to do this, WHAT IS THEY GET AROUND TO FIXING SOMETHING????
                //    i'll end up with a bunch of browsers sitting around with old dependencies in their cache?

                // this had no effect because microsoft is loading it internally, this won't be an issue when i get the service worker working again
                //if (FILTER_MICROSOFT_DLLS_BY_NAME(context.Request.Path.ToString().Split("_framework/").ElementAtOrDefault(1)))
                //    return next();
                if (context.Request.Path.Value?.Contains(".well-known") == true)
                {
                    context.Abort();
                    return Task.CompletedTask;
                }

                context.Response.Headers.Append("Cache-Control", "no-store, no-cache, must-revalidate, max-age=0");
                context.Response.Headers.Append("Pragma", "no-cache");
                context.Response.Headers.Append("Expires", "0");

                return next();
            });
            webApp.Use(async (context, next) => {
                try
                {

                    if (context.Request.Path.Value?.EndsWith(".wasm") == true)
                    {
                        if (context.Request.Path.Value.Contains("_framework/")
                        && !File.Exists(Path.Combine(TypeExtensions.entryDirectory ?? string.Empty, "wwwroot", context.Request.Path.Value.Trim('/')))
                        && Path.Combine(TypeExtensions.entryDirectory ?? string.Empty, "wwwroot", context.Request.Path.Value.Trim('/').Replace(".wasm", ".dll")) is string dllPath
                        && File.Exists(dllPath))
                        {
                            // 1. Find the actual DLL on disk
                            byte[] rawDll = await File.ReadAllBytesAsync(dllPath);

                            // 2. Wrap it on the fly
                            byte[] webcil = WrapDllInWebcil(rawDll);

                            // 3. Serve as WASM
                            context.Response.ContentType = "application/wasm";
                            await context.Response.Body.WriteAsync(webcil);
                            await context.Response.Body.FlushAsync();
                            await context.Response.CompleteAsync();
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
                await next();
            });
            webApp.UseBlazorFrameworkFiles();
            webApp.UseStaticFiles();

            // 2. Security & Routing
            webApp.UseRouting();
            webApp.UseCors("_myAllowSpecificOrigins");
            webApp.UseAntiforgery(); // Keep this enabled!
            webApp.UseAuthorization();


            //webApp.MapGet("/api/status", () => new { Status = "Online", Machine = Environment.MachineName });

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

            webApp.MapFullCircuits(webBuilder.Services);

            //webApp.MapHub<Hub>("/_blazor");
            var mapRazor = typeof(RazorComponentsEndpointRouteBuilderExtensions)
                .GetMethod(nameof(RazorComponentsEndpointRouteBuilderExtensions.MapRazorComponents))
                ?.MakeGenericMethod(mainLoader!)
                ?? throw new InvalidOperationException("Failed to render MapRazorComponents.");

            var builder = mapRazor.Invoke(null, [webApp]) as RazorComponentsEndpointConventionBuilder;

            builder?.AddInteractiveServerRenderMode()
                .AddInteractiveWebAssemblyRenderMode()
                .AddAdditionalAssemblies(
                //typeof(FlashCard._Imports).Assembly,
                //typeof(Merchantry._Imports).Assembly,
                typeof(Hosting.Pages._Imports).Assembly,
                typeof(RazorSharp._Imports).Assembly
                )
                .DisableAntiforgery();


            _ = TryRunning();
            // don't do this here because we're hijacking mains
            //_ = webApp.Services.GetRequiredService<SimpleLogger>();
            return _private;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Web server failed to start: " + ex);
            _renderTcs.SetException(ex);
            return null;
        }
        finally
        {
            IsStarting = false;
        }
    }
#endif



    protected static async Task TryRunning()
    {
#if WINDOWS
        if (_private == null) return;
        // Run the Web Server in the background
        try
        {
            using (_private.Lifetime.ApplicationStarted.Register(() => _renderTcs.SetResult(true)))
            {
                _ = _private.RunAsync();
                await _renderTcs.Task;
            }
        }
        catch (Exception ex)
        {
            _renderTcs.SetException(ex);
        }
        finally
        {
            IsStarting = false;
        }
#endif
    }

    public static byte[] WrapDllInWebcil(byte[] dllBytes)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // 1. WASM Magic Number & Version
        writer.Write([0x00, 0x61, 0x73, 0x6D]); // \0asm
        writer.Write([0x01, 0x00, 0x00, 0x00]); // Version 1

        // 2. Custom Section Header (Section ID 0)
        writer.Write((byte)0);

        // 3. Calculate Section Length
        // Section Name ("webcil") + its length prefix + the actual DLL bytes
        byte[] sectionName = Encoding.UTF8.GetBytes("webcil");
        int payloadSize = 1 + sectionName.Length + dllBytes.Length;

        // Write size as LEB128 (Variable length integer)
        WriteLEB128(writer, payloadSize);

        // 4. Write Section Name
        writer.Write((byte)sectionName.Length);
        writer.Write(sectionName);

        // 5. The Payload
        writer.Write(dllBytes);

        return ms.ToArray();
    }

    // Helper to write WASM-style variable integers
    private static void WriteLEB128(BinaryWriter writer, int value)
    {
        do
        {
            byte b = (byte)(value & 0x7F);
            value >>= 7;
            if (value != 0) b |= 0x80;
            writer.Write(b);
        } while (value != 0);
    }
}

