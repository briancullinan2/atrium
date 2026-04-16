#if WINDOWS
using Atrium.Platforms.Windows;
#endif
using Interfacing.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;

namespace Atrium.Services;

internal class WindowManager(Lazy<Application?>? App = null) : IWindowManager
{

    // TODO: make this an includeable module that triggers

    public const int SPLASH_HEIGHT = 350;
    public const int SPLASH_WIDTH = 550;

    // TODO: WINDOWS ONLY?
    public static Microsoft.Maui.Controls.Window CreateWindow()
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


    public async Task<nint> GetWindowHwnd()
    {
        var tcs = new TaskCompletionSource<nint>();
#if WINDOWS

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                foreach (var window in App?.Value?.Windows ?? [])
                {
                    // 1. Get the platform window (Microsoft.UI.Xaml.Window)
                    var platformWindow = window.Handler?.PlatformView as Microsoft.UI.Xaml.Window;

                    if (platformWindow != null)
                    {
                        // 2. Extract the HWND
                        nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(platformWindow);

                        // 3. Hook your WndProc or Tray logic here
                        if (hwnd != nint.Zero)
                        {
                            tcs.SetResult(hwnd);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }

        });

#else
    MainThread.BeginInvokeOnMainThread(() =>
    {
        try
        {
            foreach (var window in App?.Value?.Windows ?? [])
            {
            }
        } catch {}
        tcs.SetResult(0);
    });
#endif
        return await tcs.Task;
    }



    private async Task<Tuple<double, double, double, double, double>> GetWindowSizeAsync()
    {
        var tcs = new TaskCompletionSource<Tuple<double, double, double, double, double>>();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                var display = DeviceDisplay.Current.MainDisplayInfo;
                double density = display.Density;

                double width = SPLASH_WIDTH;
                double height = SPLASH_HEIGHT;

                if (App?.Value?.Windows.Count > 0)
                {
                    width = App.Value.Windows[0].Width;
                    height = App.Value.Windows[0].Height;
                }

                // Signal that we are done and pass the data back
                tcs.SetResult(Tuple.Create(display.Width, display.Height, width, height, density));
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        // The background thread pauses here until tcs.SetResult is called
        return await tcs.Task;
    }

    public async Task UpdateTitle(string? title)
    {

        MainThread.BeginInvokeOnMainThread(() =>
        {
            foreach (var window in App?.Value?.Windows ?? [])
            {
                window.Title = title; // This is now safe
            }
        });
    }


    public async Task ExpandWindow(bool expanding)
    {
        _animationCts?.Cancel();
        _animationCts = new CancellationTokenSource();
        var token = _animationCts.Token;

        try
        {
            var (displayWidth, displayHeight, startWidth, startHeight, density) = await GetWindowSizeAsync();


            // 2. Pre-calculate targets and round to nearest whole units
            double targetWidth = Math.Round(expanding ? (displayWidth / density) * 0.75 : SPLASH_WIDTH);
            double targetHeight = Math.Round(expanding ? (displayHeight / density) * 0.75 : SPLASH_HEIGHT);
            double screenWidthUnits = displayWidth / density;
            double screenHeightUnits = displayHeight / density;

            if (Math.Abs(startWidth - targetWidth) < 1) return;

            // 3. Lowering FPS to 30 often improves Window Manager stability during resizes
            int durationMs = 300;
            int fps = 50;
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
                    if (App?.Value?.Windows.Count > 0)
                    {
                        var window = App.Value.Windows[0];
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


    public async Task CreateTrayIcon()
    {
#if WINDOWS
        if (hwnd == 0)
            hwnd = await GetWindowHwnd();

        //UxTheme.SetPreferredAppMode(2); // Force Dark
        UxTheme.SetWindowTheme(hwnd, "DarkMode_Explorer", null);
        //UxTheme.FlushMenuThemes();


        nid = TrayIcon.RunBlip(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "triangle.ico"),
            "Atrium", hwnd);
        if (_wndProc == null)
        {
            InitializeWndProc(hwnd);
        }
#endif
    }

#if WINDOWS

    private static nint _oldWndProc;
    private const uint WM_CLOSE = 0x0010;

    protected static unsafe nint MyWndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == taskbarCreatedMsg && nid.HasValue)
        {
            var nid = WindowManager.nid.Value;
            // The taskbar just rebooted; re-register your icon here!
            Shell32.Shell_NotifyIcon(0, ref nid);
            nid.uTimeoutOrVersion = Shell32.NOTIFYICON_VERSION_4;
            Shell32.Shell_NotifyIcon(0x00000004, ref nid);
        }

        if (msg == 0x0400 + 1 && hwnd != 0)
        {
            uint mouseEvent = (uint)(lParam & 0xFFFF);

            if (mouseEvent == 0x0205) // WM_RBUTTONUP
            {
                TrayIcon.ShowMenu(hwnd);
            }
            else if (mouseEvent == 0x0203) // WM_LBUTTONDBLCLK (Optional 'Open')
            {
                // Handle double click
            }
        }
        if (msg == WM_CLOSE)
        {
            // If the tray icon is active, we hide instead of close
            if (TrayIcon.IsTrayIconRegisteredByGuid())
            {
                // Hide the window (SW_HIDE = 0)
                User32.ShowWindow(hWnd, 0);

                // Return 0 to "cancel" the close event
                return 0;
            }
        }
        if (msg == 0x0011) // WM_QUERYENDSESSION
        {
            // Clean up tray icon before user logs out
            TrayIcon.StopBlip(hwnd);
            return 1;
        }
        return User32.CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);

    }

    internal static User32.WndProcDelegate? _wndProc; // Keep static to prevent GC
    internal static Shell32.NOTIFYICONDATA? nid; // TODO: more than one tray icon?
    internal static nint hwnd = IntPtr.Zero;
    internal static uint taskbarCreatedMsg;
#endif

    internal static void InitializeWndProc(nint h)
    {
#if WINDOWS
        if (hwnd == 0) return;
        //hwnd = WinRT.Interop.WindowNative.GetWindowHandle(h.PlatformView);
        // 0x0233 is WM_DROPFILES
        // 0x0049 is WM_COPYGLOBALDATA (Crucial for the "No-Drop" cursor fix)
        User32.AllowDrops(hwnd);
        Shell32.DragAcceptFiles(hwnd, 1);
        _wndProc = MyWndProc; // Simplified assignment
        _oldWndProc = User32.SetWindowLongPtr(hwnd, -4, System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(_wndProc));
        taskbarCreatedMsg = User32.RegisterWindowMessage("TaskbarCreated");
#endif

    }

}
