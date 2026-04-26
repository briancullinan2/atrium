
#if !BROWSER
using Atrium.Components;
using Atrium.Services;
#endif

namespace Atrium;

// TODO: maybe turn this into an addressable service interface on client soon

#if !BROWSER
public class MauiProgram : IHasCurrent<MauiApp>, IHasProgram
{

    private static readonly MauiApp _myApp = CreateMauiApp();
    public static MauiApp Current => _myApp;

    public static IServiceProvider Service { get => _myApp.Services; }

    private static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        var args = Environment.GetCommandLineArgs();

        if (args.Any(a => a.StartsWith("app://")))
        {
            string protocolData = args.First(a => a.StartsWith("app://"));
            // TODO: Handle deep link / configuration inject here
        }

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        BuilderExtensions.BuildServices(builder.Services, CompositeServiceProvider.BuiltIn, null, null, true);

        //builder.Services.AddSingleton<Lazy<MainLoader?>>(sp => new Lazy<MainLoader?>(() => MainLoader.Current));
        builder.Services.AddSingleton<Lazy<Application?>>(sp => new Lazy<Application?>(() => Microsoft.Maui.Controls.Application.Current));

        // lol, continually breaking patterns with patterns
        builder.Services.AddSingleton<IServiceCollection>(sp => builder.Services);
        builder.Services.AddScoped<Interfacing.Services.IWindow>(sp => App.Bridge!.window);

        var mauiApp = builder.Build();

        return mauiApp;
    }


}
#else


public class MauiProgram : IHasCurrent<object>
{
    public static object? Current => null;
}

#endif
