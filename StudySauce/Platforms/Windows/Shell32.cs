using System.Runtime.InteropServices;

namespace StudySauce.Platforms.Windows
{

    internal static partial class Shell32
    {
        public const int WM_DROPFILES = 0x0233; // wtf? AI slop? MSGFLT_ALLOW

        [LibraryImport("shell32.dll")]
        public static partial void DragAcceptFiles(IntPtr hwnd, int fAccept);

        [LibraryImport("shell32.dll", EntryPoint = "DragQueryFileW", StringMarshalling = StringMarshalling.Utf16)]
        public static partial uint DragQueryFile(IntPtr hDrop, uint iFile, nint lpszFile, uint cch);

        [LibraryImport("shell32.dll")]
        public static partial void DragFinish(IntPtr hDrop);
    }
}
