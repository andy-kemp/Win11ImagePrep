using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using WinImagePrep.Helpers;
using WinImagePrep.Models;
using WinImagePrep.Services;

namespace WinImagePrep
{
    public partial class App : Application
    {
        private SplashScreen? _splashScreen;
        private ISettingsService? _settingsService;
        private AppSettings? _appSettings;
        private AppState? _restoreState;

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                base.OnStartup(e);

                // Parse command-line arguments for state restoration
                ParseCommandLineArgs(e.Args);

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

        /// <summary>
        /// Parse command-line arguments for state restoration after elevation
        /// </summary>
        private void ParseCommandLineArgs(string[] args)
        {
            try
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--state" && i + 1 < args.Length)
                    {
                        try
                        {
                            // Decode base64-encoded JSON state
                            var base64 = args[i + 1];
                            var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
                            _restoreState = JsonSerializer.Deserialize<AppState>(json);
                            Logger.Info($"Parsed application state for restoration");
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Failed to parse state argument: {ex.Message}");
                        }
                        i++; // Skip next arg since we consumed it
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error parsing command-line arguments: {ex.Message}");
            }
        }

        private async Task InitializeApplicationAsync()
        {
            // Load settings
            await Dispatcher.InvokeAsync(() =>
            {
                _splashScreen?.UpdateStatus("Loading settings...");
            });

            try
            {
                _settingsService = new SettingsService();
                _appSettings = await _settingsService.LoadSettingsAsync();

                Logger.Info($"Settings loaded: WorkingRoot = {_appSettings.WorkingRoot}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load settings: {ex.Message}");

                // Use defaults if settings fail to load
                _settingsService = new SettingsService();
                _appSettings = _settingsService.GetDefaultSettings();
                Logger.Warning("Using default settings due to load failure");
            }

            // Validate settings
            await Dispatcher.InvokeAsync(() =>
            {
                _splashScreen?.UpdateStatus("Validating configuration...");
            });

            var validationResult = await _settingsService.ValidateSettingsAsync(_appSettings);
            if (!validationResult.IsValid)
            {
                Logger.Error($"Settings validation failed: {validationResult.GetErrorMessages()}");

                var offerOptions = false;
                await Dispatcher.InvokeAsync(() =>
                {
                    var result = MessageBox.Show(
                        "Configuration validation failed:\n\n" +
                        validationResult.GetErrorMessages() + "\n\n" +
                        "Would you like to open the Options dialog to fix the configuration?",
                        "Configuration Error",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    offerOptions = result == MessageBoxResult.Yes;
                });

                if (offerOptions)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _splashScreen?.Close();

                        // Show options dialog
                        var optionsViewModel = new ViewModels.OptionsViewModel(_settingsService);
                        var optionsWindow = new OptionsWindow(optionsViewModel);
                        var dialogResult = optionsWindow.ShowDialog();

                        if (dialogResult == true)
                        {
                            // Reload settings
                            _appSettings = _settingsService.CurrentSettings;
                            Logger.Info("Settings updated through Options dialog");

                            // Continue with startup
                            Task.Run(async () => await ContinueInitializationAsync());
                        }
                        else
                        {
                            // User cancelled, exit
                            Current.Shutdown();
                        }
                    });
                    return;
                }
                else
                {
                    await Dispatcher.InvokeAsync(() => Current.Shutdown());
                    return;
                }
            }

