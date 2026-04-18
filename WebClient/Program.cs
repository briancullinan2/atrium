



using DataShared.Extensions;
using Extensions.PrometheusTypes;
using Interfacing.Services;
using RazorSharp.Layout;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using static System.Net.WebRequestMethods;

internal class Program
{
    private static WebAssemblyHost? _app;

    private static async Task Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            Console.WriteLine(e.ExceptionObject as Exception);

        // 2. Catch exceptions in 'set and forget' tasks (Async)
        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            Console.WriteLine(e.Exception.InnerException ?? e.Exception);
            e.SetObserved(); // Prevents process crash if you want, but logs it
        };

        var builder = WebAssemblyHostBuilder.CreateDefault(args);

        var Http = new HttpClient
        {
            BaseAddress = new Uri(builder.HostEnvironment.BaseAddress.Trim('/'))
        };

        byte[] wasmBytes = await Http.GetByteArrayAsync($"/_framework/Atrium.wasm");

        Console.WriteLine("Adding Atrium: " + wasmBytes);

        var assembly = Assembly.Load(wasmBytes);

        var domain = new List<Assembly>() { typeof(MainLayout).Assembly, typeof(IHasClass).Assembly }
            .Concat(AppDomain.CurrentDomain.GetAssemblies())
            .ToList();

        Console.WriteLine("Domain: " + JsonSerializer.Serialize(domain.Select(t => t.ToName()).ToList()));

        var mine = domain
            .Where(Extensions.PrometheusTypes.TypeExtensions.IsMine)
            .ToList();

        Console.WriteLine("Mine: " + JsonSerializer.Serialize(mine.Select(t => t.ToName()).ToList()));

        var serviceTypes = mine
            .SelectMany(Extensions.PrometheusTypes.TypeExtensions.GetAssTypesSafely)
            .GetServicable()
            .ToList();

        Console.WriteLine("Services: " + JsonSerializer.Serialize(serviceTypes.Select(t => t.Name).ToList()));

        DatabaseBuilder.BuildServices(builder.Services, serviceTypes);

        builder.Services.RemoveAll<IQueryManager>();
        //builder.Services.AddSingleton<IQueryManager, RemoteManager>();
        builder.Services.AddSingleton(sp => Http);

        builder.Services.AddSingleton<Lazy<WebAssemblyHost?>>(sp => new Lazy<WebAssemblyHost?>(_app));

        builder.Services.AddSingleton(sp => new HttpClient
        {
            BaseAddress = new Uri(builder.HostEnvironment.BaseAddress.Trim('/'))
        });

        builder.RootComponents.Add<WebClient.Components.Routes>("#app");

        _app = builder.Build();
        // FUCK DI
        //_ = _app.Services.GetRequiredService<SimpleLogger>();


        await _app.RunAsync();
    }
}