



using System.Net.Http;

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

        SharedRegistry.BuildSharedServiceList(builder.Services);

        builder.Services.RemoveAll<IQueryManager>();
        builder.Services.AddSingleton<IQueryManager, RemoteManager>();

        builder.Services.AddSingleton(sp => new HttpClient
        {
            BaseAddress = new Uri(builder.HostEnvironment.BaseAddress.Trim('/'))
        });

        builder.Services.AddSingleton<Lazy<WebAssemblyHost?>>(sp => new Lazy<WebAssemblyHost?>(_app));
        _app = builder.Build();
        // FUCK DI
        _ = _app.Services.GetRequiredService<SimpleLogger>();


        await _app.RunAsync();
    }
}