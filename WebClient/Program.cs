

internal class Program : IHasCurrent<WebAssemblyHost>
{
    private static WebAssemblyHost? _app;
    private static List<Type>? builtIn;

    public static WebAssemblyHost Current { get => _app ?? throw new InvalidOperationException("Create an app before accessing Program.Current."); }

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

        Type? mainLoader = null;
        MethodInfo? serviceBuilder = null;
        //MethodInfo? assemblyReader = null;
        Console.WriteLine("Adding Atrium: ");
        var asses = await RecursiveLoad(Http, "Atrium.wasm");

        try
        {
            var atrium = asses.FirstOrDefault(ass => ass.ToName() == "Atrium")
                ?? throw new InvalidOperationException("Can't find Atrium, this probably won't work.");

            var extensions = atrium.GetType("Atrium.Extensions.BuilderExtensions")
                ?? throw new InvalidOperationException("Can't find BuilderExtensions, this probably won't work.");
            serviceBuilder = extensions.GetMethods("BuildServices", null, [typeof(IServiceCollection), typeof(List<Type>), typeof(string), typeof(IServiceProviderIsService), typeof(bool)]).FirstOrDefault()
                ?? throw new InvalidOperationException("Can't find BuilderExtensions.BuildServices, this probably won't work.");
            
            mainLoader = atrium.GetType("Atrium.Components.MainLoader")
                ?? throw new InvalidOperationException("Can't find MainLoader, this probably won't work.");

            var componentBuilder = atrium.GetType("Atrium.Services.CompositeServiceProvider")
                ?? throw new InvalidOperationException("Can't find CompositeServiceProvider, this probably won't work.");

            builtIn = componentBuilder.GetProperty("BuiltIn", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null) as List<Type>
                ?? throw new InvalidOperationException("Can't find CompositeServiceProvider.BuiltIn, this probably won't work.");

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
            .GetServiceable()
            .ToList();

        Console.WriteLine("Services: " + JsonSerializer.Serialize(serviceTypes.Select(t => t.Name).ToList()));

        serviceBuilder?.Invoke(null, [builder.Services, builtIn, null, new List<Type>(), false]);

        builder.Services.RemoveAll<IQueryManager>();
        //builder.Services.AddSingleton<IQueryManager, RemoteManager>();
        builder.Services.AddSingleton(sp => Http);

        builder.Services.AddSingleton<Lazy<WebAssemblyHost?>>(sp => new Lazy<WebAssemblyHost?>(_app));

        if(mainLoader != null)
            builder.RootComponents.Add(mainLoader, "#app");

        Console.WriteLine("Building app with " + builder.Services.Count + " services: " + JsonSerializer.Serialize(builder.Services.Select(t => t.ServiceType.Name).ToList()));

        _app = builder.Build();
        
        await _app.RunAsync();
    }



    // TODO: move this to trusted loader to match was rebuild service did
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

            var ass = AppDomain.CurrentDomain.Load(wasmBytes);

            Console.WriteLine("Added: " + assemblyName);


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