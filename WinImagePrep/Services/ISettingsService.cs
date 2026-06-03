using System.Threading.Tasks;
using WinImagePrep.Models;

namespace WinImagePrep.Services
{
    /// <summary>
    /// Service for managing application settings persistence and validation
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// Gets the current settings instance
        /// </summary>
        AppSettings CurrentSettings { get; }

        /// <summary>
        /// Loads settings from the settings file
        /// If file doesn't exist or is invalid, returns default settings
        /// </summary>
        Task<AppSettings> LoadSettingsAsync();

        /// <summary>
        /// Saves settings to the settings file
        /// </summary>
        Task<bool> SaveSettingsAsync(AppSettings settings);

        /// <summary>
        /// Validates settings for correctness and system requirements
        /// </summary>
        Task<SettingsValidationResult> ValidateSettingsAsync(AppSettings settings);

        /// <summary>
        /// Gets default settings
        /// </summary>
        AppSettings GetDefaultSettings();

        /// <summary>
        /// Resets settings to defaults and saves them
        /// </summary>
        Task<bool> ResetToDefaultsAsync();

        /// <summary>
        /// Checks if the settings file exists
        /// </summary>
        bool SettingsFileExists();

        /// <summary>
        /// Creates all required directories for the current settings
        /// </summary>
        Task<bool> CreateRequiredDirectoriesAsync(AppSettings settings);

        /// <summary>
        /// Reloads settings from disk (refreshes CurrentSettings)
        /// </summary>
        Task ReloadSettingsAsync();
    }
}
