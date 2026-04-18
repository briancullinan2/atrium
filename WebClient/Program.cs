



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
using System.Runtime.Loader;
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

        /*AssemblyLoadContext.Default.Resolving += (context, assemblyName) =>
        {
            _missing.Add(assemblyName.ToName());
            // You could return null here, or attempt to load it manually
            return null;
        };*/

        var builder = WebAssemblyHostBuilder.CreateDefault(args);

        var Http = new HttpClient
        {
            BaseAddress = new Uri(builder.HostEnvironment.BaseAddress.Trim('/'))
        };

        MethodInfo? serviceBuilder = null;
        //MethodInfo? assemblyReader = null;
        Console.WriteLine("Adding Atrium: ");
        var asses = await RecursiveLoad(Http, "Atrium.wasm");

        try
        {
            var extensions = asses.Select(ass => ass.GetType("Atrium.Extensions.BuilderExtensions")).FirstOrDefault() 
                ?? throw new InvalidOperationException("Can't find BuilderExtensions, this probably won't work.");
            serviceBuilder = extensions.GetMethods("BuildServices", null, [typeof(IServiceCollection), typeof(List<Type>), typeof(string), typeof(List<Type>), typeof(bool)]).FirstOrDefault()
                ?? throw new InvalidOperationException("Can't find BuilderExtensions.BuildServices, this probably won't work.");
            //var extensions2 = ass.GetType("Atrium.Extensions.MetadataReaderExtensions")
            //   ?? throw new InvalidOperationException("Can't find MetadataReaderExtensions, this probably won't work.");
            //assemblyReader = extensions2.GetMethods("GetAssemblyReferences", null, [typeof(byte[])]).FirstOrDefault()
            //    ?? throw new InvalidOperationException("Can't find MetadataReaderExtensions.GetAssemblyReferences, this probably won't work.");
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

        ICompositeProvider? provider = _app.Services.GetRequiredService<ICompositeProvider>();

        var collection = new ServiceCollection();

        try
        {
            var assemblies = await RecursiveLoad(Http, "Hosting.wasm");
            var types = assemblies
                .Where(Extensions.PrometheusTypes.TypeExtensions.IsMine)
                .Concat(mine) // needed for service builder to recognize interfaces
                .SelectMany(TypeExtensions.GetAssTypesSafely);

            Console.WriteLine("where the fuck are my types? " + JsonSerializer.Serialize(types.Select(t => t.Name)));
            var currents = types.GetServicable();

            var checkExisting = currents.Concat(trust.SingleUser.Keys).ToList(); // add this here so it can be checked below
            List<Type> AlreadyMapped = [];
            var scope = _app.Services.CreateScope();
            foreach (var ass in checkExisting)
            {
                try
                {
                    if (scope.ServiceProvider.GetService(ass) is object serve)
                    {
                        if (ass.Extends(typeof(IHasService)))
                            collection.AddSingleton(ass, sp => serve);
                        else
                            collection.AddScoped(ass, sp => serve);

                        AlreadyMapped.Add(ass);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Who the fuck even are you? " + ex);
                }

            }
            Console.WriteLine("Already mapped: " + JsonSerializer.Serialize(AlreadyMapped.Select(t => t.Name)));

            serviceBuilder?.Invoke(null, [collection, currents, null, AlreadyMapped, false]);

            Console.WriteLine("Building app with " + collection.Count + " more services: " + JsonSerializer.Serialize(collection.Select(t => t.ServiceType.Name).ToList()));

            // Finalize the provider
            Services = collection.BuildServiceProvider();

            provider.PluginPopin = Services;

            //provider.Services.GetRequiredService<IPageState>();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }



        


        await _app.RunAsync();
    }


    public static async Task<List<Assembly>> RecursiveLoad(HttpClient Http, string assemblyName, int tries = 6)
    {
        var loaded = AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(TypeExtensions.ToName)
            .ToList();
        List<Assembly> result = [];
        if (tries < 0) return [];
        try
        {
            Console.WriteLine("Adding: " + assemblyName);

            assemblyName = assemblyName.Replace(".dll", ".wasm");

            byte[] wasmBytes = await Http.GetByteArrayAsync($"/_framework/" + assemblyName + (assemblyName.Contains(".wasm") ? "" : ".wasm"));

            var ass = Assembly.Load(wasmBytes);

            Console.WriteLine("Added: " + assemblyName + " - " + ass.GetName().GetPublicKeyToken() + " : " + ass.ImageRuntimeVersion);


            result.Add(ass);
            var referencedAssemblies = ass.GetReferencedAssemblies();
            foreach (var miss in referencedAssemblies)
            {
                try
                {
                    if (loaded.Contains(miss.ToName())) continue;

                    var services = await RecursiveLoad(Http, miss.ToName(), tries - 1);

                    result.AddRange(services);
                }
                catch (Exception ex2)
                {
                    Console.WriteLine(ex2);
                }
            }

            return result;

        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
        return [];
    }
}