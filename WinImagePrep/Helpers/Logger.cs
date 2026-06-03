using System;
using System.IO;
using WinImagePrep.Models;
using WinImagePrep.Services;

namespace WinImagePrep.Helpers
{
    public static class Logger
    {
        private static string? _logDirectory;
        private static string? _logFilePath;
        private static readonly object _lock = new object();

        // Lazy initialization - uses settings if available, falls back to default
        private static string LogDirectory
        {
            get
            {
                if (_logDirectory == null)
                {
                    try
                    {
                        var settingsService = new SettingsService();
                        _logDirectory = settingsService.CurrentSettings.LogsDirectory;
                    }
                    catch
                    {
                        // Fallback to default if settings not available
                        _logDirectory = Path.Combine(AppSettings.DefaultWorkingRoot, "Logs");
                    }

                    try
                    {
                        if (!Directory.Exists(_logDirectory))
                        {
                            Directory.CreateDirectory(_logDirectory);
                        }
                    }
                    catch
                    {
                        // Ignore initialization errors
                    }
                }
                return _logDirectory;
            }
        }

        private static string LogFilePath
        {
            get
            {
                if (_logFilePath == null)
                {
                    _logFilePath = Path.Combine(LogDirectory, $"WinImagePrep_{DateTime.Now:yyyyMMdd}.log");
                }
                return _logFilePath;
            }
        }

        public static void Log(string message, LogLevel level = LogLevel.Info)
        {
            try
            {
                lock (_lock)
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var logEntry = $"[{timestamp}] [{level}] {message}";

                    File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
                }
            }
            catch
            {
                // Ignore logging errors
            }
        }

        public static void Info(string message) => Log(message, LogLevel.Info);
        public static void Warning(string message) => Log(message, LogLevel.Warning);
        public static void Error(string message) => Log(message, LogLevel.Error);
        public static void Debug(string message) => Log(message, LogLevel.Debug);

        public static void Exception(Exception ex, string context = "")
        {
            var message = string.IsNullOrEmpty(context) 
                ? $"Exception: {ex.Message}\n{ex.StackTrace}"
                : $"Exception in {context}: {ex.Message}\n{ex.StackTrace}";
            Log(message, LogLevel.Error);
        }

        public static void CleanupOldLogs(int daysToKeep = 7)
        {
            try
            {
                if (!Directory.Exists(LogDirectory))
                    return;

                var files = Directory.GetFiles(LogDirectory, "WinImagePrep_*.log");
                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch
                        {
                            // Continue with other files
                        }
                    }
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }
}
