using System.Collections.Generic;

namespace WinImagePrep.Models
{
    /// <summary>
    /// Application state for preservation across admin elevation restart
    /// </summary>
    public class AppState
    {
        /// <summary>
        /// Selected ISO file path
        /// </summary>
        public string? IsoPath { get; set; }

        /// <summary>
        /// ISO volume label
        /// </summary>
        public string? IsoVolumeLabel { get; set; }

        /// <summary>
        /// Driver pack paths
        /// </summary>
        public List<string>? DriverPaths { get; set; }

        /// <summary>
        /// Whether Windows app removal is enabled
        /// </summary>
        public bool RemoveWindowsApps { get; set; }

        /// <summary>
        /// Package names of selected apps to remove
        /// </summary>
        public List<string>? SelectedAppsForRemoval { get; set; }

        /// <summary>
        /// Whether unattended install is enabled
        /// </summary>
        public bool EnableUnattendedInstall { get; set; }

        /// <summary>
        /// Selected Windows edition indices for preparation
        /// </summary>
        public List<int>? SelectedEditions { get; set; }
    }
}
