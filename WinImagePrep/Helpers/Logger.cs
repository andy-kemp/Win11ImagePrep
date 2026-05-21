using System;
using System.IO;

namespace WinImagePrep.Helpers
{
    public static class Logger
    {
        private static readonly string LogDirectory = Path.Combine(@"C:\WinImagePrep", "Logs");
        private static readonly string LogFilePath = Path.Combine(LogDirectory, $"WinImagePrep_{DateTime.Now:yyyyMMdd}.log");
        private static readonly object _lock = new object();

        static Logger()
        {
            try
            {
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }
            }
            catch
            {
                // Ignore initialization errors
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
