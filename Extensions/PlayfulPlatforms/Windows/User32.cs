using System.Runtime.InteropServices;

namespace Atrium.Platforms.Windows;


public static partial class User32
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

}
