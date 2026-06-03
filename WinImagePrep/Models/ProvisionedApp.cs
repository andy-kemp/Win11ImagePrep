namespace WinImagePrep.Models
{
    /// <summary>
    /// Represents a provisioned appx package from a Windows image
    /// </summary>
    public class ProvisionedApp
    {
        /// <summary>
        /// Full package name with version (e.g., "Microsoft.WindowsCalculator_11.2210.0.0_neutral_~_8wekyb3d8bbwe")
        /// </summary>
        public string PackageName { get; set; } = string.Empty;

        /// <summary>
        /// Display name (e.g., "Microsoft.WindowsCalculator")
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Package version (e.g., "11.2210.0.0")
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Publisher ID (e.g., "8wekyb3d8bbwe")
        /// </summary>
        public string PublisherId { get; set; } = string.Empty;
    }
}
