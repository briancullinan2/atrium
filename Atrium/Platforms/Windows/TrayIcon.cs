using Atrium.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Atrium.Platforms.Windows;

internal static class TrayIcon
{
    private const uint NIF_GUID = 0x00000020;
    private const uint NIM_MODIFY = 0x00000001;

    public static unsafe void AddIcon(nint hwnd, nint hIcon, string tooltip)
    {
        var nid = new Shell32.NOTIFYICONDATA
        {
            cbSize = (uint)sizeof(Shell32.NOTIFYICONDATA),
            hWnd = hwnd,
            uID = 1,
            uFlags = 0x00000001 | 0x00000002 | 0x00000004, // NIF_MESSAGE | NIF_ICON | NIF_TIP
            uCallbackMessage = 0x0400 + 1,
            hIcon = hIcon
        };

        // Direct copy to the fixed buffer using the pointer directly
        if (!string.IsNullOrEmpty(tooltip))
        {
            fixed (char* ptr = tooltip)
            {
                // We use nid.szTip directly as it decays to a char* in unsafe context
                int length = Math.Min(tooltip.Length, 127);
                Buffer.MemoryCopy(ptr, nid.szTip, 128 * sizeof(char), length * sizeof(char));
                nid.szTip[length] = '\0'; // Ensure null termination
            }
        }

        Shell32.Shell_NotifyIcon(0, ref nid); // NIM_ADD = 0
    }

    // Helper to handle the span copy
    private static ReadOnlySpan<char> AsSpan(string s) => s.AsSpan();

    public static unsafe void StopBlip(nint hwnd)
    {
        var nid = new Shell32.NOTIFYICONDATA
        {
            cbSize = (uint)sizeof(Shell32.NOTIFYICONDATA),
            hWnd = hwnd,
            uID = 1 // Must match the uID used in RunBlip
        };

        // NIM_DELETE = 2
        Shell32.Shell_NotifyIcon(2, ref nid);

        // Also a good idea to destroy the hidden window
        // PInvoke.DestroyWindow(hwnd); 
    }

    public static unsafe bool IsTrayIconRegisteredByGuid(Guid? myAppGuid = null)
    {
        var nid = new Shell32.NOTIFYICONDATA
        {
            cbSize = (uint)sizeof(Shell32.NOTIFYICONDATA),

            // We tell the shell: "Ignore the HWND, use the GUID instead"
            uFlags = NIF_GUID,
            guidItem = myAppGuid ?? BlipAppGuid ?? new Guid()
        };

        // NIM_MODIFY (1) will return true only if this specific GUID 
        // is currently sitting in the system tray.
        return Shell32.Shell_NotifyIcon(NIM_MODIFY, ref nid);
    }

    public static unsafe bool IsTrayIconRegistered(nint hwnd, uint uid)
    {
        var nid = new Shell32.NOTIFYICONDATA
        {
            cbSize = (uint)sizeof(Shell32.NOTIFYICONDATA),
            hWnd = hwnd,
            uID = uid
        };

        // NIM_MODIFY = 1
        // If the Shell doesn't recognize the hWnd/uID combo, it returns false immediately.
        return Shell32.Shell_NotifyIcon(1, ref nid);
    }

    private static readonly Guid? BlipAppGuid = new("4A9C1D3E-8B2F-4E9A-A5D1-C67B80F42C91");

