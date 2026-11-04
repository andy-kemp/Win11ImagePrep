using System;
using System.IO;
using Microsoft.Win32;

namespace WinImagePrep.Helpers
{
    /// <summary>
    /// What Win11ImagePrep_Setup.exe records in the registry. The installer writes
    /// HKLM\SOFTWARE\Andy Kemp Consulting\Win11 Image Prep (InstallLocation, InstalledVersion, ManagedUpdates)
    /// and the Windows uninstall entry. A copy run from anywhere else (Downloads, say) has none of this.
    /// </summary>
    public static class InstallRegistry
    {
        public const string AppKey = @"SOFTWARE\Andy Kemp Consulting\Win11 Image Prep";
        public const string PolicyKey = @"SOFTWARE\Policies\Andy Kemp Consulting\Win11 Image Prep";
        public const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{669DE22E-E42E-4D1B-AE68-1A81A43BCF12}_is1";

        /// <summary>
        /// Updates are deployed by the organisation (Intune, say), so the app doesn't check for or install them.
        /// Set by the installer's /MANAGEDUPDATES=1, or ManagedUpdates=1 under the policy key.
        /// </summary>
        public static bool UpdatesManagedByOrganisation =>
            ReadDword(PolicyKey, "ManagedUpdates") == 1 || ReadDword(AppKey, "ManagedUpdates") == 1;

        /// <summary>The folder the installer put the app in, or null if it wasn't installed by setup.</summary>
        public static string? InstallLocation
        {
            get
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(AppKey);
                    return key?.GetValue("InstallLocation") as string;
                }
                catch { return null; }
            }
        }

        /// <summary>True if this EXE is the copy the installer put in place.</summary>
        public static bool IsInstalledCopy(string exePath)
        {
            var location = InstallLocation;
            if (string.IsNullOrEmpty(location)) return false;
            try
            {
                return string.Equals(Path.GetFullPath(Path.GetDirectoryName(exePath)!).TrimEnd('\\'),
                                     Path.GetFullPath(location).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        /// <summary>
        /// After an in-app update of an installed copy, record the new version in the uninstall entry (Settings >
        /// Apps, and what Intune reads) so it matches the EXE. Needs administrator rights; ignored otherwise.
        /// </summary>
        public static void RecordInstalledVersion(string exePath, string version)
        {
            if (!IsInstalledCopy(exePath)) return;
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(AppKey, writable: true))
                    key?.SetValue("InstalledVersion", version);
                using (var key = Registry.LocalMachine.OpenSubKey(UninstallKey, writable: true))
                {
                    key?.SetValue("DisplayVersion", version);
                    key?.SetValue("DisplayName", "Win11 Image Prep");
                }
                Logger.Info($"Recorded installed version {version}");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Could not record the installed version: {ex.Message}");
            }
        }

        private static int? ReadDword(string subKey, string name)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(subKey);
                return key?.GetValue(name) is int v ? v : null;
            }
            catch { return null; }
        }
    }
}
