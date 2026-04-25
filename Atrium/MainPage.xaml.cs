


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
    }


    static TaskCompletionSource<bool> HasDocument = new(TaskCreationOptions.RunContinuationsAsynchronously);

    static List<Task> StartupTasks
    {
        get => [
            HasDocument.Task,
            ((RenderFragment)((Delegate)MainLoader.RenderStaticPageWrapper).InvokeService(MauiProgram.Current.Services.GetService<ICompositeProvider>())).ToHtml()
        ];
    }


    Interfacing.Services.IWindow? JsWindow { get; set; }
    protected void OnDocument(Interfacing.Services.IWindow window)
    {
        HasDocument.TrySetResult(true);
        JsWindow = window;
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


