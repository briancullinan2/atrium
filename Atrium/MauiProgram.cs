
#if !BROWSER
using Atrium.Services;
#endif

namespace Atrium;

// TODO: maybe turn this into an addressable service interface on client soon

#if !BROWSER
public class MauiProgram : IHasCurrent<MauiApp>
{

    private static readonly MauiApp _myApp = CreateMauiApp();
    public static MauiApp Current => _myApp;

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

        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

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
