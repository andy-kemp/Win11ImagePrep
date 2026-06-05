using System;
using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;

namespace WinImagePrep.Updater;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static string? _logPath;

    // Static constructor runs BEFORE anything else
    static App()
    {
        try
        {
            _logPath = Path.Combine(Path.GetTempPath(), $"WinImagePrep_Updater_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            File.WriteAllText(_logPath, $"=== Static constructor called at {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} ===\n");
            File.AppendAllText(_logPath, $"Command line: {Environment.CommandLine}\n");
            File.AppendAllText(_logPath, $"Current directory: {Environment.CurrentDirectory}\n");
            File.AppendAllText(_logPath, $"Process path: {Environment.ProcessPath}\n");

            // Register global exception handlers IMMEDIATELY in static constructor
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                File.AppendAllText(_logPath!, $"\n=== FATAL UNHANDLED EXCEPTION ===\n");
                File.AppendAllText(_logPath!, $"{ex?.GetType().Name}: {ex?.Message}\n");
                File.AppendAllText(_logPath!, $"Stack trace:\n{ex?.StackTrace}\n");
                try
                {
                    MessageBox.Show(
                        $"Updater crashed:\n\n{ex?.Message}\n\nLog: {_logPath}",
                        "Updater Fatal Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                catch { }
            };

            File.AppendAllText(_logPath, "Static constructor completed, exception handlers registered\n");
        }
        catch (Exception ex)
        {
            // Last resort - write to a fixed location
            var fallbackPath = Path.Combine(Path.GetTempPath(), "WinImagePrep_Updater_CRASH.log");
            File.WriteAllText(fallbackPath, $"Static constructor failed: {ex.Message}\n{ex.StackTrace}");
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            Log("=== OnStartup ENTRY ===");
            Log($"Command line: {Environment.CommandLine}");
            Log($"Args count: {e.Args.Length}");
            for (int i = 0; i < e.Args.Length; i++)
            {
                Log($"  Arg[{i}]: {e.Args[i]}");
            }

            // Register Dispatcher exception handler (AppDomain handler already registered in static constructor)
            DispatcherUnhandledException += (sender, args) =>
            {
                Log($"DISPATCHER EXCEPTION: {args.Exception.GetType().Name}: {args.Exception.Message}");
                Log($"Stack trace: {args.Exception.StackTrace}");
                try
                {
                    MessageBox.Show(
                        $"Updater error:\n\n{args.Exception.Message}\n\nLog: {_logPath}",
                        "Updater Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                catch { }
                args.Handled = true;
            };

            Log("About to call base.OnStartup...");
            base.OnStartup(e);
            Log("Base OnStartup completed successfully");

            // Manually create and show the MainWindow
            Log("Creating MainWindow...");
            var mainWindow = new MainWindow();
            Log("MainWindow created, showing...");
            mainWindow.Show();
            Log("MainWindow shown successfully");
        }
        catch (Exception ex)
        {
            Log($"EXCEPTION in OnStartup wrapper: {ex.GetType().Name}: {ex.Message}");
            Log($"Stack trace: {ex.StackTrace}");
            try
            {
                MessageBox.Show(
                    $"Updater failed to start:\n\n{ex.Message}\n\n{ex.StackTrace}\n\nLog: {_logPath}",
                    "Updater Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch { }
            Shutdown(1);
        }
    }

    public static void Log(string message)
    {
        try
        {
            if (_logPath == null) return;
            var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
            File.AppendAllText(_logPath, logMessage + Environment.NewLine);
        }
        catch
        {
            // Can't log if logging fails
        }
    }
}

