using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinImagePrep.Helpers;
using WinImagePrep.Models;

namespace WinImagePrep.Services
{
    public class IsoService
    {
        /// <summary>
        /// Mount an ISO file and return the drive letter
        /// </summary>
        public async Task<string?> MountIsoAsync(
            string isoPath,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                progress?.Report($"Mounting ISO: {Path.GetFileName(isoPath)}");
                progress?.Report($"Full ISO path: {isoPath}");

                // Verify the file exists first
                if (!File.Exists(isoPath))
                {
                    progress?.Report($"✗ ERROR: ISO file does not exist at: {isoPath}");
                    return null;
                }

                // Escape single quotes in the path for PowerShell
                var escapedPath = isoPath.Replace("'", "''");

                // Use Mount-DiskImage which persists the mount
                // The key is that once mounted via Mount-DiskImage, it stays mounted until explicitly dismounted
                var arguments = $"-Command \"" +
                    $"$ErrorActionPreference='Stop'; " +
                    $"try {{ " +
                    $"$mount = Mount-DiskImage -ImagePath '{escapedPath}' -PassThru -StorageType ISO -Access ReadOnly; " +
                    $"Start-Sleep -Seconds 3; " +
                    $"$vol = Get-DiskImage -ImagePath '{escapedPath}' | Get-Volume; " +
                    $"$driveLetter = $vol.DriveLetter; " +
                    $"if ([string]::IsNullOrEmpty($driveLetter)) {{ throw 'No drive letter assigned' }}; " +
                    $"Write-Output $driveLetter; " +
                    $"exit 0 " +
                    $"}} catch {{ " +
                    $"Write-Error $_.Exception.Message; " +
                    $"exit 1 " +
                    $"}}\"";

                progress?.Report($"Executing mount command...");
                var result = await ProcessHelper.ExecuteProcessAsync("powershell.exe", arguments, cancellationToken);

                progress?.Report($"Mount result: ExitCode={result.ExitCode}, Success={result.Success}");
                if (!string.IsNullOrEmpty(result.Output))
                {
                    progress?.Report($"Mount output: [{result.Output.Trim()}]");
                }
                if (!string.IsNullOrEmpty(result.Error))
                {
                    progress?.Report($"Mount stderr: {result.Error}");
                }

                if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output))
                {
                    var driveLetter = result.Output.Trim();

                    // Validate it's a single letter
                    if (driveLetter.Length == 1 && char.IsLetter(driveLetter[0]))
                    {
                        progress?.Report($"ISO mounted to drive {driveLetter}:");

                        // Give it a moment to stabilize
                        await Task.Delay(2000, cancellationToken);

                        // Verify the drive is accessible
                        var drivePath = $"{driveLetter}:\\";
                        if (Directory.Exists(drivePath))
                        {
                            try
                            {
                                // Try to access the Sources directory (should exist on Windows ISO)
                                var sourcesPath = Path.Combine(drivePath, "sources");
                                if (Directory.Exists(sourcesPath))
                                {
                                    progress?.Report($"✓ Drive {driveLetter}: is accessible and contains Windows files");
                                    return driveLetter;
                                }
                                else
                                {
                                    progress?.Report($"⚠ Drive {driveLetter}: accessible but missing 'sources' folder");
                                    // Still return it, maybe it's a different ISO structure
                                    return driveLetter;
                                }
                            }
                            catch (Exception ex)
                            {
                                progress?.Report($"⚠ Drive {driveLetter}: accessible but error reading: {ex.Message}");
                                return driveLetter; // Still return it
                            }
                        }
                        else
                        {
                            progress?.Report($"✗ Drive {driveLetter}: mount succeeded but drive not accessible");
                        }
                    }
                    else
                    {
                        progress?.Report($"✗ Invalid drive letter format: [{driveLetter}] (length={driveLetter.Length})");
                    }
                }
                else
                {
                    progress?.Report("✗ Mount command failed or returned no output");
                }

