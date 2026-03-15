using System.Diagnostics;
using System.Security.Principal;

namespace Atrium.Platforms.Windows
{
    internal class Elevation
    {
        public static void EnsureElevated(string[] args)
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);

            // If not admin, relaunch with the 'runas' verb
            if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath, // Works in .NET 6+
                    Arguments = string.Join(" ", args) + " --elevated-task",
                    UseShellExecute = true,
                    Verb = "runas" // This triggers the UAC prompt
                };

                try
                {
                    Process.Start(startInfo);
                    Environment.Exit(0); // Exit the non-elevated instance
                }
                catch
                {
                    // User clicked 'No' on UAC
                    Console.WriteLine("Administrator rights are required to host the web server.");
                }
            }
        }
    }
}
