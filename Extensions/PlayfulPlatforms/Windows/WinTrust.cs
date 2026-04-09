using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Extensions.PlayfulPlatforms.Windows
{
    public static partial class WinTrust
    {
        [LibraryImport("wintrust.dll", EntryPoint = "WinVerifyTrust", StringMarshalling = StringMarshalling.Utf16)]
        public static partial uint WinVerifyTrust(IntPtr hwnd, Guid pgActionID, IntPtr pWVTData);
        
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WinTrustFileInfo
        {
            public uint cbStruct;
            [MarshalAs(UnmanagedType.LPWStr)] public string pcwszFilePath;
            public IntPtr hFile;
            public IntPtr pgKnownSubject;

            public WinTrustFileInfo(string filePath)
            {
                cbStruct = (uint)Marshal.SizeOf(typeof(WinTrustFileInfo));
                pcwszFilePath = filePath;
                hFile = IntPtr.Zero;
                pgKnownSubject = IntPtr.Zero;
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WinTrustData
        {
            public uint cbStruct;
            public IntPtr pPolicyCallbackData;
            public IntPtr pSIPCallbackData;
            public uint dwUIChoice;
            public uint fdwRevocationChecks;
            public uint dwUnionChoice;
            public IntPtr pFile; // Pointer to WinTrustFileInfo
            public uint dwStateAction;
            public IntPtr hWVTStateData;
            public IntPtr pgActionID;
            public uint dwProvFlags;
            public uint dwWait;

            public WinTrustData(IntPtr fileInfoPtr)
            {
                cbStruct = (uint)Marshal.SizeOf(typeof(WinTrustData));
                pPolicyCallbackData = IntPtr.Zero;
                pSIPCallbackData = IntPtr.Zero;
                dwUIChoice = 2;          // WTD_UI_NONE
                fdwRevocationChecks = 0; // WTD_REVOKE_NONE
                dwUnionChoice = 1;       // WTD_CHOICE_FILE
                pFile = fileInfoPtr;
                dwStateAction = 0;
                hWVTStateData = IntPtr.Zero;
                pgActionID = IntPtr.Zero;
                dwProvFlags = 0x00000040; // WTD_REVOCATION_CHECK_NONE
                dwWait = 0;
            }
        }
    }
}
