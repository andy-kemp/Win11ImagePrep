using System;
using System.Diagnostics;
using System.Security.Principal;

namespace WinImagePrep.Helpers
{
    public static class AdminHelper
    {
        /// <summary>
        /// Check if the current process is running with administrator privileges
        /// </summary>
        public static bool IsRunningAsAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Restart the application with administrator privileges
        /// </summary>
        public static void RestartAsAdministrator()
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty,
                    UseShellExecute = true,
                    Verb = "runas" // Run as administrator
                };

                Process.Start(processInfo);
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to restart as administrator", ex);
            }
        }
    }
}
