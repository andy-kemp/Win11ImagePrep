using System;
using System.IO;
using WinImagePrep.Models;

namespace WinImagePrep.Helpers
{
    public static class FileSystemHelper
    {
        /// <summary>
        /// Check if there's enough disk space on the drive
        /// </summary>
        public static DiskSpaceInfo CheckDiskSpace(string path, long requiredGB = 25)
        {
            try
            {
                var driveInfo = new DriveInfo(Path.GetPathRoot(path) ?? "C:\\");
                var freeSpaceGB = driveInfo.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;

                return new DiskSpaceInfo
                {
                    HasEnoughSpace = freeSpaceGB >= requiredGB,
                    FreeSpaceGB = Math.Round(freeSpaceGB, 2),
                    RequiredGB = requiredGB,
                    DriveLetter = driveInfo.Name
                };
            }
            catch
            {
                return new DiskSpaceInfo
                {
                    HasEnoughSpace = false,
                    FreeSpaceGB = 0,
                    RequiredGB = requiredGB,
                    DriveLetter = "Unknown"
                };
            }
        }

        /// <summary>
        /// Ensure a directory exists, creating it if necessary
        /// </summary>
        public static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        /// <summary>
        /// Delete directory contents recursively
        /// </summary>
        public static void DeleteDirectoryContents(string path, bool recursive = true)
        {
            if (!Directory.Exists(path))
                return;

            try
            {
                var dirInfo = new DirectoryInfo(path);

                foreach (var file in dirInfo.GetFiles())
                {
                    try
                    {
                        file.Delete();
                    }
                    catch
                    {
                        // Continue with other files
                    }
                }

                if (recursive)
                {
                    foreach (var dir in dirInfo.GetDirectories())
                    {
                        try
                        {
                            dir.Delete(true);
                        }
                        catch
                        {
                            // Continue with other directories
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
        }

        /// <summary>
        /// Get a safe file name by removing invalid characters
        /// </summary>
        public static string GetSafeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var safeName = fileName;

            foreach (var c in invalidChars)
            {
                safeName = safeName.Replace(c, '_');
            }

            return safeName;
        }

        /// <summary>
        /// Format bytes to human-readable string
        /// </summary>
        public static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}
