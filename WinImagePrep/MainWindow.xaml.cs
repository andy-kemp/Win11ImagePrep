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
            // Auto-scroll to the latest log entry only if we're already near the bottom
            if (sender is ListBoxItem item && item.Parent is ListBox listBox)
            {
                var scrollViewer = FindScrollViewer(listBox);
                if (scrollViewer != null)
                {
                    // Only auto-scroll if user is already at or near the bottom (within 50 pixels)
                    var distanceFromBottom = scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset;
                    if (distanceFromBottom < 50)
                    {
                        scrollViewer.ScrollToEnd();
                    }
                }
            }
        }

        private ScrollViewer? FindScrollViewer(DependencyObject parent)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is ScrollViewer scrollViewer)
                    return scrollViewer;

                var result = FindScrollViewer(child);
                if (result != null)
                    return result;
            }
            return null;
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
