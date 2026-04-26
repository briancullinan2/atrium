


using Atrium.Components;
using Atrium.Services;
using System.Linq.Expressions;

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
        _ = TryStarting();
    }


    protected async Task TryStarting()
    {
        try
        {
            await HasDocument.Task;

            string? mainContent = null;
            if ((((Delegate)MainLoader.RenderStaticPageWrapper).InvokeService(Composite) as RenderFragment)?.ToHtml()
                    is Task<string> task)
                mainContent = await task;

            if (mainContent != null)
                App.Bridge?.SetHtml(mainContent);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }


    static readonly TaskCompletionSource<bool> HasDocument = new(TaskCreationOptions.RunContinuationsAsynchronously);


    Interfacing.Services.IWindow? JsWindow { get; set; }
    public ICompositeProvider? Composite { get; private set; }

    protected void OnDocument(Interfacing.Services.IWindow window)
    {
        HasDocument.TrySetResult(true);
        JsWindow = window;
    }


    protected override void OnHandlerChanged()
    {
        Composite = MauiProgram.Current.Services.GetService<ICompositeProvider>();
        var PlatformView = htmlViewer.Handler?.PlatformView;
#if WINDOWS
        if (PlatformView is Microsoft.UI.Xaml.Controls.WebView2 nativeWebView)
        {
            nativeWebView?.CoreWebView2Initialized += (s, args) =>
            {
                App.Bridge = new WebViewBridge(s.CoreWebView2);
                App.Bridge?.OnDocument += OnDocument;
            };
        }
#elif ANDROID
        if (PlatformView is Android.Webkit.WebView nativeWebView)
        {
            App.Bridge = new WebViewBridge(nativeWebView);
            App.Bridge?.OnDocument += OnDocument;
        }

#elif IOS || MACCATALYST
        if (PlatformView is WebKit.WKWebView wkView)
        {
            App.Bridge = new WebViewBridge(wkView);
            App.Bridge?.OnDocument += OnDocument;
        }
#endif
    }

}

#endif


