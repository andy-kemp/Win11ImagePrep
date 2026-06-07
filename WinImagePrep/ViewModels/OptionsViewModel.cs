using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using WinImagePrep.Models;
using WinImagePrep.Services;

namespace WinImagePrep.ViewModels
{
    /// <summary>
    /// ViewModel for the Options/Settings window
    /// </summary>
    public partial class OptionsViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;
        private AppSettings _originalSettings;

        [ObservableProperty]
        private string _workingFolder = string.Empty;

        [ObservableProperty]
        private bool _deleteTempFilesOnExit = true;

        [ObservableProperty]
        private bool _autoCleanupMounts = true;

        [ObservableProperty]
        private bool _checkForUpdates = true;

        [ObservableProperty]
        private string _logLevel = "Information";

        [ObservableProperty]
        private string _validationMessage = string.Empty;

        [ObservableProperty]
        private bool _isValidationError = false;

        [ObservableProperty]
        private bool _isValidationSuccess = false;

        [ObservableProperty]
        private bool _isValidating = false;

        // Derived paths (read-only, computed from WorkingFolder)
        [ObservableProperty]
        private string _isoExtractionPath = string.Empty;

        [ObservableProperty]
        private string _driversPath = string.Empty;

        [ObservableProperty]
        private string _mountPath = string.Empty;

        [ObservableProperty]
        private string _tempPath = string.Empty;

        [ObservableProperty]
        private string _savedImagesPath = string.Empty;

        [ObservableProperty]
        private string _logsPath = string.Empty;

        public OptionsViewModel(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            _originalSettings = settingsService.CurrentSettings.Clone();

            // Load current settings
            LoadSettings();
        }

        /// <summary>
        /// Loads settings into UI properties
        /// </summary>
        private void LoadSettings()
        {
            WorkingFolder = _originalSettings.WorkingRoot;
            DeleteTempFilesOnExit = _originalSettings.DeleteTempFilesOnExit;
            AutoCleanupMounts = _originalSettings.AutoCleanupMounts;
            CheckForUpdates = _originalSettings.CheckForUpdates;
            LogLevel = _originalSettings.LogLevel;

            UpdateDerivedPaths();
        }

        /// <summary>
        /// Updates derived path properties when WorkingFolder changes
        /// </summary>
        partial void OnWorkingFolderChanged(string value)
        {
            UpdateDerivedPaths();
            ClearValidation();
        }

        /// <summary>
        /// Updates all derived path displays
        /// </summary>
        private void UpdateDerivedPaths()
        {
            if (string.IsNullOrWhiteSpace(WorkingFolder))
            {
                IsoExtractionPath = string.Empty;
                DriversPath = string.Empty;
                MountPath = string.Empty;
                TempPath = string.Empty;
                SavedImagesPath = string.Empty;
                LogsPath = string.Empty;
                return;
            }

            try
            {
                var tempSettings = new AppSettings { WorkingRoot = WorkingFolder };
                IsoExtractionPath = tempSettings.Windows11Directory;
                DriversPath = tempSettings.DriversDirectory;
                MountPath = tempSettings.MountDirectory;
                TempPath = tempSettings.TempBaseDirectory;
                SavedImagesPath = tempSettings.SavedImagesDirectory;
                LogsPath = tempSettings.LogsDirectory;
            }
            catch
            {
                // If path is invalid, clear derived paths
                IsoExtractionPath = "(Invalid path)";
                DriversPath = "(Invalid path)";
                MountPath = "(Invalid path)";
                TempPath = "(Invalid path)";
                SavedImagesPath = "(Invalid path)";
                LogsPath = "(Invalid path)";
            }
        }

