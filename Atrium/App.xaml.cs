using Interfacing.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;

namespace Atrium;

public partial class App : Microsoft.Maui.Controls.Application, IHasWindow, IHasCurrent<Application>
{
    public App()
    {
        InitializeComponent();
    }



    public const int SPLASH_HEIGHT = 350;
    public const int SPLASH_WIDTH = 550;

    // TODO: WINDOWS ONLY?
    protected override Microsoft.Maui.Controls.Window CreateWindow(IActivationState? activationState)
    {
        var window = new Microsoft.Maui.Controls.Window(new MainPage()) { Title = "Atrium" };

        // Get display dimensions
        var displayInfo = DeviceDisplay.Current.MainDisplayInfo;

        // Calculate center (convert pixels to density-independent units)
        window.X = (displayInfo.Width / displayInfo.Density - SPLASH_WIDTH) / 2;
        window.Y = (displayInfo.Height / displayInfo.Density - SPLASH_HEIGHT) / 2;

        window.Width = SPLASH_WIDTH;
        window.Height = SPLASH_HEIGHT;
        return window;
    }

    private CancellationTokenSource? _animationCts;

    public bool IsSplashMode { get; set; }

    public async Task ExpandWindow(bool expanding)
    {
        _animationCts?.Cancel();
        _animationCts = new CancellationTokenSource();
        var token = _animationCts.Token;

        try
        {
            var display = DeviceDisplay.Current.MainDisplayInfo;
            double density = display.Density;

            // 1. Get current state as doubles
            double startWidth = SPLASH_WIDTH;
            double startHeight = SPLASH_HEIGHT;
            if (App.Current?.Windows.Count > 0)
            {
                startWidth = App.Current.Windows[0].Width;
                startHeight = App.Current.Windows[0].Height;
            }

            // 2. Pre-calculate targets and round to nearest whole units
            double targetWidth = Math.Round(expanding ? (display.Width / density) * 0.75 : SPLASH_WIDTH);
            double targetHeight = Math.Round(expanding ? (display.Height / density) * 0.75 : SPLASH_HEIGHT);
            double screenWidthUnits = display.Width / density;
            double screenHeightUnits = display.Height / density;

            if (Math.Abs(startWidth - targetWidth) < 1) return;

            // 3. Lowering FPS to 30 often improves Window Manager stability during resizes
            int durationMs = 300;
            int fps = 60;
            int totalFrames = (int)((durationMs / 1000.0) * fps);

            for (int i = 1; i <= totalFrames; i++)
            {
                if (token.IsCancellationRequested) return;

                double t = (double)i / totalFrames;
                double ease = 3 * Math.Pow(t, 2) - 2 * Math.Pow(t, 3);

                // 4. Calculate everything as doubles first
                double nextW = startWidth + (targetWidth - startWidth) * ease;
                double nextH = startHeight + (targetHeight - startHeight) * ease;

                // 5. ROUND to nearest integer unit to prevent sub-pixel jitter
                // This ensures the title bar buttons don't fight with the rounding engine
                int finalW = (int)Math.Round(nextW);
                int finalH = (int)Math.Round(nextH);
                int finalX = (int)Math.Round((screenWidthUnits - finalW) / 2);
                int finalY = (int)Math.Round((screenHeightUnits - finalH) / 2);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (App.Current?.Windows.Count > 0)
                    {
                        var window = App.Current.Windows[0];
                        // Setting these properties individually can trigger multiple WM_SIZE messages
                        // MAUI on Windows eventually calls SetWindowPos
                        window.X = finalX;
                        window.Y = finalY;
                        window.Width = finalW;
                        window.Height = finalH;
                    }
                });

                await Task.Delay(1000 / fps, token);
            }
        }
        catch (TaskCanceledException) { }
    }
}