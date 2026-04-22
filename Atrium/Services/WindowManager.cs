#if WINDOWS
using Atrium.Platforms.Windows;
#endif

#if !BROWSER
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Devices;
#endif

using System.ComponentModel.DataAnnotations;
using static System.Net.WebRequestMethods;

namespace Atrium.Services;

internal class WindowManager(
    ICompositeProvider? Composite
#if !BROWSER
    , Lazy<Application?>? App = null
#endif
) : IWindowManager, ITitleService, IHasName
{

    // TODO: make this an includeable module that triggers

    public const int SPLASH_HEIGHT = 350;
    public const int SPLASH_WIDTH = 550;

    // TODO: WINDOWS ONLY?
#if !BROWSER
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
#endif

    private CancellationTokenSource? _animationCts;

    public bool IsSplashMode { get; set; }


    public Task<nint> GetWindowHwnd()
    {
            if (tcs == null || tcs.Task.IsCompleted)
                tcs = new TaskCompletionSource<nint>();
            else
                return tcs.Task;

#if !BROWSER
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
#endif
        return tcs.Task;
    }



    private Task<Tuple<double, double, double, double, double>> GetWindowSizeAsync()
    {
        if(sizeTcs == null || sizeTcs.Task.IsCompleted)
            sizeTcs = new TaskCompletionSource<Tuple<double, double, double, double, double>>();
        else
            return sizeTcs.Task;

#if !BROWSER
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
                sizeTcs.SetResult(Tuple.Create(display.Width, display.Height, width, height, density));
            }
            catch (Exception ex)
            {
                sizeTcs.SetException(ex);
            }
        });
#endif

        // The background thread pauses here until tcs.SetResult is called
        return sizeTcs.Task;
    }


    public virtual async Task<string?> SetDefaultTitle(Type? controlType)
    {
        var title = controlType?.GetCustomAttributes<DisplayAttribute>()
            .FirstOrDefault();
        if (title is DisplayAttribute attr)
        {
            return await UpdateTitle(attr.Name);
            //await InvokeAsync(StateHasChanged);
        }
        return await UpdateTitle(null);
    }


    public virtual async Task<string?> UpdateTitle(string? title)
    {
        if (title == null)
        {
            _title = AppName;
        }
        else
        {
            _title = title;
        }

        var Form = Composite?.GetService<IFormFactor>();
        // TODO: just saw an error javascript cannot be set when statically rendering
        if (Form?.IsWebContext == true)
            // TODO: set title in header
            return _title + " - " + AppName;

        // shouldn't end up here, but interesting service patterns emerge
#if !BROWSER
        MainThread.BeginInvokeOnMainThread(() =>
        {
            foreach (var window in App?.Value?.Windows ?? [])
            {
                window.Title = _title + " - " + AppName; // This is now safe
            }
        });
#else
        //var Page = Composite?.GetService<IPageState>();
        var Rendered = Composite?.GetService<IRenderState>();
        if (Rendered == null) return _title + " - " + AppName;
        await Rendered.EnsureInitialized();
        // thats craaaaazyy that microsoft thinks anything about this framework is an improvement
        await ((IJSRuntime)Rendered.Runtime)!.InvokeVoidAsync("eval", "document.title = " 
            + JsonSerializer.Serialize(_title + " - " + AppName));
#endif
        return _title + " - " + AppName;
    }

    internal static string? _title;
    private TaskCompletionSource<nint>? tcs = null;
    private TaskCompletionSource<Tuple<double, double, double, double, double>>? sizeTcs = null;

    event Action<string?>? InternalTitleChanged;
    public event Action<string?>? OnTitleChanged
    {
        add
        {
            if (value == null) return;
            InternalTitleChanged += value;
            if (_title != null)
                value?.Invoke(_title);
        }
        remove
        {
            if (value == null) return;
            InternalTitleChanged -= value;
        }
    }




    public static string? AppName
    {
        get => Assembly.GetEntryAssembly()?
            .GetCustomAttributes<AssemblyProductAttribute>().FirstOrDefault()
            ?.Product;
    }



    public async Task ExpandWindow(bool expanding)
    {
        if (IsSplashMode) return;
        

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
            //int durationMs = 100;
            int fps = 50;
            int totalFrames = 1; // (int)((durationMs / 1000.0) * fps);

            for (int i = 1; i <= totalFrames; i++)
            {
                await Task.Delay(1000 / fps, token);

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

                // can clicking allow window pos change like the old days?
#if !BROWSER
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if(App?.Value != Application.Current)
                    {
                        Console.WriteLine("wtf?");
                    }
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
#endif
            }
        }
        catch (TaskCanceledException) { }
    }


    public async Task<bool?> CheckInstalled()
    {
#if WINDOWS
        return TrayIcon.IsTrayIconRegisteredByGuid();
#else
        return false; // TODO: how to do the persistent notification tray on android for web server like ip webcam?
#endif
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
