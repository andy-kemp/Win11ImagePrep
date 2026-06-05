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
            // DON'T overwrite _logPath - use the one from static constructor
            // _logPath was already set in static constructor

            Log("=== Updater Application Starting ===");
            Log($"Command line: {Environment.CommandLine}");
            Log($"Args count: {e.Args.Length}");
            for (int i = 0; i < e.Args.Length; i++)
            {
                Log($"  Arg[{i}]: {e.Args[i]}");
            }

            // Global exception handlers
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                Log($"FATAL UNHANDLED EXCEPTION: {ex?.GetType().Name}: {ex?.Message}");
                Log($"Stack trace: {ex?.StackTrace}");
                MessageBox.Show(
                    $"Updater crashed:\n\n{ex?.Message}\n\nLog: {_logPath}",
                    "Updater Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (sender, args) =>
            {
                Log($"DISPATCHER EXCEPTION: {args.Exception.GetType().Name}: {args.Exception.Message}");
                Log($"Stack trace: {args.Exception.StackTrace}");
                MessageBox.Show(
                    $"Updater error:\n\n{args.Exception.Message}\n\nLog: {_logPath}",
                    "Updater Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                args.Handled = true;
            };

            Log("About to call base.OnStartup...");
            base.OnStartup(e);
            Log("Base OnStartup completed successfully");
        }
        catch (Exception ex)
        {
            Log($"EXCEPTION in OnStartup wrapper: {ex.GetType().Name}: {ex.Message}");
            Log($"Stack trace: {ex.StackTrace}");
            MessageBox.Show(
                $"Updater failed to start:\n\n{ex.Message}\n\nLog: {_logPath}",
                "Updater Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
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

