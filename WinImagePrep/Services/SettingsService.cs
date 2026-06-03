using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WinImagePrep.Helpers;
using WinImagePrep.Models;

namespace WinImagePrep.Services
{
    /// <summary>
    /// Service for managing application settings with JSON persistence and validation
    /// </summary>
    public class SettingsService : ISettingsService
    {
        private static readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);
        private AppSettings _currentSettings;

        public AppSettings CurrentSettings => _currentSettings;

        public SettingsService()
        {
            _currentSettings = GetDefaultSettings();
        }

        /// <summary>
        /// Loads settings from JSON file or creates defaults
        /// </summary>
        public async Task<AppSettings> LoadSettingsAsync()
        {
            try
            {
                if (!SettingsFileExists())
                {
                    Logger.Info("Settings file not found, creating with defaults");
                    _currentSettings = GetDefaultSettings();
                    await SaveSettingsAsync(_currentSettings);
                    return _currentSettings;
                }

                await _fileLock.WaitAsync();
                try
                {
                    var json = await File.ReadAllTextAsync(AppSettings.SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);

                    if (settings == null || !settings.IsValidPathFormat())
                    {
                        Logger.Warning("Invalid settings file, using defaults");
                        _currentSettings = GetDefaultSettings();
                        return _currentSettings;
                    }

                    _currentSettings = settings;
                    Logger.Info($"Settings loaded: WorkingRoot = {_currentSettings.WorkingRoot}");
                    return _currentSettings;
                }
                finally
                {
                    _fileLock.Release();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load settings: {ex.Message}");
                _currentSettings = GetDefaultSettings();
                return _currentSettings;
            }
        }

        /// <summary>
        /// Saves settings to JSON file with atomic write
        /// </summary>
        public async Task<bool> SaveSettingsAsync(AppSettings settings)
        {
            try
            {
                // Validate before saving
                var validationResult = await ValidateSettingsAsync(settings);
                if (!validationResult.IsValid)
                {
                    Logger.Error($"Cannot save invalid settings: {validationResult.GetErrorMessages()}");
                    return false;
                }

                // Ensure settings directory exists
                var settingsDir = AppSettings.SettingsDirectory;
                if (!Directory.Exists(settingsDir))
                {
                    Directory.CreateDirectory(settingsDir);
                    Logger.Info($"Created settings directory: {settingsDir}");
                }

                await _fileLock.WaitAsync();
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true
                    };

                    var json = JsonSerializer.Serialize(settings, options);

                    // Atomic write: write to temp file, then replace
                    var tempFile = AppSettings.SettingsFilePath + ".tmp";
                    await File.WriteAllTextAsync(tempFile, json);

                    if (File.Exists(AppSettings.SettingsFilePath))
                    {
                        File.Replace(tempFile, AppSettings.SettingsFilePath, null);
                    }
                    else
                    {
                        File.Move(tempFile, AppSettings.SettingsFilePath);
                    }

                    _currentSettings = settings.Clone();
                    Logger.Info($"Settings saved: WorkingRoot = {settings.WorkingRoot}");
                    return true;
                }
                finally
                {
                    _fileLock.Release();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to save settings: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Validates settings against system requirements
        /// </summary>
        public async Task<SettingsValidationResult> ValidateSettingsAsync(AppSettings settings)
        {
            var result = new SettingsValidationResult { IsValid = true };

            try
            {
                // Validate path format
                if (string.IsNullOrWhiteSpace(settings.WorkingRoot))
                {
                    result.AddError("Working folder path cannot be empty");
                    return result;
                }

                if (!settings.IsValidPathFormat())
                {
                    result.AddError("Working folder path is not a valid path format");
                    return result;
                }

                // Get full path
                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(settings.WorkingRoot);
                }
                catch (Exception ex)
                {
                    result.AddError($"Invalid path format: {ex.Message}");
                    return result;
                }

                // Check if path is rooted (absolute)
                if (!Path.IsPathRooted(fullPath))
                {
                    result.AddError("Working folder must be an absolute path (e.g., C:\\Win11ImagePrep)");
                    return result;
                }

                // Get drive info
                DriveInfo? drive = null;
                try
                {
                    var driveLetter = Path.GetPathRoot(fullPath);
                    if (!string.IsNullOrEmpty(driveLetter))
                    {
                        drive = new DriveInfo(driveLetter);
                    }
                }
                catch (Exception ex)
                {
                    result.AddError($"Cannot access drive information: {ex.Message}");
                    return result;
                }

                if (drive == null)
                {
                    result.AddError("Cannot determine drive for the specified path");
                    return result;
                }

                // Validate drive type
                if (drive.DriveType == DriveType.Network)
                {
                    result.AddError("Working folder cannot be on a network share. Please use a local drive.");
                    return result;
                }

                if (drive.DriveType == DriveType.Removable)
                {
                    result.AddError("Working folder cannot be on a removable drive (USB, external drive). Please use a fixed local drive.");
                    return result;
                }

                if (drive.DriveType == DriveType.CDRom)
                {
                    result.AddError("Working folder cannot be on a CD-ROM drive.");
                    return result;
                }

                if (drive.DriveType != DriveType.Fixed)
                {
                    result.AddWarning($"Drive type '{drive.DriveType}' may not be suitable for working folder.");
                }

                // Check if drive is ready
                if (!drive.IsReady)
                {
                    result.AddError($"Drive {drive.Name} is not ready or accessible");
                    return result;
                }

                // Check free space
                long freeSpaceGB = drive.AvailableFreeSpace / (1024 * 1024 * 1024);
                if (freeSpaceGB < AppSettings.MinimumFreeSpaceGB)
                {
                    result.AddError($"Insufficient disk space. Required: {AppSettings.MinimumFreeSpaceGB} GB, Available: {freeSpaceGB} GB");
                    return result;
                }

                if (freeSpaceGB < AppSettings.MinimumFreeSpaceGB + 10)
                {
                    result.AddWarning($"Low disk space. Available: {freeSpaceGB} GB. Recommended: {AppSettings.MinimumFreeSpaceGB + 10} GB or more");
                }

                // Check for OneDrive path
                if (IsOneDrivePath(fullPath))
                {
                    result.AddError("Working folder cannot be inside OneDrive. OneDrive sync will interfere with image operations.");
                    return result;
                }

                // Check if directory exists or can be created
                if (Directory.Exists(fullPath))
                {
                    // Check write permissions
                    if (!await CanWriteToDirectoryAsync(fullPath))
                    {
                        result.AddError("No write permission for the specified folder. Please choose a different location or run as administrator.");
                        return result;
                    }

                    result.AddInfo("Folder exists and is writable");
                }
                else
                {
                    // Check if parent exists and we can create subdirectory
                    var parentDir = Directory.GetParent(fullPath);
                    if (parentDir != null && parentDir.Exists)
                    {
                        if (!await CanWriteToDirectoryAsync(parentDir.FullName))
                        {
                            result.AddError("No permission to create folder in the specified location. Please choose a different location or run as administrator.");
                            return result;
                        }
                        result.AddInfo("Folder will be created");
                    }
                    else
                    {
                        result.AddError("Parent folder does not exist. Please choose an existing parent location.");
                        return result;
                    }
                }

                // Validate LogLevel
                var validLogLevels = new[] { "Minimal", "Information", "Verbose" };
                if (!validLogLevels.Contains(settings.LogLevel))
                {
                    result.AddWarning($"Invalid log level '{settings.LogLevel}'. Using 'Information'.");
                    settings.LogLevel = "Information";
                }

                result.AddInfo($"Validation passed for: {fullPath}");
            }
            catch (Exception ex)
            {
                result.AddError($"Validation error: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Gets default settings
        /// </summary>
        public AppSettings GetDefaultSettings()
        {
            return new AppSettings
            {
                WorkingRoot = AppSettings.DefaultWorkingRoot,
                DeleteTempFilesOnExit = true,
                AutoCleanupMounts = true,
                CheckForUpdates = true,
                FirstRunComplete = false,
                LogLevel = "Information"
            };
        }

        /// <summary>
        /// Resets settings to defaults and saves
        /// </summary>
        public async Task<bool> ResetToDefaultsAsync()
        {
            try
            {
                var defaults = GetDefaultSettings();
                var saved = await SaveSettingsAsync(defaults);
                if (saved)
                {
                    Logger.Info("Settings reset to defaults");
                    _currentSettings = defaults;
                }
                return saved;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to reset settings: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if settings file exists
        /// </summary>
        public bool SettingsFileExists()
        {
            return File.Exists(AppSettings.SettingsFilePath);
        }

        /// <summary>
        /// Creates all required directories for the settings
        /// </summary>
        public async Task<bool> CreateRequiredDirectoriesAsync(AppSettings settings)
        {
            try
            {
                var directories = new[]
                {
                    settings.WorkingRoot,
                    settings.SavedImagesDirectory,
                    settings.LogsDirectory,
                    settings.ConfigDirectory,
                    settings.TempBaseDirectory,
                    settings.Windows11Directory,
                    settings.DriversDirectory,
                    settings.MountDirectory,
                    settings.MountPEDirectory,
                    settings.MountSetupDirectory
                };

                foreach (var dir in directories)
                {
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                        Logger.Info($"Created directory: {dir}");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to create directories: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Reloads settings from disk
        /// </summary>
        public async Task ReloadSettingsAsync()
        {
            await LoadSettingsAsync();
        }

        /// <summary>
        /// Checks if a path is inside OneDrive
        /// </summary>
        private bool IsOneDrivePath(string path)
        {
            try
            {
                var oneDrivePaths = new[]
                {
                    Environment.GetEnvironmentVariable("OneDrive"),
                    Environment.GetEnvironmentVariable("OneDriveCommercial"),
                    Environment.GetEnvironmentVariable("OneDriveConsumer"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive")
                };

                var fullPath = Path.GetFullPath(path);

                foreach (var oneDrivePath in oneDrivePaths)
                {
                    if (!string.IsNullOrEmpty(oneDrivePath))
                    {
                        var fullOneDrivePath = Path.GetFullPath(oneDrivePath);
                        if (fullPath.StartsWith(fullOneDrivePath, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
            catch
            {
                return false; // If we can't determine, assume it's safe
            }
        }

        /// <summary>
        /// Tests write permission to a directory
        /// </summary>
        private async Task<bool> CanWriteToDirectoryAsync(string directoryPath)
        {
            try
            {
                var testFile = Path.Combine(directoryPath, $".write_test_{Guid.NewGuid()}.tmp");
                await File.WriteAllTextAsync(testFile, "test");
                File.Delete(testFile);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
