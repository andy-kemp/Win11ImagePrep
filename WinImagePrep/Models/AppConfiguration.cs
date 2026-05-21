using System;
using System.IO;

namespace WinImagePrep.Models
{
    public class AppConfiguration
    {
        // Base directory for all app files
        public string BaseDirectory { get; set; } = @"C:\WinImagePrep";

        // Persistent storage for final outputs (saved images, logs, config)
        public string SavedImagesDirectory => Path.Combine(BaseDirectory, "SavedImages");
        public string LogsDirectory => Path.Combine(BaseDirectory, "Logs");
        public string ConfigDirectory => Path.Combine(BaseDirectory, "Config");

        // Temporary working directories (cleaned up during operations)
        public string TempBaseDirectory => Path.Combine(BaseDirectory, "Temp");
        public string Windows11Directory => Path.Combine(TempBaseDirectory, "Windows11");
        public string DriversDirectory => Path.Combine(TempBaseDirectory, "Drivers");
        public string MountDirectory => Path.Combine(TempBaseDirectory, "Mount");

        public string MountPEDirectory => Path.Combine(MountDirectory, "PE");
        public string MountSetupDirectory => Path.Combine(MountDirectory, "Setup");

        // Persistent base kept for backward compatibility
        public string PersistentBaseDirectory => BaseDirectory;

        public long RequiredFreeSpaceGB { get; set; } = 25;
        public int MaxLogEntries { get; set; } = 1000;
        public bool EnableDetailedLogging { get; set; } = true;
    }
}