        /// <summary>
        /// Opens folder browser dialog
        /// </summary>
        [RelayCommand]
        private void Browse()
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select Working Folder Location",
                ShowNewFolderButton = true,
                SelectedPath = Directory.Exists(WorkingFolder) ? WorkingFolder : string.Empty
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                WorkingFolder = dialog.SelectedPath;
            }
        }

        /// <summary>
        /// Validates current settings
        /// </summary>
        [RelayCommand]
        public async Task ValidateAsync()
        {
            IsValidating = true;
            ClearValidation();

            try
            {
                var settings = CreateSettingsFromUI();
                var validationResult = await _settingsService.ValidateSettingsAsync(settings);

                if (validationResult.IsValid)
                {
                    IsValidationSuccess = true;
                    ValidationMessage = "✓ Validation passed\n\n" + validationResult.GetAllMessages();
                }
                else
                {
                    IsValidationError = true;
                    ValidationMessage = validationResult.GetAllMessages();
                }
            }
            catch (Exception ex)
            {
                IsValidationError = true;
                ValidationMessage = $"✗ Validation failed: {ex.Message}";
            }
            finally
            {
                IsValidating = false;
            }
        }

        /// <summary>
        /// Saves settings
        /// </summary>
        [RelayCommand]
        public async Task<bool> SaveAsync()
        {
            IsValidating = true;
            ClearValidation();

            try
            {
                var settings = CreateSettingsFromUI();

                // Validate first
                var validationResult = await _settingsService.ValidateSettingsAsync(settings);
                if (!validationResult.IsValid)
                {
                    IsValidationError = true;
                    ValidationMessage = "Cannot save settings:\n\n" + validationResult.GetAllMessages();
                    return false;
                }

                // Save settings
                var saved = await _settingsService.SaveSettingsAsync(settings);
                if (!saved)
                {
                    IsValidationError = true;
                    ValidationMessage = "✗ Failed to save settings. Check logs for details.";
                    return false;
                }

                // Create directories
                await _settingsService.CreateRequiredDirectoriesAsync(settings);

                IsValidationSuccess = true;
                ValidationMessage = "✓ Settings saved successfully";

                // Update original settings so Cancel works correctly
                _originalSettings = settings.Clone();

                return true;
            }
            catch (Exception ex)
            {
                IsValidationError = true;
                ValidationMessage = $"✗ Error saving settings: {ex.Message}";
                return false;
            }
            finally
            {
                IsValidating = false;
            }
        }

        /// <summary>
        /// Resets to default settings
        /// </summary>
        [RelayCommand]
        private async Task ResetToDefaultsAsync()
        {
            var result = MessageBox.Show(
                "Are you sure you want to reset all settings to their default values?\n\n" +
                $"Working Folder will be set to: {AppSettings.DefaultWorkingRoot}\n\n" +
                "This action can be undone by clicking Cancel without saving.",
                "Reset to Defaults",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            var defaults = _settingsService.GetDefaultSettings();
            WorkingFolder = defaults.WorkingRoot;
            DeleteTempFilesOnExit = defaults.DeleteTempFilesOnExit;
            AutoCleanupMounts = defaults.AutoCleanupMounts;
            CheckForUpdates = defaults.CheckForUpdates;
            LogLevel = defaults.LogLevel;

            ClearValidation();
            ValidationMessage = "ℹ Settings reset to defaults. Click Save to apply.";
        }

        /// <summary>
        /// Resets everything including settings file and triggers first-run on next start
        /// </summary>
        [RelayCommand]
        private async Task ResetEverythingAsync()
        {
            var result = MessageBox.Show(
                "⚠ This will completely reset WinImagePrep:\n\n" +
                "• Delete all application settings\n" +
                "• Restore all defaults\n" +
                "• Show the first-run wizard on next start\n\n" +
                "This is useful for troubleshooting but will require reconfiguration.\n\n" +
                "The application will close after reset. Continue?",
                "Reset Everything",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                // Delete the settings file
                if (File.Exists(AppSettings.SettingsFilePath))
                {
                    File.Delete(AppSettings.SettingsFilePath);
                }

                MessageBox.Show(
                    "All settings have been reset.\n\n" +
                    "The application will now close.\n\n" +
                    "When you restart WinImagePrep, the first-run wizard will appear.",
                    "Reset Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Close the options window
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Find and close the options window
                    foreach (Window window in Application.Current.Windows)
                    {
                        if (window is OptionsWindow)
                        {
                            window.DialogResult = false;
                            window.Close();
                        }
                    }

                    // Shutdown the application
                    Application.Current.Shutdown();
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error resetting settings:\n\n{ex.Message}\n\n" +
                    "You may need to manually delete:\n" +
                    $"{AppSettings.SettingsFilePath}",
                    "Reset Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Cancels changes and reverts to original settings
        /// </summary>
        [RelayCommand]
        private void Cancel()
        {
            LoadSettings();
            ClearValidation();
        }

        /// <summary>
        /// Creates AppSettings from current UI values
        /// </summary>
        private AppSettings CreateSettingsFromUI()
        {
            return new AppSettings
            {
                WorkingRoot = WorkingFolder?.Trim() ?? string.Empty,
                DeleteTempFilesOnExit = DeleteTempFilesOnExit,
                AutoCleanupMounts = AutoCleanupMounts,
                CheckForUpdates = CheckForUpdates,
                FirstRunComplete = _originalSettings.FirstRunComplete, // Preserve FirstRunComplete
                FirstRunUpdateCheckComplete = _originalSettings.FirstRunUpdateCheckComplete, // Preserve FirstRunUpdateCheckComplete
                LogLevel = LogLevel
            };
        }

        /// <summary>
        /// Clears validation messages
        /// </summary>
        private void ClearValidation()
        {
            ValidationMessage = string.Empty;
            IsValidationError = false;
            IsValidationSuccess = false;
        }

        /// <summary>
        /// Gets whether settings have been modified
        /// </summary>
        public bool HasChanges()
        {
            return WorkingFolder != _originalSettings.WorkingRoot ||
                   DeleteTempFilesOnExit != _originalSettings.DeleteTempFilesOnExit ||
                   AutoCleanupMounts != _originalSettings.AutoCleanupMounts ||
                   CheckForUpdates != _originalSettings.CheckForUpdates ||
                   LogLevel != _originalSettings.LogLevel;
        }
    }
}
