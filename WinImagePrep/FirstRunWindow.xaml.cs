using System;
using System.Windows;
using WinImagePrep.Helpers;
using WinImagePrep.Services;
using WinImagePrep.ViewModels;

namespace WinImagePrep
{
    /// <summary>
    /// First-run welcome window
    /// </summary>
    public partial class FirstRunWindow : Window
    {
        private readonly FirstRunViewModel _viewModel;
        private readonly ISettingsService _settingsService;

        /// <summary>
        /// Indicates whether the user completed first-run successfully
        /// </summary>
        public bool UserAccepted { get; private set; }

        public FirstRunWindow(ISettingsService settingsService)
        {
            try
            {
                Logger.Info("FirstRunWindow: Initializing...");
                InitializeComponent();

                _settingsService = settingsService;
                _viewModel = new FirstRunViewModel(settingsService);
                DataContext = _viewModel;

                // Ensure window is visible and activated
                Loaded += (s, e) => 
                {
                    Logger.Info("FirstRunWindow: Loaded event fired");
                    Activate();
                    Focus();
                };

                Logger.Info("FirstRunWindow: Initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.Error($"FirstRunWindow: Initialization error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Handles Continue button click
        /// </summary>
        private async void Continue_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Logger.Info("═══ FIRST-RUN CONTINUE CLICKED ═══");
                Logger.Info($"Current settings FirstRunComplete: {_settingsService.CurrentSettings.FirstRunComplete}");

                // Mark first run as complete
                var settings = _settingsService.CurrentSettings.Clone();
                Logger.Info($"Cloned settings FirstRunComplete: {settings.FirstRunComplete}");

                settings.FirstRunComplete = true;
                Logger.Info($"Set FirstRunComplete to TRUE");

                var saved = await _settingsService.SaveSettingsAsync(settings);
                Logger.Info($"SaveSettingsAsync returned: {saved}");

                if (!saved)
                {
                    Logger.Warning("Failed to save FirstRunComplete setting");
                    MessageBox.Show(
                        "Failed to save first-run completion. The wizard may appear again on next launch.",
                        "Save Warning",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                else
                {
                    Logger.Info("✓ First-run wizard completed successfully - settings saved");

                    // Verify the save worked
                    var settingsPath = WinImagePrep.Models.AppSettings.SettingsFilePath;
                    if (System.IO.File.Exists(settingsPath))
                    {
                        var fileContent = await System.IO.File.ReadAllTextAsync(settingsPath);
                        Logger.Info($"Settings file size: {fileContent.Length} chars");
                        Logger.Info($"Settings file content preview: {fileContent.Substring(0, Math.Min(200, fileContent.Length))}...");

                        // Check if FirstRunComplete appears in the file
                        if (fileContent.Contains("\"FirstRunComplete\": true"))
                        {
                            Logger.Info("✓ VERIFIED: FirstRunComplete=true found in settings file");
                        }
                        else if (fileContent.Contains("\"FirstRunComplete\": false"))
                        {
                            Logger.Error("✗ ERROR: FirstRunComplete=false still in settings file!");
                        }
                        else
                        {
                            Logger.Error("✗ ERROR: FirstRunComplete not found in settings file!");
                        }
                    }
                    else
                    {
                        Logger.Error($"✗ ERROR: Settings file not found at {settingsPath}");
                    }
                }

                UserAccepted = true;
                Logger.Info("Closing first-run window...");
                Close();
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Error completing first-run: {ex.Message}");
                Logger.Error($"Stack trace: {ex.StackTrace}");
                MessageBox.Show(
                    $"An error occurred: {ex.Message}\n\nThe application will continue anyway.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                UserAccepted = true;
                Close();
            }
        }
    }
}
