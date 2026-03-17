#if WINDOWS
using Windows.ApplicationModel.DataTransfer;
#endif

namespace Atrium
{
    public partial class MainPage : ContentPage
    {
        internal static IServiceProvider? _services;

        public MainPage()
        {
            InitializeComponent();
        }

        protected override void OnHandlerChanged()
        {
            base.OnHandlerChanged();
#if WINDOWS
            if (blazorWebView.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.WebView2 webView)
            {
                // Don't access webView.CoreWebView2 here; it's likely null.
                webView.CoreWebView2Initialized += (s, e) =>
                {
                    var core = s.CoreWebView2;

                    // 3. Optional: Prevent the browser from actually opening the file if dropped
                    core.Settings.IsWebMessageEnabled = true;
                    _ = core.AddScriptToExecuteOnDocumentCreatedAsync(
                        "window.addEventListener('dragover', e => e.preventDefault()); " +
                        "window.addEventListener('drop', e => e.preventDefault());");
                };

                // These events exist on the Control level, not the Core level
                webView.DragEnter += (s, e) =>
                {
                    // Check the data package for file paths
                    if (e.DataView.Contains(StandardDataFormats.StorageItems))
                    {
                        // This turns the Red Circle into a Green Plus
                        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
                
                        MainThread.BeginInvokeOnMainThread(() => {
                            using var scope = _services?.CreateScope();
                            var manager = scope?.ServiceProvider.GetRequiredService<FlashCard.Services.IFileManager>();
                            manager?.SetDragging(true);
                        });
                    }
                };

                webView.DragLeave += (s, e) =>
                {
                    MainThread.BeginInvokeOnMainThread(() => {
                        using var scope = _services?.CreateScope();
                        var manager = scope?.ServiceProvider.GetRequiredService<FlashCard.Services.IFileManager>();
                        manager?.SetDragging(false);
                    });
                };

                webView.Drop += async (s, e) =>
                {
                    if (e.DataView.Contains(StandardDataFormats.StorageItems))
                    {
                        var items = await e.DataView.GetStorageItemsAsync();
                        foreach (var item in items)
                        {
                            // Here is your absolute path for the .apkg!
                            string path = item.Path;
                            using var scope = _services?.CreateScope();
                            var manager = scope?.ServiceProvider.GetRequiredService<FlashCard.Services.IFileManager>();
                            manager?.UploadFile(path);
                        }
                    }
                };

            }
#endif
        }
    }
}
