using System;
using System.Threading.Tasks;
using System.Windows;
using WinImagePrep.Helpers;

namespace WinImagePrep
{
    public partial class App : Application
    {
        private SplashScreen? _splashScreen;

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                base.OnStartup(e);

                // Show splash screen
                _splashScreen = new SplashScreen();
                _splashScreen.Show();

                // Set up global exception handling
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                Current.DispatcherUnhandledException += OnDispatcherUnhandledException;

                // Perform initialization asynchronously
                Task.Run(async () =>
                {
                    try
                    {
                        await InitializeApplicationAsync();
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            _splashScreen?.Close();
                            MessageBox.Show(
                                $"Critical startup error:\n\n{ex.Message}",
                                "Critical Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                            Current.Shutdown();
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Critical startup error:\n\n{ex.Message}",
                    "Critical Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Current.Shutdown();
            }
        }

        private async Task InitializeApplicationAsync()
        {
            // Check for administrator privileges (WARNING ONLY)
            await Dispatcher.InvokeAsync(() =>
            {
                _splashScreen?.UpdateStatus("Checking permissions...");
            });

            if (!AdminHelper.IsRunningAsAdministrator())
            {
                var continueStartup = false;
                await Dispatcher.InvokeAsync(() =>
                {
                    var result = MessageBox.Show(
                        "WARNING: This application is not running with administrator privileges.\n\n" +
                        "Some features may not work correctly without admin rights.\n\n" +
                        "Do you want to continue anyway?",
                        "Administrator Rights Warning",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    continueStartup = result == MessageBoxResult.Yes;
                });

                if (!continueStartup)
                {
                    await Dispatcher.InvokeAsync(() => Current.Shutdown());
                    return;
                }
            }

            // Initialize working directory
            await Dispatcher.InvokeAsync(() =>
            {
                _splashScreen?.UpdateStatus("Creating working directories...");
            });

            try
            {
                InitializeWorkingDirectory();
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show(
                        $"Failed to initialize working directory: {ex.Message}",
                        "Initialization Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                });
            }

            // Cleanup old logs (fast operation)
            await Dispatcher.InvokeAsync(() =>
            {
                _splashScreen?.UpdateStatus("Cleaning up old logs...");
            });

            try
            {
                Helpers.Logger.CleanupOldLogs(7);
                Helpers.Logger.Info("=== Application Started ===");
            }
            catch
            {
                // Ignore logging errors
            }

            // Small delay to ensure splash is visible
            await Task.Delay(500);

            // Show main window and close splash
            await Dispatcher.InvokeAsync(() =>
            {
                _splashScreen?.UpdateStatus("Loading main window...");

                var mainWindow = new MainWindow();
                MainWindow = mainWindow;
                mainWindow.Show();

                _splashScreen?.Close();
                _splashScreen = null;
            });
        }

        private void InitializeWorkingDirectory()
        {
            var config = new Models.AppConfiguration();

            // Persistent directories (C:\WinImagePrep)
            var persistentDirectories = new[]
            {
                config.PersistentBaseDirectory,
                config.SavedImagesDirectory,
                config.LogsDirectory,
                config.ConfigDirectory
            };

            // Temporary directories (AppData Local)
            var tempDirectories = new[]
            {
                config.TempBaseDirectory,
                config.Windows11Directory,
                config.DriversDirectory,
                config.MountDirectory,
                config.MountPEDirectory,
                config.MountSetupDirectory
            };

            // Create persistent directories
            foreach (var dir in persistentDirectories)
            {
                if (!System.IO.Directory.Exists(dir))
                {
                    System.IO.Directory.CreateDirectory(dir);
                }
            }

            // Create temporary directories
            foreach (var dir in tempDirectories)
            {
                if (!System.IO.Directory.Exists(dir))
                {
                    System.IO.Directory.CreateDirectory(dir);
                }
            }
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogException(ex);
                CleanupHelper.CleanupMountedImages();
                MessageBox.Show(
                    $"A critical error occurred:\n\n{ex.Message}\n\nThe application will now close.",
                    "Critical Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LogException(e.Exception);
            CleanupHelper.CleanupMountedImages();
            MessageBox.Show(
                $"An error occurred:\n\n{e.Exception.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }

        private void LogException(Exception ex)
        {
            try
            {
                var config = new Models.AppConfiguration();
                var logPath = System.IO.Path.Combine(config.LogsDirectory, "error.log");
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n";
                System.IO.File.AppendAllText(logPath, logEntry);
            }
            catch
            {
                // Ignore logging errors
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Helpers.Logger.Info("=== Application Exiting ===");
            CleanupHelper.CleanupMountedImages();
            base.OnExit(e);
        }
    }
}
