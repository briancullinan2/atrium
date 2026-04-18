



using DataShared.Extensions;
using Extensions.PrometheusTypes;
using Interfacing.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Services;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using RazorSharp.Layout;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using static System.Net.WebRequestMethods;
using TypeExtensions = Extensions.PrometheusTypes.TypeExtensions;

internal class Program
{
    private static WebAssemblyHost? _app;

    public static ServiceProvider? Services { get; private set; }

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

        MethodInfo? serviceBuilder = null;

        try
        {
            byte[] wasmBytes = await Http.GetByteArrayAsync($"/_framework/Atrium.wasm");
            Console.WriteLine("Adding Atrium: ");
            var ass = Assembly.Load(wasmBytes);
            var extensions = ass.GetType("Atrium.Extensions.BuilderExtensions") 
                ?? throw new InvalidOperationException("Can't find BuilderExtensions, this probably won't work.");
            serviceBuilder = extensions.GetMethods("BuildServices", null, [typeof(IServiceCollection), typeof(List<Type>), typeof(string), typeof(List<Type>), typeof(bool)]).FirstOrDefault()
                ?? throw new InvalidOperationException("Can't find BuilderExtensions.BuildServices, this probably won't work.");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }


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

        serviceBuilder?.Invoke(null, [builder.Services, serviceTypes, null, new List<Type>(), false]);

        builder.Services.RemoveAll<IQueryManager>();
        //builder.Services.AddSingleton<IQueryManager, RemoteManager>();
        builder.Services.AddSingleton(sp => Http);

        builder.Services.AddSingleton<Lazy<WebAssemblyHost?>>(sp => new Lazy<WebAssemblyHost?>(_app));

        builder.RootComponents.Add<WebClient.Components.Routes>("#app");

        Console.WriteLine("Building app with " + builder.Services.Count + " services: " + JsonSerializer.Serialize(builder.Services.Select(t => t.ServiceType.Name).ToList()));

        _app = builder.Build();
        // FUCK DI
        //_ = _app.Services.GetRequiredService<SimpleLogger>();

        // TODO: move this to trusted loader and just grab Atrium?
        var assemblyLoader = _app.Services.GetRequiredService<LazyAssemblyLoader>();

        ITrustProvider? trust = _app.Services.GetRequiredService<ITrustProvider>();

        IComponentActivator? plugin = _app.Services.GetRequiredService<IComponentActivator>();

        var collection = new ServiceCollection();

        try
        {
            Console.WriteLine("Adding Hosting: ");

            var assemblies = await assemblyLoader.LoadAssembliesAsync(["/_framework/Hosting.wasm"]);


            var currents = assemblies
                .SelectMany(TypeExtensions.GetAssTypesSafely)
                .GetServicable()
                .ToList();

            trust.BuildServices(collection, currents);

            // Finalize the provider
            Services = collection.BuildServiceProvider();

            if (plugin is IHasService p
                && p.Services is ICompositeProvider service)
                service.PluginPopin = Services;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }



        


        await _app.RunAsync();
    }
}