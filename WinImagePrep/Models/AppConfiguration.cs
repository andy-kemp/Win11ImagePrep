using System;
using System.IO;
using WinImagePrep.Services;

namespace WinImagePrep.Models
{
    /// <summary>
    /// Application configuration that derives paths from AppSettings
    /// Maintains backward compatibility with existing code
    /// </summary>
    public class AppConfiguration
    {
        private readonly AppSettings _settings;

        /// <summary>
        /// Creates AppConfiguration from a settings instance
        /// </summary>
        public AppConfiguration(AppSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// Creates AppConfiguration with default settings (for backward compatibility)
        /// </summary>
        public AppConfiguration() : this(new AppSettings())
        {
        }

        // Base directory for all app files
        public string BaseDirectory => _settings.WorkingRoot;

        // Persistent storage for final outputs (saved images, logs, config)
        public string SavedImagesDirectory => _settings.SavedImagesDirectory;
        public string LogsDirectory => _settings.LogsDirectory;
        public string ConfigDirectory => _settings.ConfigDirectory;

        // Temporary working directories (cleaned up during operations)
        public string TempBaseDirectory => _settings.TempBaseDirectory;
        public string Windows11Directory => _settings.Windows11Directory;
        public string DriversDirectory => _settings.DriversDirectory;
        public string MountDirectory => _settings.MountDirectory;

        public string MountPEDirectory => _settings.MountPEDirectory;
        public string MountSetupDirectory => _settings.MountSetupDirectory;

        // Persistent base kept for backward compatibility
        public string PersistentBaseDirectory => BaseDirectory;

        public long RequiredFreeSpaceGB => AppSettings.MinimumFreeSpaceGB;
        public int MaxLogEntries { get; set; } = 1000;
        public bool EnableDetailedLogging => _settings.LogLevel == "Verbose";

        /// <summary>
        /// Gets the underlying settings instance
        /// </summary>
        public AppSettings Settings => _settings;
    }
}
