using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using WinImagePrep.Models;

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
        /// Restart the application with administrator privileges, preserving complete application state
        /// </summary>
        /// <param name="state">Application state to preserve</param>
        public static void RestartAsAdministrator(AppState? state = null)
        {
            try
            {
                var args = new StringBuilder();

                // Serialize entire state if provided
                if (state != null)
                {
                    var stateJson = JsonSerializer.Serialize(state);
                    var stateBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(stateJson));
                    args.Append($"--state \"{stateBase64}\"");
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
