using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using WinImagePrep.Models;

namespace WinImagePrep.Helpers
{
    public static class CleanupHelper
    {
        /// <summary>
        /// Clean up all mounted WIM images and temporary files
        /// </summary>
        public static void CleanupMountedImages()
        {
            try
            {
                // Get list of mounted images using DISM
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "dism.exe",
                        Arguments = "/Get-MountedImageInfo",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // Parse output to find mount directories and discard them
                if (output.Contains("Mount Dir"))
                {
                    var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (line.Contains("Mount Dir") && line.Contains(":"))
                        {
                            var mountDir = line.Split(':')[1].Trim();
                            if (Directory.Exists(mountDir))
                            {
                                DismountImage(mountDir);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors during cleanup
            }

            // Dismount any mounted ISOs
            DismountAllISOs();

            // Clean temporary working directories (not saved images!)
            CleanupTempWorkingDirectories();
        }

        /// <summary>
        /// Dismount a specific WIM image
        /// </summary>
        public static void DismountImage(string mountPath, bool commit = false)
        {
            try
            {
                var arguments = $"/Unmount-Image /MountDir:\"{mountPath}\" /{(commit ? "Commit" : "Discard")}";
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "dism.exe",
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.WaitForExit();
            }
            catch
            {
                // Ignore errors
            }
        }

        /// <summary>
        /// Dismount all mounted ISOs
        /// </summary>
        public static void DismountAllISOs()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-Command \"Get-DiskImage | Where-Object {$_.Attached -eq $true} | Dismount-DiskImage\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                process.Start();
                process.WaitForExit();
            }
            catch
            {
                // Ignore errors
            }
        }

        /// <summary>
        /// Clean up temporary files in working directory
        /// </summary>
        public static void CleanupTemporaryFiles(string baseDirectory)
        {
            try
            {
                var mountDir = Path.Combine(baseDirectory, "Mount");
                if (Directory.Exists(mountDir))
                {
                    var subDirs = Directory.GetDirectories(mountDir);
                    foreach (var dir in subDirs)
                    {
                        try
                        {
                            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                            {
                                Directory.Delete(dir, false);
                            }
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
        /// Clean up temporary working directories (AppData Local temp files)
        /// Does NOT touch saved images or logs in C:\WinImagePrep
        /// </summary>
        public static void CleanupTempWorkingDirectories()
        {
            try
            {
                var config = new AppConfiguration();

                // Only clean temporary directories, NOT persistent ones
                var tempDirectoriesToClean = new[]
                {
                    config.Windows11Directory,
                    config.DriversDirectory,
                    config.MountDirectory
                };

                foreach (var dir in tempDirectoriesToClean)
                {
                    try
                    {
                        if (Directory.Exists(dir))
                        {
                            // Delete contents but keep the directory structure
                            FileSystemHelper.DeleteDirectoryContents(dir);
                        }
                    }
                    catch
                    {
                        // Continue with other directories
                    }
                }
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }
    }
}
