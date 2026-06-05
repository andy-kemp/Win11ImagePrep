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
                    Logger.Info("First-run wizard completed - main window will check for updates");
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
