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

    protected override void OnStartup(StartupEventArgs e)
    {
        // Set up global exception handling and logging BEFORE anything else
        _logPath = Path.Combine(Path.GetTempPath(), $"WinImagePrep_Updater_{DateTime.Now:yyyyMMdd_HHmmss}.log");

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

        try
        {
            base.OnStartup(e);
            Log("Base OnStartup completed successfully");
        }
        catch (Exception ex)
        {
            Log($"ERROR in OnStartup: {ex.GetType().Name}: {ex.Message}");
            Log($"Stack trace: {ex.StackTrace}");
            throw;
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