    public static unsafe Shell32.NOTIFYICONDATA? RunBlip(string iconPath, string tooltip, nint? nativeHwnd)
    {
        // 1. Create the hidden host window
        // We use "Static" which is a built-in system class to avoid RegisterClass overhead
        nint hwnd = nativeHwnd ?? User32.CreateWindowEx(
            0,
            "Static",
            "BlazorTrayHost",
            0x80000000, // WS_POPUP
            0, 0, 0, 0,
            nint.Zero, nint.Zero, nint.Zero, nint.Zero);

        if (hwnd == nint.Zero) return null;

        // 2. Load your rasterized .ico from your pipeline
        nint hIcon = User32.LoadImage(
            nint.Zero,
            iconPath,
            1, // IMAGE_ICON
            0, 0,
            0x00000010 | 0x00000020); // LR_LOADFROMFILE | LR_DEFAULTSIZE

        // 3. Register the Tray Icon
        Shell32.NOTIFYICONDATA nid;
        if (BlipAppGuid != null)
        {
            if (IsTrayIconRegisteredByGuid(BlipAppGuid.Value))
                return null;

            nid = new Shell32.NOTIFYICONDATA
            {
                cbSize = (uint)sizeof(Shell32.NOTIFYICONDATA),
                hWnd = hwnd,
                uFlags = NIF_GUID | 0x01 | 0x02 | 0x04, // NIF_GUID | NIF_MESSAGE | NIF_ICON
                guidItem = BlipAppGuid.Value,
                hIcon = hIcon,
                uCallbackMessage = 0x0400 + 1
            };
        }
        else
        {
            nid = new()
            {
                cbSize = (uint)sizeof(Shell32.NOTIFYICONDATA),
                hWnd = hwnd,
                uID = 1,
                uFlags = 0x01 | 0x02 | 0x04, // NIF_MESSAGE | NIF_ICON | NIF_TIP
                uCallbackMessage = 0x0400 + 1, // WM_USER + 1
                hIcon = hIcon
            };
        }

        var destSpan = MemoryMarshal.CreateSpan(ref nid.szTip[0], 128);
        tooltip.AsSpan().CopyTo(destSpan);
        if (tooltip.Length < 128) destSpan[tooltip.Length] = '\0';
        Shell32.Shell_NotifyIcon(0, ref nid); // NIM_ADD
        nid.uTimeoutOrVersion = Shell32.NOTIFYICON_VERSION_4;
        Shell32.Shell_NotifyIcon(0x00000004, ref nid);

        return nid;

#if false
        // 4. The Message Loop (The "Engine")
        // This keeps the thread alive and handles the Right-Click events
        while (User32.GetMessageW(out User32.MSG msg, hwnd, 0, 0) != 0)
        {
            // Handle Tray callback
           
            else if (msg.message == 0x0113  // WM_TIMER
                || msg.message == 0x0020  // WM_SETCURSOR
                || msg.message == 0x0084  // WM_NCHITTEST
                || msg.message == 0x000F  // WM_PAINT
                || msg.message == 0x0111)   // WM_COMMAND (usually menu results)
            {
                // Now you'll only see the Shell messages and window creation/lifecycle
                //Console.WriteLine($"INTERESTING MSG: 0x{msg.message:X4} | wParam: 0x{msg.wParam:X8} | lParam: 0x{msg.lParam:X8}");
            }
            else 
            

            // Standard dispatching
            // Translate/Dispatch are usually needed for keyboard/text, 
            // but it's good practice to keep the pump standard.
            User32.TranslateMessage(msg);
            User32.DispatchMessage(msg);
        }
#endif
    }

    internal static void ShowMenu(nint hwnd)
    {
        nint hMenu = User32.CreatePopupMenu();
        User32.AppendMenu(hMenu, 0x0000, 1001, "Open");
        User32.AppendMenu(hMenu, 0x0000, 1002, "Exit");

        User32.GetCursorPos(out var pt);

        // Critical: This makes the menu close when you click away
        User32.SetForegroundWindow(hwnd);

        uint selection = User32.TrackPopupMenu(
            hMenu,
            0x0100 | 0x0002, // TPM_RETURNCMD | TPM_RIGHTBUTTON
            pt.X, pt.Y, 0, hwnd, nint.Zero);

        if (selection == 1001)
        {
            Process current = Process.GetCurrentProcess();

            Process? other = Process.GetProcessesByName(current.ProcessName)
                .FirstOrDefault();

            if (other != null)
            {
                User32.ShowWindow(WindowManager.hwnd, 1);

                User32.FocusProcess(other);
            }
        }
        else if (selection == 1002)
        {
            Environment.Exit(0);
        }
    }
}