                return null;
            }
            catch (Exception ex)
            {
                progress?.Report($"✗ Exception during mount: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Dismount an ISO file
        /// </summary>
        public async Task<bool> DismountIsoAsync(
            string isoPath,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                progress?.Report($"Dismounting ISO: {Path.GetFileName(isoPath)}");

                // Escape single quotes for PowerShell
                var escapedPath = isoPath.Replace("'", "''");

                var arguments = $"-Command \"Dismount-DiskImage -ImagePath '{escapedPath}'\"";
                var result = await ProcessHelper.ExecuteProcessAsync("powershell.exe", arguments, cancellationToken);

                if (result.Success)
                {
                    progress?.Report("ISO dismounted successfully");
                    return true;
                }

                progress?.Report($"Dismount returned exit code: {result.ExitCode}");
                if (!string.IsNullOrEmpty(result.Error))
                {
                    progress?.Report($"Dismount error: {result.Error}");
                }

                return false;
            }
            catch (Exception ex)
            {
                progress?.Report($"Error dismounting ISO: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Validate ISO integrity and check for required WIM files
        /// </summary>
        public async Task<IsoValidationResult> ValidateIsoAsync(
            string isoPath,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new IsoValidationResult
            {
                IsoPath = isoPath,
                IsValid = false
            };

            try
            {
                if (!File.Exists(isoPath))
                {
                    result.Message = "ISO file does not exist";
                    return result;
                }

                progress?.Report("Validating ISO...");

                // Mount the ISO
                var driveLetter = await MountIsoAsync(isoPath, progress, cancellationToken);
                if (string.IsNullOrEmpty(driveLetter))
                {
                    result.Message = "Failed to mount ISO";
                    return result;
                }

                result.MountedDriveLetter = driveLetter;

                // Check for required files
                var bootWimPath = $"{driveLetter}:\\Sources\\boot.wim";
                var installWimPath = $"{driveLetter}:\\Sources\\install.wim";

                result.HasBootWim = File.Exists(bootWimPath);
                result.HasInstallWim = File.Exists(installWimPath);
                result.IsValid = result.HasBootWim && result.HasInstallWim;

                if (!result.IsValid)
                {
                    result.Message = "Missing required WIM files (boot.wim or install.wim)";
                }
                else
                {
                    result.Message = "ISO is valid";
                }

                // Dismount the ISO
                await DismountIsoAsync(isoPath, progress, cancellationToken);
            }
            catch (Exception ex)
            {
                result.Message = $"Error validating ISO: {ex.Message}";
                // Try to dismount in case of error
                try
                {
                    await DismountIsoAsync(isoPath, null, cancellationToken);
                }
                catch { }
            }

            return result;
        }

        /// <summary>
        /// Extract ISO contents to a directory
        /// </summary>
        public async Task<bool> ExtractIsoAsync(
            string isoPath,
            string destinationPath,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                progress?.Report("Extracting ISO contents...");

                // Mount the ISO
                var driveLetter = await MountIsoAsync(isoPath, progress, cancellationToken);
                if (string.IsNullOrEmpty(driveLetter))
                {
                    return false;
                }

                // Clean destination directory
                if (Directory.Exists(destinationPath))
                {
                    progress?.Report("Cleaning destination directory...");
                    FileSystemHelper.DeleteDirectoryContents(destinationPath);
                }
                else
                {
                    Directory.CreateDirectory(destinationPath);
                }

                // Copy files using robocopy
                progress?.Report("Copying files from ISO...");

                // Verify the drive is still accessible before copying
                var sourcePath = $"{driveLetter}:";  // No trailing backslash to avoid escaping quote
                if (!Directory.Exists(sourcePath + "\\"))
                {
                    progress?.Report($"✗ ERROR: Drive {driveLetter}: is no longer accessible!");
                    await DismountIsoAsync(isoPath, progress, cancellationToken);
                    return false;
                }

                var sourcesCheck = Path.Combine(sourcePath + "\\", "sources");
                if (!Directory.Exists(sourcesCheck))
                {
                    progress?.Report($"✗ WARNING: Drive {driveLetter}: missing 'sources' folder - may not be a valid Windows ISO");
                }

                progress?.Report($"✓ Drive {driveLetter}: verified accessible, starting copy...");

                // Robocopy source format: D: (no trailing backslash)
                // Robocopy will add the backslash automatically
                var arguments = $"\"{sourcePath}\" \"{destinationPath}\" /E /COPY:DAT /R:1 /W:1";

                progress?.Report($"Source: {sourcePath}");
                progress?.Report($"Destination: {destinationPath}");
                progress?.Report($"Starting robocopy...");

                var result = await ProcessHelper.ExecuteProcessAsync("robocopy.exe", arguments, cancellationToken);

                // Log output for debugging
                if (!string.IsNullOrEmpty(result.Output))
                {
                    progress?.Report($"Robocopy output: {result.Output.Substring(0, Math.Min(500, result.Output.Length))}");
                }
                if (!string.IsNullOrEmpty(result.Error))
                {
                    progress?.Report($"Robocopy error: {result.Error}");
                }

                // Robocopy exit codes: 0-7 are success, 8+ are errors
                var success = result.ExitCode >= 0 && result.ExitCode < 8;

                // Dismount the ISO
                await DismountIsoAsync(isoPath, progress, cancellationToken);

                if (success)
                {
                    progress?.Report("ISO extracted successfully");
                    return true;
                }
                else
                {
                    progress?.Report($"Failed to extract ISO (exit code: {result.ExitCode})");
                    if (!string.IsNullOrEmpty(result.Error))
                    {
                        progress?.Report($"Error details: {result.Error}");
                    }
                    if (!string.IsNullOrEmpty(result.Output))
                    {
                        progress?.Report($"Output: {result.Output}");
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"Error extracting ISO: {ex.Message}");
                // Try to dismount in case of error
                try
                {
                    await DismountIsoAsync(isoPath, null, cancellationToken);
                }
                catch { }
                return false;
            }
        }

        /// <summary>
        /// Check if a file is a valid ISO
        /// </summary>
        public bool IsValidIsoFile(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return false;

                var extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension != ".iso")
                    return false;

                // Check file size (should be at least 1GB for Windows ISO)
                var fileInfo = new FileInfo(path);
                return fileInfo.Length > 1024L * 1024L * 1024L; // > 1GB
            }
            catch
            {
                return false;
            }
        }
    }
}