            await ContinueInitializationAsync();
        }

        private async Task ContinueInitializationAsync()
        {
            // Initialize working directory
            await Dispatcher.InvokeAsync(() =>
            {
                _splashScreen?.UpdateStatus("Creating working directories...");
            });

            try
            {
                await InitializeWorkingDirectoryAsync();
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
                Helpers.Logger.Info($"Working Root: {_appSettings?.WorkingRoot}");
                Helpers.Logger.Info($"Log Level: {_appSettings?.LogLevel}");
            }
            catch
            {
                // Ignore logging errors
            }

            // Small delay to ensure splash is visible
            await Task.Delay(500);

            // Check for first run
            Logger.Info($"═══ FIRST-RUN CHECK ═══");
            Logger.Info($"_appSettings is null: {_appSettings == null}");

            // Additional safety: if settings file exists, treat as already initialized
            bool settingsFileExists = System.IO.File.Exists(Models.AppSettings.SettingsFilePath);
            Logger.Info($"Settings file exists: {settingsFileExists}");

            if (_appSettings != null)
            {
                Logger.Info($"_appSettings.FirstRunComplete: {_appSettings.FirstRunComplete}");
            }

            // Only show first-run if BOTH conditions are met:
            // 1. FirstRunComplete flag is false
            // 2. Settings file doesn't exist (truly first time)
            bool shouldShowFirstRun = _appSettings != null && !_appSettings.FirstRunComplete && !settingsFileExists;
            Logger.Info($"Should show first-run wizard: {shouldShowFirstRun}");

            if (shouldShowFirstRun)
            {
                try
                {
                    Logger.Info("First run detected, showing welcome wizard...");
                    bool shouldContinue = false;

                    // Close splash screen first
                    await Dispatcher.InvokeAsync(() =>
                    {
                        Logger.Info("Closing splash screen...");
                        _splashScreen?.Close();
                        _splashScreen = null;
                        Logger.Info("Splash screen closed");
                    });

                    // Small delay to ensure splash is fully closed
                    await Task.Delay(500);

                    // Use TaskCompletionSource to wait for window to close
                    var tcs = new TaskCompletionSource<bool>();

                    await Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            Logger.Info("Creating FirstRunWindow as main window...");
                            var firstRunWindow = new FirstRunWindow(_settingsService!);

                            // Set as main window and wire up close handler
                            Current.MainWindow = firstRunWindow;

                            firstRunWindow.Closed += (s, e) =>
                            {
                                Logger.Info($"FirstRunWindow closed, UserAccepted: {firstRunWindow.UserAccepted}");
                                tcs.SetResult(firstRunWindow.UserAccepted);
                            };

                            Logger.Info("Showing FirstRunWindow (non-modal)...");
                            firstRunWindow.Show();
                            firstRunWindow.Activate();
                            Logger.Info("FirstRunWindow shown and activated");
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Error creating/showing first-run window: {ex.Message}");
                            Logger.Error($"Stack trace: {ex.StackTrace}");
                            MessageBox.Show(
                                $"First-run wizard error: {ex.Message}\n\nContinuing to main application...",
                                "First Run Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                            tcs.SetResult(true);
                        }
                    });

                    // Wait for window to close
                    Logger.Info("Waiting for first-run window to close...");
                    shouldContinue = await tcs.Task;
                    Logger.Info($"First-run complete, shouldContinue: {shouldContinue}");

                    if (!shouldContinue)
                    {
                        // User cancelled first-run
                        Logger.Info("User cancelled first-run, shutting down");
                        await Dispatcher.InvokeAsync(() => Current.Shutdown());
                        return;
                    }

                    // Clear the temporary main window
                    await Dispatcher.InvokeAsync(() => Current.MainWindow = null);

                    // Reload settings after first run from disk to pick up any changes
                    if (_settingsService != null)
                    {
                        try
                        {
                            Logger.Info("═══ RELOADING SETTINGS AFTER FIRST-RUN ═══");
                            await _settingsService.ReloadSettingsAsync();
                            _appSettings = _settingsService.CurrentSettings;
                            Logger.Info($"Settings reloaded - FirstRunComplete: {_appSettings.FirstRunComplete}");

                            // Double-check the file
                            var settingsPath = Models.AppSettings.SettingsFilePath;
                            if (System.IO.File.Exists(settingsPath))
                            {
                                var fileContent = await System.IO.File.ReadAllTextAsync(settingsPath);
                                if (fileContent.Contains("\"FirstRunComplete\": true"))
                                {
                                    Logger.Info("✓ File verification: FirstRunComplete=true in file");
                                }
                                else if (fileContent.Contains("\"FirstRunComplete\": false"))
                                {
                                    Logger.Error("✗ File verification: FirstRunComplete=false in file (THIS IS THE BUG!)");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Error reloading settings after first-run: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Fatal error in first-run flow: {ex.Message}");
                    Logger.Error($"Stack trace: {ex.StackTrace}");
                    MessageBox.Show(
                        $"First-run error: {ex.Message}\n\nThe application will now exit.",
                        "Startup Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    await Dispatcher.InvokeAsync(() => Current.Shutdown());
                    return;
                }
            }
            else
            {
                Logger.Info("Not first run, skipping welcome wizard");
            }

            // Show main window and close splash
            await Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    // Close splash screen FIRST
                    if (_splashScreen != null)
                    {
                        _splashScreen.Close();
                        _splashScreen = null;
                    }

                    // Small delay to ensure splash is fully closed
                    System.Threading.Thread.Sleep(100);

                    var mainWindow = new MainWindow();
                    MainWindow = mainWindow;

                    // Restore state from command-line args if present (after elevation)
                    if (_restoreState != null && mainWindow.DataContext is ViewModels.MainViewModel viewModel)
                    {
                        Logger.Info("Restoring application state from command-line arguments...");
                        viewModel.RestoreState(_restoreState);
                    }

                    mainWindow.Show();
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error showing main window: {ex.Message}");
                    MessageBox.Show(
                        $"Failed to show main window: {ex.Message}",
                        "Startup Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    Current.Shutdown();
                }
            });
        }

        private async Task InitializeWorkingDirectoryAsync()
        {
            if (_settingsService == null || _appSettings == null)
            {
                throw new InvalidOperationException("Settings not loaded");
            }

            // Create all required directories
            var created = await _settingsService.CreateRequiredDirectoriesAsync(_appSettings);
            if (!created)
            {
                throw new InvalidOperationException("Failed to create required directories");
            }

            Logger.Info("Working directories initialized successfully");
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
                // Use settings if available, otherwise use default path
                string logsDirectory;
                if (_appSettings != null)
                {
                    logsDirectory = _appSettings.LogsDirectory;
                }
                else
                {
                    logsDirectory = System.IO.Path.Combine(AppSettings.DefaultWorkingRoot, "Logs");
                }

                var logPath = System.IO.Path.Combine(logsDirectory, "error.log");
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
