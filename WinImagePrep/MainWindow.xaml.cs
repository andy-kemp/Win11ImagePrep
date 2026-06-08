using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
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

        private bool _startupTasksRun = false;

        public MainWindow()
        {
            try
            {
                InitializeComponent();

                _viewModel = new MainViewModel();
                DataContext = _viewModel;

                // Subscribe to log entries changes for auto-scroll
                _viewModel.LogEntries.CollectionChanged += LogEntries_CollectionChanged;

                // Use ONLY ContentRendered for startup tasks (it's more reliable than Loaded)
                ContentRendered += MainWindow_ContentRendered;

                // Setup drag-and-drop handlers
                SetupDragAndDrop();
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

        private async void MainWindow_ContentRendered(object? sender, EventArgs e)
        {
            // Unsubscribe to prevent running twice
            ContentRendered -= MainWindow_ContentRendered;

            // Ensure we only run once
            if (_startupTasksRun) return;
            _startupTasksRun = true;

            // Run the startup tasks
            await RunStartupTasksAsync();
        }

        private async Task RunStartupTasksAsync()
        {
            try
            {
                // Small delay to let the window fully render first
                await Task.Delay(100);

                // Fire-and-forget cleanup in background - DON'T WAIT FOR IT
                // (DISM cleanup can hang/take too long and block startup)
                _ = Task.Run(() =>
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

                // Check for existing work and prompt user
                if (_viewModel != null)
                {
                    await _viewModel.CheckForExistingWorkAsync();
                    await _viewModel.CheckPendingFirstRunUpdateAsync();

                    // Perform startup update check
                    Logger.Info("🚀 MainWindow: About to call PerformFirstRunUpdateCheckAsync...");
                    await _viewModel.PerformFirstRunUpdateCheckAsync();
                    Logger.Info("✅ MainWindow: PerformFirstRunUpdateCheckAsync completed");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Startup tasks failed: {ex.Message}");
                MessageBox.Show(
                    $"Startup tasks encountered an error:\n\n{ex.Message}",
                    "Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void LogEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Auto-scroll to bottom when new log entries are added
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    LogScrollViewer?.ScrollToEnd();
                }), System.Windows.Threading.DispatcherPriority.ContextIdle);
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // This event might not fire reliably - ContentRendered is used as primary trigger
            MessageBox.Show("Loaded event fired (backup)", "DEBUG - Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Check if an operation is in progress
            if (_viewModel != null && _viewModel.IsProcessing)
            {
                var result = MessageBox.Show(
                    "An operation is currently in progress.\n\n" +
                    "Closing now will:\n" +
                    "• Cancel the current operation\n" +
                    "• Terminate any running DISM processes\n" +
                    "• Clean up mounted images\n" +
                    "• May leave temporary files\n\n" +
                    "Are you sure you want to close?",
                    "Operation In Progress",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }

                // User confirmed - cancel the operation
                try
                {
                    _viewModel.CancelOperation();
                    Logger.Info("Operation cancelled by user closing window");
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Error cancelling operation: {ex.Message}");
                }
            }

            base.OnClosing(e);
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

        private void SetupDragAndDrop()
        {
            // ISO TextBox drag-and-drop
            IsoTextBox.PreviewDragEnter += IsoTextBox_PreviewDragEnter;
            IsoTextBox.PreviewDragOver += IsoTextBox_PreviewDragOver;
            IsoTextBox.Drop += IsoTextBox_Drop;

            // Driver Source TextBox drag-and-drop
            DriverSourceTextBox.PreviewDragEnter += DriverSourceTextBox_PreviewDragEnter;
            DriverSourceTextBox.PreviewDragOver += DriverSourceTextBox_PreviewDragOver;
            DriverSourceTextBox.Drop += DriverSourceTextBox_Drop;
        }

        // ISO drag-and-drop handlers
        private void IsoTextBox_PreviewDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void IsoTextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void IsoTextBox_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files != null && files.Length > 0)
                    {
                        string filePath = files[0]; // Take first file only
                        string extension = Path.GetExtension(filePath).ToLowerInvariant();

                        if (extension == ".iso")
                        {
                            if (_viewModel != null)
                            {
                                _viewModel.SelectedIsoPath = filePath;
                                _viewModel.AddLog($"📁 ISO file loaded via drag-and-drop: {Path.GetFileName(filePath)}");
                                Logger.Info($"ISO file loaded via drag-and-drop: {filePath}");
                            }
                        }
                        else
                        {
                            MessageBox.Show(
                                "Please drop a valid ISO file (.iso extension).",
                                "Invalid File Type",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error handling ISO drop: {ex.Message}");
                MessageBox.Show(
                    $"Error loading dropped file:\n\n{ex.Message}",
                    "Drag-and-Drop Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            e.Handled = true;
        }

        // Driver Source drag-and-drop handlers
        private void DriverSourceTextBox_PreviewDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void DriverSourceTextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void DriverSourceTextBox_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] items = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (items != null && items.Length > 0)
                    {
                        string itemPath = items[0]; // Take first item only

                        if (Directory.Exists(itemPath))
                        {
                            // Folder dropped
                            if (_viewModel != null)
                            {
                                _viewModel.IsDriverSourceFolder = true;
                                _viewModel.SelectedDriverSourcePath = itemPath;
                                _viewModel.AddLog($"📁 Driver folder loaded via drag-and-drop: {Path.GetFileName(itemPath)}");
                                Logger.Info($"Driver folder loaded via drag-and-drop: {itemPath}");
                            }
                        }
                        else if (File.Exists(itemPath))
                        {
                            // File dropped
                            string extension = Path.GetExtension(itemPath).ToLowerInvariant();

                            if (extension == ".msi")
                            {
                                if (_viewModel != null)
                                {
                                    _viewModel.IsDriverSourceMsi = true;
                                    _viewModel.SelectedDriverSourcePath = itemPath;
                                    _viewModel.AddLog($"📦 MSI file loaded via drag-and-drop: {Path.GetFileName(itemPath)}");
                                    Logger.Info($"MSI file loaded via drag-and-drop: {itemPath}");
                                }
                            }
                            else if (extension == ".zip")
                            {
                                if (_viewModel != null)
                                {
                                    _viewModel.IsDriverSourceZip = true;
                                    _viewModel.SelectedDriverSourcePath = itemPath;
                                    _viewModel.AddLog($"📦 ZIP file loaded via drag-and-drop: {Path.GetFileName(itemPath)}");
                                    Logger.Info($"ZIP file loaded via drag-and-drop: {itemPath}");
                                }
                            }
                            else
                            {
                                MessageBox.Show(
                                    "Please drop a valid driver file (.msi or .zip) or a folder containing drivers.",
                                    "Invalid File Type",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error handling driver source drop: {ex.Message}");
                MessageBox.Show(
                    $"Error loading dropped file:\n\n{ex.Message}",
                    "Drag-and-Drop Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            e.Handled = true;
        }
    }
}
