
using Atrium.Services;


namespace Atrium;

#if !BROWSER

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();

        // Subscribe to the handler change to capture the native control
        //htmlViewer.HandlerChanged += OnWebViewHandlerChanged;
        var htmlSource = new HtmlWebViewSource
        {
            Html = @"<html><body><h1 id=""title"">Hello from MAUI!</h1></body></html>"
        };
        htmlViewer.Source = htmlSource;
    }


    // lol this may be even funnier than php-babel, or that shit i wrote long before php-babel
    // lol https://github.com/briancullinan2/studysauce3/blob/main/src/Admin/Bundle/Controller/AdminController.php#L1181
    public static void InjectApp(Interfacing.Services.IWindow window)
    {
        App.Bridge?.InvokeAsync(async () =>
        {
            try
            {
                window.document.getElementById("title").innerHTML = "Hello from C# also";

                //(window["title"] as IJsProxy)?.As<Interfacing.Services.IElement>().innerHTML = "Hello from C#";

                window.addEventListener("popstate", (e) => window.postMessage(new
                {
                    id = "Atrium.Services.Navigation.OnPopState",
                    data = JSON.stringify(e.state)
                }, "*"));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        });
    }
    


    protected override void OnHandlerChanged()
    {
        var PlatformView = htmlViewer.Handler?.PlatformView;
#if WINDOWS
        if (PlatformView is Microsoft.UI.Xaml.Controls.WebView2 nativeWebView)
        {
            nativeWebView?.CoreWebView2Initialized += (s, args) =>
            {
                App.Bridge = new WebViewBridge(s.CoreWebView2);
                App.Bridge?.OnDocument += InjectApp;
            };
        }
#elif ANDROID
        if (PlatformView is Android.Webkit.WebView nativeWebView)
        {
            App.Bridge = new WebViewBridge(nativeWebView);
            App.Bridge?.OnDocument += InjectApp;
        }

#elif IOS || MACCATALYST
        if (PlatformView is WebKit.WKWebView wkView)
        {
            App.Bridge = new WebViewBridge(wkView);
            App.Bridge?.OnDocument += InjectApp;
        }
#endif
    }

}

#endif


