using System;
using System.Collections.Generic;
using System.Text;

namespace Hosting.Platforms.Windows;

using Microsoft.Win32;

public static class StartupCheck
{

    public static bool IsScheduledForStartup(string appName)
    {
        string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(runKey);
        if (key != null)
        {
            // Check if the value exists
            return key.GetValue(appName) != null;
        }
        return false;
    }

    public static void SetStartup(string appName, bool enable)
    {
        string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        // Open the key with write access (true)
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(runKey, true);
        if (key != null)
        {
            if (enable)
            {
                // Get the path of your current running executable
                string? appPath = Environment.ProcessPath;
                key.SetValue(appName, $"\"{appPath}\"");
            }
            else
            {
                // Remove the value if it exists
                if (key.GetValue(appName) != null)
                {
                    key.DeleteValue(appName);
                }
            }
        }
    }
}

