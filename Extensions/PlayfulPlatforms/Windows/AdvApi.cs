using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Extensions.PlayfulPlatforms.Windows;

public static partial class AdvApi
{
    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr CreateService(
        IntPtr hSCManager, string lpServiceName, string lpDisplayName,
        uint dwDesiredAccess, uint dwServiceType, uint dwStartType,
        uint dwErrorControl, string lpBinaryPathName, string? lpLoadOrderGroup,
        IntPtr lpdwTagId, string? lpDependencies, string? lpServiceStartName, string? lpPassword);

    private const string Advapi32 = "advapi32.dll";

    [LibraryImport(Advapi32, EntryPoint = "OpenSCManagerW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint OpenSCManager(string lpMachineName, string lpDatabaseName, uint dwDesiredAccess);

    [LibraryImport(Advapi32, EntryPoint = "EnumServicesStatusExW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EnumServicesStatusEx(
        nint hSCManager,
        int infoLevel,
        uint dwServiceType,
        uint dwServiceState,
        nint lpServices,
        uint cbBufSize,
        out uint pcbBytesNeeded,
        out uint lpServicesReturned,
        ref uint lpResumeHandle,
        string pszGroupName);

    [LibraryImport(Advapi32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseServiceHandle(nint hSCObject);

    // Struct for service info
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct ENUM_SERVICE_STATUS_PROCESS
    {
        public nint lpServiceName;
        public nint lpDisplayName;
        // Service Status components follow - we usually only need the names for your check
        public uint dwServiceType;
        public uint dwCurrentState;
        public uint dwControlsAccepted;
        public uint dwWin32ExitCode;
        public uint dwServiceSpecificExitCode;
        public uint dwCheckPoint;
        public uint dwWaitHint;
        public uint dwProcessId;
        public uint dwServiceFlags;
    }


}
