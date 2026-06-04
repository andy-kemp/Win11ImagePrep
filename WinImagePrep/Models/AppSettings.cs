using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

namespace WinImagePrep.Models
{
    /// <summary>
    /// Application settings that can be persisted to JSON
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// Root working directory for all application operations
        /// Default: C:\ProgramData\Win11ImagePrep
        /// </summary>
        public string WorkingRoot { get; set; } = @"C:\ProgramData\Win11ImagePrep";

        /// <summary>
        /// Whether to delete temporary files when the application exits
        /// </summary>
        public bool DeleteTempFilesOnExit { get; set; } = true;

        /// <summary>
        /// Whether to automatically cleanup mounted images on errors or exit
        /// </summary>
        public bool AutoCleanupMounts { get; set; } = true;

        /// <summary>
        /// Whether to check for application updates on startup
        /// </summary>
        public bool CheckForUpdates { get; set; } = true;

        /// <summary>
        /// Whether the first-run wizard has been completed
        /// </summary>
        public bool FirstRunComplete { get; set; } = false;

        /// <summary>
        /// Whether the first-run update check has been performed
        /// </summary>
        public bool FirstRunUpdateCheckComplete { get; set; } = false;

        /// <summary>
        /// Logging level: Minimal, Information, Verbose
        /// </summary>
        public string LogLevel { get; set; } = "Information";

        /// <summary>
        /// Whether to remove Windows apps from the image
        /// </summary>
        public bool RemoveWindowsApps { get; set; } = false;

        /// <summary>
        /// List of package names selected for removal (stores only package names, not full WindowsApp objects)
        /// </summary>
        public List<string> SelectedAppsForRemoval { get; set; } = new List<string>();

        /// <summary>
        /// Whether to enable unattended Windows installation
        /// </summary>
        public bool EnableUnattendedInstall { get; set; } = false;

        /// <summary>
        /// Configuration for unattended installation (autounattend.xml generation)
        /// </summary>
        public UnattendedConfig? UnattendedInstallConfig { get; set; }

        /// <summary>
        /// Pending update version string (set during first-run if user accepts update)
        /// </summary>
        public string? PendingUpdateVersion { get; set; }

        // Validation constants
        public const long MinimumFreeSpaceGB = 25;
        public const string DefaultWorkingRoot = @"C:\ProgramData\Win11ImagePrep";
        public const string SettingsFileName = "settings.json";
        public const string SettingsDirectory = @"C:\ProgramData\Win11ImagePrep";

        /// <summary>
        /// Full path to settings file
        /// </summary>
        [JsonIgnore]
        public static string SettingsFilePath => Path.Combine(SettingsDirectory, SettingsFileName);

        // Derived paths (computed from WorkingRoot)

        /// <summary>
        /// Directory for saved/final Windows images
        /// </summary>
        [JsonIgnore]
        public string SavedImagesDirectory => Path.Combine(WorkingRoot, "SavedImages");

        /// <summary>
        /// Directory for application logs
        /// </summary>
        [JsonIgnore]
        public string LogsDirectory => Path.Combine(WorkingRoot, "Logs");

        /// <summary>
        /// Directory for configuration files (legacy)
        /// </summary>
        [JsonIgnore]
        public string ConfigDirectory => Path.Combine(WorkingRoot, "Config");

        /// <summary>
        /// Base directory for temporary working files
        /// </summary>
        [JsonIgnore]
        public string TempBaseDirectory => Path.Combine(WorkingRoot, "Temp");

        /// <summary>
        /// Directory for extracted Windows ISO files
        /// </summary>
        [JsonIgnore]
        public string Windows11Directory => Path.Combine(TempBaseDirectory, "Windows11");

        /// <summary>
        /// Directory for extracted driver files
        /// </summary>
        [JsonIgnore]
        public string DriversDirectory => Path.Combine(TempBaseDirectory, "Drivers");

        /// <summary>
        /// Base directory for WIM mount operations
        /// </summary>
        [JsonIgnore]
        public string MountDirectory => Path.Combine(TempBaseDirectory, "Mount");

        /// <summary>
        /// Mount point for Windows PE
        /// </summary>
        [JsonIgnore]
        public string MountPEDirectory => Path.Combine(MountDirectory, "PE");

        /// <summary>
        /// Mount point for Setup
        /// </summary>
        [JsonIgnore]
        public string MountSetupDirectory => Path.Combine(MountDirectory, "Setup");

        /// <summary>
        /// Creates a deep copy of the settings
        /// </summary>
        public AppSettings Clone()
        {
            return new AppSettings
            {
                WorkingRoot = this.WorkingRoot,
                DeleteTempFilesOnExit = this.DeleteTempFilesOnExit,
                AutoCleanupMounts = this.AutoCleanupMounts,
                CheckForUpdates = this.CheckForUpdates,
                FirstRunComplete = this.FirstRunComplete,
                LogLevel = this.LogLevel,
                RemoveWindowsApps = this.RemoveWindowsApps,
                SelectedAppsForRemoval = new List<string>(this.SelectedAppsForRemoval),
                EnableUnattendedInstall = this.EnableUnattendedInstall,
                UnattendedInstallConfig = this.UnattendedInstallConfig?.Clone()
            };
        }

        /// <summary>
        /// Validates that the working root is a valid path format
        /// </summary>
        public bool IsValidPathFormat()
        {
            if (string.IsNullOrWhiteSpace(WorkingRoot))
                return false;

            try
            {
                // Check if it's a valid path format
                var fullPath = Path.GetFullPath(WorkingRoot);
                return !string.IsNullOrEmpty(fullPath);
            }
            catch
            {
                return false;
            }
        }
    }
}
