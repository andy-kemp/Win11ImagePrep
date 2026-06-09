using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using WinImagePrep.Helpers;
using WinImagePrep.ViewModels;

namespace WinImagePrep
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern bool ChangeWindowMessageFilterEx(IntPtr hwnd, uint message, uint action, ref CHANGEFILTERSTRUCT pChangeFilterStruct);

        [DllImport("user32.dll")]
        private static extern bool ChangeWindowMessageFilter(uint message, uint dwFlag);

        [StructLayout(LayoutKind.Sequential)]
        private struct CHANGEFILTERSTRUCT
        {
            public uint cbSize;
            public uint ExtStatus;
        }

        private const uint WM_DROPFILES = 0x0233;
        private const uint WM_COPYDATA = 0x004A;
        private const uint WM_COPYGLOBALDATA = 0x0049;
        private const uint MSGFLT_ALLOW = 1;
        private const uint MSGFLT_ADD = 1;

        // Additional messages needed for drag-and-drop in elevated apps
        private const uint WM_CLIPBOARDUPDATE = 0x031D;
        private const uint WM_DWMSENDICONICTHUMBNAIL = 0x0323;
        private const uint WM_DWMSENDICONICLIVEPREVIEWBITMAP = 0x0326;

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

                // Setup drag-and-drop after window is loaded
                SourceInitialized += MainWindow_SourceInitialized;

                Logger.Info("MainWindow initialized successfully");
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

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            try
            {
                // Allow drag-and-drop from lower privilege processes (File Explorer -> elevated app)
                var hwnd = new WindowInteropHelper(this).Handle;

                Logger.Info($"Window handle: {hwnd}");

                // Allow all necessary messages for drag-and-drop to work with UIPI
                uint[] messages = new uint[] 
                { 
                    WM_DROPFILES, 
                    WM_COPYDATA, 
                    WM_COPYGLOBALDATA,
                    WM_CLIPBOARDUPDATE,
                    WM_DWMSENDICONICTHUMBNAIL,
                    WM_DWMSENDICONICLIVEPREVIEWBITMAP
                };

                // Use both ChangeWindowMessageFilter and ChangeWindowMessageFilterEx for compatibility
                foreach (var msg in messages)
                {
                    ChangeWindowMessageFilter(msg, MSGFLT_ADD);

                    var changeFilterStruct = new CHANGEFILTERSTRUCT { cbSize = (uint)Marshal.SizeOf(typeof(CHANGEFILTERSTRUCT)) };
                    ChangeWindowMessageFilterEx(hwnd, msg, MSGFLT_ALLOW, ref changeFilterStruct);
                }

                Logger.Info($"Configured {messages.Length} window message filters for drag-and-drop");

                // Also try to hook window messages directly
                HwndSource source = HwndSource.FromHwnd(hwnd);
                if (source != null)
                {
                    source.AddHook(WndProc);
                    Logger.Info("Window message hook added");
                }

                Logger.Info("Drag-and-drop UIPI bypass configured successfully");

                // Wire up drag-and-drop events using PREVIEW events for better UIPI handling
                if (DropZone != null)
                {
                    DropZone.PreviewDrop += DropZone_Drop;
                    DropZone.PreviewDragEnter += DropZone_DragEnter;
                    DropZone.PreviewDragLeave += DropZone_DragLeave;
                    DropZone.PreviewDragOver += DropZone_DragOver;
                    Logger.Info("Drop zone PREVIEW event handlers attached");
                }
                else
                {
                    Logger.Error("DropZone is NULL - cannot attach handlers!");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to configure drag-and-drop: {ex.Message}");
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_DROPFILES_INT = 0x0233;

            if (msg == WM_DROPFILES_INT)
            {
                Logger.Info("WM_DROPFILES message received!");
                // Let WPF handle it
                handled = false;
            }

            return IntPtr.Zero;
        }

        private void DropZone_DragEnter(object sender, DragEventArgs e)
        {
            Logger.Info("DropZone_DragEnter called!");
            Logger.Info($"Data formats available: {string.Join(", ", e.Data.GetFormats())}");

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                Logger.Info("FileDrop data present - allowing copy");
                e.Effects = DragDropEffects.Copy;
                if (sender is System.Windows.Controls.Border border)
                {
                    border.BorderBrush = System.Windows.Media.Brushes.Green;
                    border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(230, 255, 237));
                }
            }
            else
            {
                Logger.Info("No FileDrop data - rejecting");
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void DropZone_DragOver(object sender, DragEventArgs e)
        {
            // Less verbose - only log on first call
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

        private void DropZone_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border)
            {
                border.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212));
                border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 248, 248));
            }
        }

        private void DropZone_Drop(object sender, DragEventArgs e)
        {
            try
            {
                Logger.Info("DropZone_Drop called!");

                // Restore border appearance
                if (sender is System.Windows.Controls.Border border)
                {
                    border.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212));
                    border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 248, 248));
                }

                if (e.Data.GetDataPresent(DataFormats.FileDrop) && _viewModel != null)
                {
                    string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    Logger.Info($"Files dropped: {files.Length}");

                    bool isoFound = false;
                    int driversAdded = 0;

                    foreach (string path in files)
                    {
                        Logger.Info($"Processing: {path}");

                        if (File.Exists(path))
                        {
                            string ext = Path.GetExtension(path).ToLowerInvariant();
                            Logger.Info($"Extension: {ext}");

                            if (ext == ".iso" && !isoFound)
                            {
                                _viewModel.SelectedIsoPath = path;
                                _viewModel.AddLog($"📁 ISO loaded: {Path.GetFileName(path)}");
                                Logger.Info("ISO loaded successfully");
                                isoFound = true;
                            }
                            else if (ext == ".msi")
                            {
                                var driverSource = new WinImagePrep.Models.DriverSourceInfo
                                {
                                    Path = path,
                                    Type = WinImagePrep.Models.DriverSourceType.Msi
                                };
                                _viewModel.DriverSources.Add(driverSource);
                                _viewModel.AddLog($"📦 MSI driver pack added: {Path.GetFileName(path)}");
                                Logger.Info("MSI loaded successfully");
                                driversAdded++;
                            }
                            else if (ext == ".zip")
                            {
                                var driverSource = new WinImagePrep.Models.DriverSourceInfo
                                {
                                    Path = path,
                                    Type = WinImagePrep.Models.DriverSourceType.Zip
                                };
                                _viewModel.DriverSources.Add(driverSource);
                                _viewModel.AddLog($"📦 ZIP driver pack added: {Path.GetFileName(path)}");
                                Logger.Info("ZIP loaded successfully");
                                driversAdded++;
                            }
                        }
                        else if (Directory.Exists(path))
                        {
                            var driverSource = new WinImagePrep.Models.DriverSourceInfo
                            {
                                Path = path,
                                Type = WinImagePrep.Models.DriverSourceType.Folder
                            };
                            _viewModel.DriverSources.Add(driverSource);
                            _viewModel.AddLog($"📁 Driver folder added: {Path.GetFileName(path)}");
                            Logger.Info("Folder loaded successfully");
                            driversAdded++;
                        }
                    }

                    if (isoFound && driversAdded > 0)
                    {
                        _viewModel.AddLog($"✅ ISO and {driversAdded} driver pack(s) loaded via drag-and-drop!");
                    }
                    else if (isoFound)
                    {
                        _viewModel.AddLog("✅ ISO loaded successfully!");
                    }
                    else if (driversAdded > 0)
                    {
                        _viewModel.AddLog($"✅ {driversAdded} driver pack(s) loaded successfully!");
                    }
                    else
                    {
                        Logger.Warning("No valid files found in drop");
                        _viewModel.AddLog("⚠️ No valid ISO or driver files found");
                    }
                }
                else
                {
                    Logger.Warning("No FileDrop data in drop event");
                }

                e.Handled = true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Drop error: {ex.Message}");
                Logger.Error($"Stack trace: {ex.StackTrace}");
                _viewModel?.AddLog($"❌ Error loading files: {ex.Message}");
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
    }
}
