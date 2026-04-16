using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Atrium.Platforms.Windows;

internal static partial class UxTheme
{
    [LibraryImport("uxtheme.dll", EntryPoint = "SetPreferredAppMode")]
    public static partial int SetPreferredAppMode(int appMode);
    // 0 = Default, 1 = AllowDark, 2 = ForceDark, 3 = ForceLight

    [LibraryImport("uxtheme.dll", EntryPoint = "FlushMenuThemes")]
    public static partial void FlushMenuThemes();

    [LibraryImport("uxtheme.dll", EntryPoint = "SetWindowTheme", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int SetWindowTheme(nint hWnd, string pszSubAppName, string pszSubIdList);
}
