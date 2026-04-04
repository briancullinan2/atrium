



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

        builder.Services.AddSingleton<ILocalStore, LocalStore>();
        builder.Services.AddSingleton<SimpleLogger>();
        builder.Services.AddSingleton<CircuitHandler>();
        builder.Services.AddSingleton<IConnectionStateProvider>(sp => sp.GetRequiredService<CircuitHandler>());


        builder.Services.AddSingleton<IFormFactor, FormFactor>();
        builder.Services.AddSingleton<ITitleService, WebClient.Services.TitleService>();
        builder.Services.AddSingleton<IHostingService, HostingService>();
        builder.Services.AddSingleton<IChatService, ChatService>();
        builder.Services.AddSingleton<IFileManager, ClientFileManager>();
        builder.Services.AddSingleton<IAnkiService, AnkiService>();

        builder.Services.AddSingleton(sp => new HttpClient
        {
            BaseAddress = new Uri(builder.HostEnvironment.BaseAddress.Trim('/'))
        });


        builder.Services.RemoveAll<IQueryManager>();
        builder.Services.RemoveAll<IPageManager>();

        builder.Services.AddSingleton<IQueryManager, RemoteManager>();
        builder.Services.AddSingleton<IPageManager, WebClient.Services.PageManager>();

        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped(sp => (AuthService)sp.GetRequiredService<IAuthService>());

        builder.Services.AddDbContextFactory<RemoteStorage>();
        builder.Services.AddDbContextFactory<TestStorage>();
        builder.Services.AddSingleton<ILocalStore, LocalStore>();

        builder.Services.AddSingleton<Lazy<ILocalStore?>>(sp => new Lazy<ILocalStore?>(_app?.Services.GetService<ILocalStore>()));


        builder.Services.AddSingleton<WebAssemblyHost>(sp => (WebAssemblyHost)_app!);
        _app = builder.Build();
        // FUCK DI
        _ = _app.Services.GetRequiredService<SimpleLogger>();


        await _app.RunAsync();
    }
}