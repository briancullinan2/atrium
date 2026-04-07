using System.Diagnostics;
using System.Security.Principal;

namespace Extensions.Platforms.Windows;

internal class Elevation
{
    public static void EnsureElevated(string[] args)
    {
#if WINDOWS
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new(identity);

        // If not admin, relaunch with the 'runas' verb
        if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = Environment.ProcessPath, // Works in .NET 6+
                Arguments = string.Join(" ", args) + " --elevated-task",
                UseShellExecute = true,
                Verb = "runas" // This triggers the UAC prompt
            };

            try
            {
                _ = Process.Start(startInfo);
                Environment.Exit(0); // Exit the non-elevated instance
            }
            catch
            {
                // User clicked 'No' on UAC
                Console.WriteLine("Administrator rights are required to host the web server.");
            }
        }
#endif
    }
}
