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
        // 1. Cancel any ongoing animation
        _animationCts?.Cancel();
        _animationCts = new CancellationTokenSource();
        var token = _animationCts.Token;

        try
        {
            var display = DeviceDisplay.Current.MainDisplayInfo;
            double density = display.Density;

            // Define Start and End states
            double _currentH;
            double _currentW;
            double startWidth = _currentW = SPLASH_WIDTH;
            double startHeight = _currentH = SPLASH_HEIGHT;
            if (App.Current?.Windows.Count > 0)
            {
                startWidth = _currentW = App.Current.Windows[0].Width;
                startHeight = _currentH = App.Current.Windows[0].Height;
            }

            double targetWidth = expanding ? (display.Width / density) * 0.75 : SPLASH_WIDTH;
            double targetHeight = expanding ? (display.Height / density) * 0.75 : SPLASH_HEIGHT;

            // If we are already at the target, don't waste cycles
            if (Math.Abs(startWidth - targetWidth) < 1) return;

            int durationMs = 500;
            int fps = 60;
            int totalFrames = (int)((durationMs / 1000.0) * fps);

            for (int i = 1; i <= totalFrames; i++)
            {
                // Check if we've been interrupted by a new click
                if (token.IsCancellationRequested) return;

                double t = (double)i / totalFrames;

                // Ease-In-Out Spline
                double ease = 3 * Math.Pow(t, 2) - 2 * Math.Pow(t, 3);

                // Interpolate based on wherever the window currently is
                _currentW = startWidth + (targetWidth - startWidth) * ease;
                _currentH = startHeight + (targetHeight - startHeight) * ease;

                double currentX = (display.Width / density - _currentW) / 2;
                double currentY = (display.Height / density - _currentH) / 2;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (App.Current?.Windows.Count > 0)
                    {
                        var window = App.Current.Windows[0];
                        window.X = currentX;
                        window.Y = currentY;
                        window.Width = _currentW;
                        window.Height = _currentH;
                    }
                });

                await Task.Delay(1000 / fps, token);
            }
        }
        catch (TaskCanceledException)
        {
            // Smoothly swallowed: we simply stopped because a new animation started
        }
    }
}