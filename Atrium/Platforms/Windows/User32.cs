using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Atrium.Platforms.Windows;


internal static partial class User32
{
    public const uint WM_COPYGLOBALDATA = 0x0049;
    public const uint WM_DROPFILES = 0x0233;
    public const uint WM_COPYDATA = 0x004a;
    public const uint MSGFLT_ALLOW = 1;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ChangeWindowMessageFilterEx(IntPtr hWnd, uint msg, uint action, ref CHANGEFILTERSTRUCT pChangeFilterStruct);


    // Delegate for the Window Procedure
    public delegate nint WndProcDelegate(nint hWnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static partial nint SetWindowLongPtr64(nint hWnd, int nIndex, nint dwNewLong);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static partial int SetWindowLong32(nint hWnd, int nIndex, int dwNewLong);

    // Wrapper to handle 32-bit vs 64-bit automatically
    public static nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong)
    {
        if (IntPtr.Size == 8)
            return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
        else
            return (nint)SetWindowLong32(hWnd, nIndex, (int)dwNewLong);
    }

    [LibraryImport("user32.dll", EntryPoint = "CallWindowProcW")]
    public static partial nint CallWindowProc(nint lpPrevWndFunc, nint hWnd, uint msg, nint wParam, nint lParam);


    [StructLayout(LayoutKind.Sequential)]
    public struct CHANGEFILTERSTRUCT
    {
        public uint cbSize;
        public uint ExtStatus; // MessageFilterInfo
    }

    public static void AllowDrops(IntPtr hwnd)
    {
        CHANGEFILTERSTRUCT cfs = new() { cbSize = (uint)Marshal.SizeOf<CHANGEFILTERSTRUCT>() };
        _ = ChangeWindowMessageFilterEx(hwnd, WM_DROPFILES, MSGFLT_ALLOW, ref cfs);
        _ = ChangeWindowMessageFilterEx(hwnd, WM_COPYGLOBALDATA, MSGFLT_ALLOW, ref cfs);
        _ = ChangeWindowMessageFilterEx(hwnd, WM_COPYDATA, MSGFLT_ALLOW, ref cfs);
    }

    // Modern LibraryImport for ShowWindow
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    public static void FocusProcess(Process proc)
    {
        IntPtr handle = proc.MainWindowHandle;
        if (handle != IntPtr.Zero)
        {
            // Restore in case it's minimized, then bring to front
            ShowWindow(handle, SW_RESTORE);
            SetForegroundWindow(handle);
        }
    }

    
    // --- User32.dll ---
    [LibraryImport("user32.dll", EntryPoint = "DispatchMessageW")]
    public static partial nint DispatchMessage(in MSG lpMsg);

    [LibraryImport("user32.dll", EntryPoint = "TranslateMessage")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool TranslateMessage(in MSG lpMsg);


    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint RegisterWindowMessage(string lpString);

    [LibraryImport("user32.dll")]
    public static partial void PostQuitMessage(int nExitCode);

    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint CreateWindowEx(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x, int y, int nWidth, int nHeight,
        nint hWndParent,
        nint hMenu,
        nint hInstance,
        nint lpParam);

    [LibraryImport("user32.dll", EntryPoint = "LoadImageW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint LoadImage(
        nint hInst,
        string lpszName,
        uint uType,
        int cxDesired, int cyDesired,
        uint fuLoad);

    [LibraryImport("user32.dll")]
    public static partial nint CreatePopupMenu();

    [LibraryImport("user32.dll", EntryPoint = "AppendMenuW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AppendMenu(nint hMenu, uint uFlags, nint uIDNewItem, string lpNewItem);

    [LibraryImport("user32.dll")]
    public static partial uint TrackPopupMenu(
        nint hMenu,
        uint uFlags,
        int x, int y,
        int nReserved,
        nint hWnd,
        nint prcRect);

    [LibraryImport("user32.dll")]
    public static partial int GetMessageW(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetCursorPos(out POINT lpPoint);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(nint hWnd);
    
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public nint hwnd;
        public uint message;
        public nint wParam;
        public nint lParam;
        public uint time;
        public POINT pt;
    }

}
