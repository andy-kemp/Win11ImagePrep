using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

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
        /// Restart the application with administrator privileges, preserving state
        /// </summary>
        /// <param name="isoPath">Optional ISO path to restore</param>
        /// <param name="driverPaths">Optional driver pack paths to restore</param>
        public static void RestartAsAdministrator(string? isoPath = null, IEnumerable<string>? driverPaths = null)
        {
            try
            {
                var args = new StringBuilder();

                // Add ISO path if provided
                if (!string.IsNullOrEmpty(isoPath))
                {
                    var isoJson = JsonSerializer.Serialize(isoPath);
                    var isoBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(isoJson));
                    args.Append($"--iso \"{isoBase64}\" ");
                }

                // Add driver paths if provided
                if (driverPaths != null && driverPaths.Any())
                {
                    var driversJson = JsonSerializer.Serialize(driverPaths.ToList());
                    var driversBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(driversJson));
                    args.Append($"--drivers \"{driversBase64}\"");
                }

                var processInfo = new ProcessStartInfo
                {
                    FileName = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty,
                    Arguments = args.ToString().Trim(),
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
