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
                // Mark first run as complete
                var settings = _settingsService.CurrentSettings.Clone();
                settings.FirstRunComplete = true;

                var saved = await _settingsService.SaveSettingsAsync(settings);
                if (!saved)
                {
                    Logger.Warning("Failed to save FirstRunComplete setting");
                }
                else
                {
                    Logger.Info("First-run wizard completed");
                }

                // Check for updates immediately
                Logger.Info("Checking for updates after first-run...");
                try
                {
                    using var httpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                    var updateService = new UpdateService(httpClient);
                    var (updateAvailable, latestVersion) = await updateService.CheckForUpdateAsync();

                    if (updateAvailable && latestVersion != null)
                    {
                        var currentVersion = updateService.GetCurrentVersionString();
                        var latestVersionStr = $"{latestVersion.Major}.{latestVersion.Minor}.{latestVersion.Build}";

                        Logger.Info($"Update available: v{latestVersionStr} (current: v{currentVersion})");

                        var result = MessageBox.Show(
                            $"Welcome to WinImagePrep!\n\n" +
                            $"A newer version is available:\n\n" +
                            $"Current version: {currentVersion}\n" +
                            $"Latest version: {latestVersionStr}\n\n" +
                            $"Would you like to update now?\n\n" +
                            $"The update will download and install automatically.\n" +
                            $"Estimated time: ~1 minute (depending on download speed)",
                            "Update Available",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);

                        if (result == MessageBoxResult.Yes)
                        {
                            Logger.Info("User accepted update - starting download immediately");

                            // Download and apply the update immediately
                            var success = await updateService.DownloadAndApplyUpdateAsync(
                                new Progress<string>(msg => Logger.Info($"Update progress: {msg}")));

                            if (success)
                            {
                                Logger.Info("Update downloaded successfully - application will close for update");

                                // Close this window
                                UserAccepted = false; // Don't continue to main app
                                Close();

                                // Shutdown the application to let updater take over
                                System.Windows.Application.Current.Shutdown();
                                return;
                            }
                            else
                            {
                                Logger.Warning("Update failed - continuing to main application");
                                MessageBox.Show(
                                    "Update failed. You can check for updates later from the Tools menu.",
                                    "Update Failed",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                            }
                        }
                        else
                        {
                            Logger.Info("User declined update");
                        }
                    }
                    else
                    {
                        Logger.Info($"No update available, running latest version: {updateService.GetCurrentVersionString()}");
                    }
                }
                catch (Exception updateEx)
                {
                    Logger.Warning($"Update check failed: {updateEx.Message}");
                    // Don't show error to user, just continue
                }

                UserAccepted = true;
                Close();
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Error completing first-run: {ex.Message}");
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
