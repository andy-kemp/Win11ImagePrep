using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinImagePrep.Helpers;
using WinImagePrep.Models;

namespace WinImagePrep.Services
{
    public class DismService
    {
        private readonly string _dismPath = "dism.exe";

        /// <summary>
        /// Mount a WIM image
        /// </summary>
        public async Task<bool> MountWimAsync(
            string wimPath,
            int imageIndex,
            string mountPath,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                progress?.Report($"Mounting WIM image {imageIndex} from {Path.GetFileName(wimPath)}...");

                // Verify WIM file exists and is accessible
                if (!File.Exists(wimPath))
                {
                    progress?.Report($"✗ ERROR: WIM file not found: {wimPath}");
                    return false;
                }

                // Check and clear read-only flag if set
                var fileInfo = new FileInfo(wimPath);
                if (fileInfo.IsReadOnly)
                {
                    progress?.Report($"⚠ WIM file is read-only, removing read-only flag...");
                    fileInfo.IsReadOnly = false;
                }

                // Ensure mount directory exists and is empty
                FileSystemHelper.EnsureDirectoryExists(mountPath);
                if (Directory.EnumerateFileSystemEntries(mountPath).Any())
                {
                    progress?.Report($"⚠ Mount directory not empty, cleaning: {mountPath}");
                    FileSystemHelper.DeleteDirectoryContents(mountPath);
                }

                var arguments = $"/Mount-Wim /WimFile:\"{wimPath}\" /Index:{imageIndex} /MountDir:\"{mountPath}\"";

                progress?.Report($"DISM command: dism.exe {arguments}");
                var result = await ProcessHelper.ExecuteProcessAsync(_dismPath, arguments, cancellationToken);

                progress?.Report($"DISM exit code: {result.ExitCode}");
                if (!string.IsNullOrEmpty(result.Output))
                {
                    progress?.Report($"DISM output: {result.Output.Substring(0, Math.Min(500, result.Output.Length))}");
                }
                if (!string.IsNullOrEmpty(result.Error))
                {
                    progress?.Report($"DISM error: {result.Error}");
                }

                if (result.Success)
                {
                    progress?.Report($"Successfully mounted image {imageIndex}");

                    // Verify mount was successful by checking if Windows folder exists
                    var windowsFolder = Path.Combine(mountPath, "Windows");
                    if (Directory.Exists(windowsFolder))
                    {
                        progress?.Report($"✓ Mount verified: Windows folder found");
                        return true;
                    }
                    else
                    {
                        progress?.Report($"⚠ WARNING: Mount may not be complete, Windows folder not found");
                        return false;
                    }
                }
                else
                {
                    progress?.Report($"Failed to mount image: Exit code {result.ExitCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"Error mounting WIM: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Unmount a WIM image
        /// </summary>
        public async Task<bool> UnmountWimAsync(
            string mountPath,
            bool commit = true,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default,
            bool deleteMountDirectory = false)
        {
            try
            {
                progress?.Report($"Unmounting image from {mountPath}...");

                // Check if this is actually a mounted image
                if (!Directory.Exists(mountPath))
                {
                    progress?.Report($"⚠ Mount directory does not exist, skipping unmount: {mountPath}");
                    return true; // Not an error, just nothing to unmount
                }

                var windowsFolder = Path.Combine(mountPath, "Windows");
                if (!Directory.Exists(windowsFolder))
                {
                    progress?.Report($"⚠ No Windows folder found, mount may have failed. Cleaning directory...");
                    try
                    {
                        FileSystemHelper.DeleteDirectoryContents(mountPath);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                    return true; // Not mounted, so "unmount" succeeded
                }

                var arguments = $"/Unmount-Wim /MountDir:\"{mountPath}\" /{(commit ? "Commit" : "Discard")}";

                progress?.Report($"DISM command: dism.exe {arguments}");
                var result = await ProcessHelper.ExecuteProcessAsync(_dismPath, arguments, cancellationToken);

                progress?.Report($"DISM exit code: {result.ExitCode}");
                if (!string.IsNullOrEmpty(result.Output))
                {
                    progress?.Report($"DISM output: {result.Output.Substring(0, Math.Min(300, result.Output.Length))}");
                }
                if (!string.IsNullOrEmpty(result.Error))
                {
                    progress?.Report($"DISM error: {result.Error}");
                }

                if (result.Success)
                {
                    progress?.Report($"Successfully unmounted image");

                    // Only delete the mount directory if requested (for edition-specific mounts)
                    if (deleteMountDirectory)
                    {
                        try
                        {
                            if (Directory.Exists(mountPath))
                            {
                                progress?.Report($"Deleting temporary mount directory: {mountPath}");
                                Directory.Delete(mountPath, true);
                            }
                        }
                        catch (Exception ex)
                        {
                            progress?.Report($"⚠ Warning: Could not delete mount directory: {ex.Message}");
                            // Not a fatal error, continue
                        }
                    }
                    else
                    {
                        // For persistent mount directories, just clear contents
                        try
                        {
                            progress?.Report($"Clearing contents of persistent mount directory: {mountPath}");
                            FileSystemHelper.DeleteDirectoryContents(mountPath);
                        }
                        catch (Exception ex)
                        {
                            progress?.Report($"⚠ Warning: Could not clear mount directory: {ex.Message}");
                        }
                    }

                    return true;
                }
                else
                {
                    // If unmount fails with error 50 (not mounted), treat as success
                    if (result.ExitCode == 50)
                    {
                        progress?.Report($"⚠ Image was not mounted (error 50), cleaning directory...");
                        try
                        {
                            if (deleteMountDirectory && Directory.Exists(mountPath))
                            {
                                Directory.Delete(mountPath, true);
                            }
                            else
                            {
                                FileSystemHelper.DeleteDirectoryContents(mountPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            progress?.Report($"⚠ Warning: Could not clean mount directory: {ex.Message}");
                        }
                        return true;
                    }

                    progress?.Report($"Failed to unmount image: Exit code {result.ExitCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"Error unmounting WIM: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Add drivers to a mounted WIM image
        /// </summary>
        public async Task<bool> AddDriversAsync(
            string mountPath,
            string driverPath,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                progress?.Report($"Adding drivers from {driverPath}...");

                var arguments = $"/Image:\"{mountPath}\" /Add-Driver /Driver:\"{driverPath}\" /Recurse";

                progress?.Report($"DISM command: dism.exe {arguments}");
                var result = await ProcessHelper.ExecuteProcessAsync(_dismPath, arguments, cancellationToken);

                progress?.Report($"DISM exit code: {result.ExitCode}");
                if (!string.IsNullOrEmpty(result.Output))
                {
                    progress?.Report($"DISM output: {result.Output.Substring(0, Math.Min(500, result.Output.Length))}");
                }
                if (!string.IsNullOrEmpty(result.Error))
                {
                    progress?.Report($"DISM error: {result.Error}");
                }

                if (result.Success)
                {
                    progress?.Report("Drivers added successfully");
                    return true;
                }
                else
                {
                    // Check if it's just a warning about unsigned drivers
                    if (result.Output.Contains("successfully installed") || result.ExitCode == 0)
                    {
                        progress?.Report("Drivers added (some warnings may have occurred)");
                        return true;
                    }
                    progress?.Report($"Failed to add drivers: Exit code {result.ExitCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"Error adding drivers: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get WIM image information
        /// </summary>
        public async Task<List<WimEdition>> GetWimInfoAsync(
            string wimPath,
            CancellationToken cancellationToken = default)
        {
            var editions = new List<WimEdition>();

            try
            {
                var arguments = $"/Get-WimInfo /WimFile:\"{wimPath}\"";
                var result = await ProcessHelper.ExecuteProcessAsync(_dismPath, arguments, cancellationToken);

                if (result.Success)
                {
                    editions = ParseWimInfo(result.Output);
                }
            }
            catch
            {
                // Return empty list on error
            }

            return editions;
        }

        /// <summary>
        /// Split a WIM file into smaller SWM files for FAT32 compatibility
        /// </summary>
        public async Task<bool> SplitWimAsync(
            string wimPath,
            string outputPath,
            int fileSizeMB = 3800,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                progress?.Report($"Splitting WIM file for FAT32 compatibility...");

                var arguments = $"/Split-Image /ImageFile:\"{wimPath}\" /SWMFile:\"{outputPath}\" /FileSize:{fileSizeMB}";
                var result = await ProcessHelper.ExecuteProcessAsync(_dismPath, arguments, cancellationToken);

                if (result.Success)
                {
                    progress?.Report("WIM file split successfully");
                    return true;
                }
                else
                {
                    progress?.Report($"Failed to split WIM: {result.Error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"Error splitting WIM: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if a path has a mounted image
        /// </summary>
        public bool IsMounted(string mountPath)
        {
            try
            {
                return Directory.Exists(mountPath) && Directory.GetFiles(mountPath).Length > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Parse DISM output to extract WIM edition information
        /// </summary>
        private List<WimEdition> ParseWimInfo(string dismOutput)
        {
            var editions = new List<WimEdition>();

            try
            {
                var lines = dismOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                WimEdition? currentEdition = null;

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();

                    if (trimmedLine.StartsWith("Index :"))
                    {
                        if (currentEdition != null)
                        {
                            editions.Add(currentEdition);
                        }

                        currentEdition = new WimEdition();
                        if (int.TryParse(trimmedLine.Split(':')[1].Trim(), out var index))
                        {
                            currentEdition.ImageIndex = index;
                        }
                    }
                    else if (currentEdition != null)
                    {
                        if (trimmedLine.StartsWith("Name :"))
                        {
                            currentEdition.ImageName = trimmedLine.Split(new[] { ':' }, 2)[1].Trim();
                        }
                        else if (trimmedLine.StartsWith("Size :"))
                        {
                            var sizeStr = trimmedLine.Split(':')[1].Trim().Replace(",", "").Replace(" bytes", "");
                            if (ulong.TryParse(sizeStr, out var size))
                            {
                                currentEdition.ImageSize = size;
                            }
                        }
                        else if (trimmedLine.StartsWith("Description :"))
                        {
                            currentEdition.Description = trimmedLine.Split(new[] { ':' }, 2)[1].Trim();
                        }
                    }
                }

                if (currentEdition != null)
                {
                    editions.Add(currentEdition);
                }
            }
            catch
            {
                // Return what we've parsed so far
            }

            return editions;
        }

        /// <summary>
        /// Get list of currently mounted images
        /// </summary>
        public async Task<List<string>> GetMountedImagesAsync()
        {
            var mountedPaths = new List<string>();

            try
            {
                var result = await ProcessHelper.ExecuteProcessAsync(_dismPath, "/Get-MountedImageInfo");

                if (result.Success)
                {
                    var lines = result.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (line.Contains("Mount Dir") && line.Contains(":"))
                        {
                            var mountDir = line.Split(':')[1].Trim();
                            if (Directory.Exists(mountDir))
                            {
                                mountedPaths.Add(mountDir);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Return empty list on error
            }

            return mountedPaths;
        }
    }
}
