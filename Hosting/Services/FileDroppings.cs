#if WINDOWS
using Extensions.PlayfulPlatforms.Windows;
using Microsoft.Maui.Devices;
#endif


#if !BROWSER
using Microsoft.Maui.Storage;
#endif

namespace Hosting.Services;

public partial class FileManager
{


#if WINDOWS

    private static User32.WndProcDelegate? _wndProc; // Keep static to prevent GC
    private static nint _oldWndProc;
    private static bool _isFileDragging;

    internal void InitializeWndProc(Microsoft.Maui.Handlers.IWindowHandler h)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(h.PlatformView);
        // 0x0233 is WM_DROPFILES
        // 0x0049 is WM_COPYGLOBALDATA (Crucial for the "No-Drop" cursor fix)
        User32.AllowDrops(hwnd);
        Shell32.DragAcceptFiles(hwnd, 1);
        _wndProc = MyWndProc; // Simplified assignment
        _oldWndProc = User32.SetWindowLongPtr(hwnd, -4, System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(_wndProc));

    }

    private nint MyWndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == Shell32.WM_DROPFILES) // WM_DROPFILES
        {
            _isFileDragging = false;
            HandleNativeDrop(wParam);
            return nint.Zero;
        }

        if (msg == 0x0040) // WM_NOTIFY
        {
            // Internal WinUI3 / WebView2 notification handling could go here
            User32.AllowDrops(hWnd);
        }

        if (msg == 0x0047) // WM_WINDOWPOSCHANGED or similar "hover" messages
        {
            // This is where you would normally tell the shell to change the cursor
            User32.AllowDrops(hWnd);
        }

        if (msg == Shell32.WM_CAPTURECHANGED)
        {
            if (!_isFileDragging)
            {
                _isFileDragging = true;
                // Notify the front end UI of the upload
                _ = SetDragging(true);
            }
        }

        /*
        if (msg == Shell32.WM_SETCURSOR)
        {
            // High word of lParam is the mouse message that triggered it
            ushort mouseMsg = (ushort)((((ulong)lParam) >> 16) & 0xffff);
    
            if (mouseMsg == Shell32.WM_MOUSEMOVE)
            {
                // If we get SETCURSOR + MOUSEMOVE but our app doesn't have 
                // the mouse 'captured', it's almost certainly an external DRAG.
                if(!_isFileDragging)
                {
                    _isFileDragging = true;
                    using(var scope = _services?.CreateScope())
                    {
                        // Notify the front end UI of the upload
                        var manager = scope?.ServiceProvider.GetRequiredService<IFileManager>();
                        manager?.SetDragging(true);
                    }

                }
            }
        }
        */

        return User32.CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }


    private void HandleNativeDrop(nint hDrop)
    {
        // Get count of dropped files
        uint fileCount = Shell32.DragQueryFile(hDrop, 0xFFFFFFFF, nint.Zero, 0);

        // Notify the front end UI of the upload
        _ = SetDragging(false);

        // TODO: whatever HandleNativeDrop sets up services

        for (uint i = 0; i < fileCount; i++)
        {
            // 1. Get required length (returns length without null terminator)
            uint length = Shell32.DragQueryFile(hDrop, i, nint.Zero, 0) + 1;

            // 2. Allocate buffer and pin it
            char[] buffer = new char[length];
            unsafe
            {
                fixed (char* pBuffer = buffer)
                {
                    // 3. Fill the buffer
                    _ = Shell32.DragQueryFile(hDrop, i, (nint)pBuffer, length);
                }
            }

            // 4. Convert to C# string
            string filePath = new string(buffer).TrimEnd('\0');

            _ = UploadFile(filePath);
        }

        Shell32.DragFinish(hDrop);
    }
#endif


}
