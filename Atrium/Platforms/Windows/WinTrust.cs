using System.Runtime.InteropServices;

namespace Atrium.Platforms.Windows;

internal static partial class WinTrust
{
    [LibraryImport("wintrust.dll", EntryPoint = "WinVerifyTrust", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint WinVerifyTrust(IntPtr hwnd, Guid pgActionID, IntPtr pWVTData);
    
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WinTrustFileInfo(string filePath)
    {
        public uint cbStruct = (uint)Marshal.SizeOf<WinTrustFileInfo>();
        [MarshalAs(UnmanagedType.LPWStr)] public string pcwszFilePath = filePath;
        public IntPtr hFile = IntPtr.Zero;
        public IntPtr pgKnownSubject = IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WinTrustData(IntPtr fileInfoPtr)
    {
        public uint cbStruct = (uint)Marshal.SizeOf<WinTrustData>();
        public IntPtr pPolicyCallbackData = IntPtr.Zero;
        public IntPtr pSIPCallbackData = IntPtr.Zero;
        public uint dwUIChoice = 2;
        public uint fdwRevocationChecks = 0;
        public uint dwUnionChoice = 1;
        public IntPtr pFile = fileInfoPtr; // Pointer to WinTrustFileInfo
        public uint dwStateAction = 0;
        public IntPtr hWVTStateData = IntPtr.Zero;
        public IntPtr pgActionID = IntPtr.Zero;
        public uint dwProvFlags = 0x00000040;
        public uint dwWait = 0;
    }
}
