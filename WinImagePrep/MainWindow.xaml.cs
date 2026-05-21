using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WinImagePrep.Helpers;
using WinImagePrep.ViewModels;

namespace WinImagePrep
{
    public partial class MainWindow : Window
    {
        private MainViewModel? _viewModel;

        public MainWindow()
        {
            try
            {
                InitializeComponent();

                _viewModel = new MainViewModel();
                DataContext = _viewModel;

                // Run cleanup in background after window is loaded
                Loaded += MainWindow_Loaded;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to initialize main window:\n\n{ex.Message}",
                    "Window Initialization Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                throw;
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Perform cleanup in background after window is shown
            await Task.Run(() =>
            {
                try
                {
                    CleanupHelper.CleanupMountedImages();
                    Logger.Info("Background cleanup completed");
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Background cleanup failed: {ex.Message}");
                }
            });
        }

        private void LogItem_Loaded(object sender, RoutedEventArgs e)
        {
            // Auto-scroll to the latest log entry
            if (sender is ListBoxItem item)
            {
                item.BringIntoView();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // Dispose ViewModel (with timeout)
                var disposeTask = Task.Run(() => _viewModel?.Dispose());
                if (!disposeTask.Wait(TimeSpan.FromSeconds(2)))
                {
                    Logger.Warning("ViewModel dispose timed out after 2 seconds");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error during window close: {ex.Message}");
            }

            base.OnClosed(e);

            // Force application shutdown
            Application.Current.Shutdown();

            // Final fallback - force process exit after a brief delay
            Task.Run(async () =>
            {
                await Task.Delay(500);
                Environment.Exit(0);
            });
        }
    }
}
