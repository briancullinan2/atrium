using System.Runtime.InteropServices;

namespace Atrium.Platforms.Windows;


public static partial class Shell32
{
    public const int WM_DROPFILES = 0x0233; // wtf? AI slop? MSGFLT_ALLOW
    public const uint WM_SETCURSOR = 0x0020;
    public const uint WM_MOUSEMOVE = 0x0200;
    public const uint WM_CAPTURECHANGED = 0x00D4;

    [LibraryImport("shell32.dll")]
    public static partial void DragAcceptFiles(IntPtr hwnd, int fAccept);

    [LibraryImport("shell32.dll", EntryPoint = "DragQueryFileW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint DragQueryFile(IntPtr hDrop, uint iFile, nint lpszFile, uint cch);

    [LibraryImport("shell32.dll")]
    public static partial void DragFinish(IntPtr hDrop);


    public const uint NOTIFYICON_VERSION_4 = 4;

    [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public unsafe struct NOTIFYICONDATA
    {
        public uint cbSize;
        public nint hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public nint hIcon;

        // Use fixed buffers instead of MarshalAs
        public fixed char szTip[128];

        public uint dwState;
        public uint dwStateMask;

        public fixed char szInfo[256];

        public uint uTimeoutOrVersion;

        public fixed char szInfoTitle[64];

        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

}
