using System;
using System.Collections.Generic;

namespace WinImagePrep.Models
{
    /// <summary>
    /// Configuration settings for unattended Windows installation
    /// </summary>
    public class UnattendedConfig
    {
        /// <summary>
        /// Windows edition to install (e.g., "Windows 11 Pro", "Windows 11 Enterprise")
        /// If null or empty, user will be prompted to select edition during install
        /// </summary>
        public string? TargetEdition { get; set; }

        /// <summary>
        /// UI Language (e.g., "en-US", "en-GB")
        /// </summary>
        public string UILanguage { get; set; } = "en-US";

        /// <summary>
        /// Input locale/keyboard layout (e.g., "en-US", "en-GB")
        /// </summary>
        public string InputLocale { get; set; } = "en-US";

        /// <summary>
        /// System locale (e.g., "en-US")
        /// </summary>
        public string SystemLocale { get; set; } = "en-US";

        /// <summary>
        /// User locale (e.g., "en-US")
        /// </summary>
        public string UserLocale { get; set; } = "en-US";

        /// <summary>
        /// Time zone (e.g., "Pacific Standard Time", "GMT Standard Time")
        /// </summary>
        public string TimeZone { get; set; } = "GMT Standard Time";

        /// <summary>
        /// Computer name. If null or empty, Windows will generate one
        /// </summary>
        public string? ComputerName { get; set; }

        /// <summary>
        /// Local administrator account username
        /// </summary>
        public string AdminUsername { get; set; } = "Admin";

        /// <summary>
        /// Local administrator account password (leave empty to prompt during install)
        /// </summary>
        public string AdminPassword { get; set; } = string.Empty;

        /// <summary>
        /// Whether to automatically partition disk 0 (wipes all existing partitions)
        /// </summary>
        public bool AutoPartitionDisk { get; set; } = true;

        /// <summary>
        /// Target disk number (usually 0 for the primary disk)
        /// </summary>
        public int TargetDiskId { get; set; } = 0;

        /// <summary>
        /// Whether to skip OOBE (Out of Box Experience) screens
        /// Note: Set to FALSE for Autopilot devices to allow Autopilot enrollment
        /// </summary>
        public bool SkipOOBE { get; set; } = false;

        /// <summary>
        /// Whether to hide EULA page
        /// </summary>
        public bool HideEULA { get; set; } = true;

        /// <summary>
        /// Whether to hide wireless setup page
        /// Note: Set to FALSE for Autopilot devices
        /// </summary>
        public bool HideWirelessSetup { get; set; } = false;

        /// <summary>
        /// Creates a deep copy of this configuration
        /// </summary>
        public UnattendedConfig Clone()
        {
            return new UnattendedConfig
            {
                TargetEdition = this.TargetEdition,
                UILanguage = this.UILanguage,
                InputLocale = this.InputLocale,
                SystemLocale = this.SystemLocale,
                UserLocale = this.UserLocale,
                TimeZone = this.TimeZone,
                ComputerName = this.ComputerName,
                AdminUsername = this.AdminUsername,
                AdminPassword = this.AdminPassword,
                AutoPartitionDisk = this.AutoPartitionDisk,
                TargetDiskId = this.TargetDiskId,
                SkipOOBE = this.SkipOOBE,
                HideEULA = this.HideEULA,
                HideWirelessSetup = this.HideWirelessSetup
            };
        }

        /// <summary>
        /// Common time zones
        /// </summary>
        public static readonly List<string> CommonTimeZones = new()
        {
            "GMT Standard Time",
            "Pacific Standard Time",
            "Mountain Standard Time",
            "Central Standard Time",
            "Eastern Standard Time",
            "UTC",
            "Central European Standard Time",
            "W. Europe Standard Time",
            "Tokyo Standard Time",
            "AUS Eastern Standard Time"
        };

        /// <summary>
        /// Common locales
        /// </summary>
        public static readonly List<string> CommonLocales = new()
        {
            "en-US",
            "en-GB",
            "en-AU",
            "en-CA",
            "fr-FR",
            "de-DE",
            "es-ES",
            "it-IT",
            "ja-JP",
            "zh-CN"
        };
    }
}
