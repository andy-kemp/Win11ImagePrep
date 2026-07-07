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
        /// Get total size of a directory (bytes)
        /// </summary>
        private long GetDirectorySizeBytes(DirectoryInfo dir)
        {
            long size = 0;
            try
            {
                foreach (var file in dir.GetFiles("*.*", SearchOption.AllDirectories))
                {
                    try { size += file.Length; } catch { }
                }
            }
            catch { }
            return size;
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
        /// Get the volume label from an ISO file
        /// </summary>
        public async Task<string> GetIsoVolumeLabelAsync(
            string isoPath,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                progress?.Report("Reading ISO volume label...");

                // Escape single quotes for PowerShell
                var escapedPath = isoPath.Replace("'", "''");

                var arguments = $"-Command \"" +
                    $"$ErrorActionPreference='Stop'; " +
                    $"try {{ " +
                    $"$mount = Mount-DiskImage -ImagePath '{escapedPath}' -PassThru -StorageType ISO -Access ReadOnly; " +
                    $"Start-Sleep -Seconds 2; " +
                    $"$vol = Get-DiskImage -ImagePath '{escapedPath}' | Get-Volume; " +
                    $"$label = $vol.FileSystemLabel; " +
                    $"Dismount-DiskImage -ImagePath '{escapedPath}' | Out-Null; " +
                    $"if ([string]::IsNullOrEmpty($label)) {{ $label = 'WIN11USB' }}; " +
                    $"Write-Output $label; " +
                    $"exit 0 " +
                    $"}} catch {{ " +
                    $"Write-Output 'WIN11USB'; " +
                    $"exit 0 " +
                    $"}}\"";

                var result = await ProcessHelper.ExecuteProcessAsync("powershell.exe", arguments, cancellationToken);

                if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output))
                {
                    var label = result.Output.Trim();
                    progress?.Report($"ISO volume label: {label}");
                    return label;
                }

                progress?.Report("Could not read ISO label, using default");
                return "WIN11USB";
            }
            catch (Exception ex)
            {
                progress?.Report($"Error reading ISO label: {ex.Message}");
                return "WIN11USB";
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

                    // CRITICAL: Remove read-only flag from all copied files
                    // ISO files are read-only which prevents DISM from mounting WIM files
                    progress?.Report("Removing read-only flags from extracted files...");
                    try
                    {
                        var files = Directory.GetFiles(destinationPath, "*.*", SearchOption.AllDirectories);
                        int count = 0;
                        foreach (var file in files)
                        {
                            var fileInfo = new FileInfo(file);
                            if (fileInfo.IsReadOnly)
                            {
                                fileInfo.IsReadOnly = false;
                                count++;
                            }
                        }
                        progress?.Report($"✓ Cleared read-only flag on {count} files");
                    }
                    catch (Exception ex)
                    {
                        progress?.Report($"⚠ Warning: Failed to clear some read-only flags: {ex.Message}");
                    }

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

        /// <summary>
        /// Find oscdimg.exe from Windows ADK installation
        /// </summary>
        public string? FindOscdimgPath()
        {
            // 1) Check explicit override via environment variable
            var env = Environment.GetEnvironmentVariable("WINIMAGEPREP_OSCDIMG");
            if (!string.IsNullOrEmpty(env) && File.Exists(env))
                return env;

            // 2) Check common ADK installation paths
            var searchPaths = new[]
            {
                @"C:\Program Files (x86)\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\amd64\Oscdimg\oscdimg.exe",
                @"C:\Program Files (x86)\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\x86\Oscdimg\oscdimg.exe",
                @"C:\Program Files\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\amd64\Oscdimg\oscdimg.exe",
                @"C:\Program Files\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\x86\Oscdimg\oscdimg.exe",
                @"C:\Program Files (x86)\Windows Kits\11\Assessment and Deployment Kit\Deployment Tools\amd64\Oscdimg\oscdimg.exe",
                @"C:\Program Files\Windows Kits\11\Assessment and Deployment Kit\Deployment Tools\amd64\Oscdimg\oscdimg.exe",
                // Additional possible subfolders added by some ADK installs
                @"C:\Program Files (x86)\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\arm\Oscdimg\oscdimg.exe",
                @"C:\Program Files (x86)\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\arm64\Oscdimg\oscdimg.exe"
            };

            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                    return path;
            }

            // 3) Search PATH environment for oscdimg.exe
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                foreach (var part in pathEnv.Split(';'))
                {
                    try
                    {
                        var candidate = Path.Combine(part.Trim(), "oscdimg.exe");
                        if (File.Exists(candidate))
                            return candidate;
                    }
                    catch { }
                }
            }

            // 4) Last resort: do a quick probe under Program Files (x86)\Windows Kits
            try
            {
                var kits = new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) };
                foreach (var root in kits)
                {
                    if (string.IsNullOrEmpty(root)) continue;
                    var candidates = Directory.EnumerateFiles(root, "oscdimg.exe", SearchOption.AllDirectories).Take(5);
                    var first = candidates.FirstOrDefault();
                    if (!string.IsNullOrEmpty(first))
                        return first;
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Create a bootable ISO from a directory using oscdimg
        /// </summary>
        public async Task<bool> CreateBootableIsoAsync(
            string sourceDirectory,
            string outputIsoPath,
            string volumeLabel,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                progress?.Report("Searching for oscdimg.exe...");

                var oscdimgPath = FindOscdimgPath();
                if (string.IsNullOrEmpty(oscdimgPath))
                {
                    progress?.Report("✗ oscdimg.exe not found!");
                    progress?.Report("Please install Windows ADK (Assessment and Deployment Kit)");
                    progress?.Report("Download from: https://docs.microsoft.com/en-us/windows-hardware/get-started/adk-install");
                    return false;
                }

                progress?.Report($"✓ Found oscdimg at: {oscdimgPath}");

                // Validate source directory
                if (!Directory.Exists(sourceDirectory))
                {
                    progress?.Report($"✗ Source directory not found: {sourceDirectory}");
                    return false;
                }

                // Check for required boot files
                var bootDir = Path.Combine(sourceDirectory, "boot");
                var efiBootDir = Path.Combine(sourceDirectory, "efi", "microsoft", "boot");

                // Check for UEFI boot file (efisys.bin or efisys_noprompt.bin)
                var efisysPath = Path.Combine(bootDir, "efisys.bin");
                var efisysNopromptPath = Path.Combine(bootDir, "efisys_noprompt.bin");

                string? bootFile = null;
                if (File.Exists(efisysPath))
                {
                    bootFile = efisysPath;
                    progress?.Report($"✓ Found UEFI boot file: efisys.bin");
                }
                else if (File.Exists(efisysNopromptPath))
                {
                    bootFile = efisysNopromptPath;
                    progress?.Report($"✓ Found UEFI boot file: efisys_noprompt.bin");
                }
                else
                {
                    progress?.Report($"✗ Boot file not found at: {efisysPath}");
                    progress?.Report($"✗ Also checked: {efisysNopromptPath}");
                    progress?.Report($"Boot directory exists: {Directory.Exists(bootDir)}");
                    if (Directory.Exists(bootDir))
                    {
                        var bootFiles = Directory.GetFiles(bootDir);
                        progress?.Report($"Files in boot directory:");
                        foreach (var file in bootFiles)
                        {
                            progress?.Report($"  - {Path.GetFileName(file)}");
                        }
                    }
                    progress?.Report("The source directory must contain Windows boot files");
                    return false;
                }

                // Check for legacy BIOS boot sector (etfsboot.com)
                var etfsbootPath = Path.Combine(bootDir, "etfsboot.com");
                if (!File.Exists(etfsbootPath))
                {
                    progress?.Report($"⚠ Warning: Legacy BIOS boot file not found: {etfsbootPath}");
                    progress?.Report($"ISO will be UEFI-only");
                }

                progress?.Report($"✓ Source directory validated");
                progress?.Report($"Creating bootable ISO: {Path.GetFileName(outputIsoPath)}");
                progress?.Report("This may take several minutes...");

                // Ensure output directory exists and is writable
                var outputDir = Path.GetDirectoryName(outputIsoPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                // Check free disk space on the drive containing the output path
                try
                {
                    var drive = new DriveInfo(Path.GetPathRoot(outputDir ?? Path.GetTempPath()));
                    // Rough estimate: require at least 1.2x the size of the source directory
                    long requiredBytes = 0;
                    try
                    {
                        requiredBytes = GetDirectorySizeBytes(new DirectoryInfo(sourceDirectory));
                    }
                    catch { requiredBytes = 10L * 1024 * 1024 * 1024; } // fallback 10GB

                    long requiredEstimate = (long)(requiredBytes * 1.2);
                    progress?.Report($"Checking disk space: available {drive.AvailableFreeSpace / (1024*1024)} MB, required ~{requiredEstimate / (1024*1024)} MB");
                    if (drive.AvailableFreeSpace < requiredEstimate)
                    {
                        progress?.Report("✗ Insufficient disk space for ISO creation");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    progress?.Report($"⚠ Warning: Failed to check disk space: {ex.Message}");
                }

                // Build oscdimg command for UEFI/BIOS hybrid boot
                // -m = ignore max size limit
                // -o = optimize storage
                // -u2 = UDF file system
                // -udfver102 = UDF version 1.02
                // -bootdata = boot configuration
                string arguments;

                if (File.Exists(etfsbootPath))
                {
                    // Hybrid UEFI + BIOS boot
                    //   2 = two boot images
                    //   #p0 = first boot image (BIOS) 
                    //   #pEF = second boot image (UEFI)
                    arguments = $"-m -o -u2 -udfver102 " +
                        $"-l\"{volumeLabel}\" " +
                        $"-bootdata:2#p0,e,b\"{etfsbootPath}\"#pEF,e,b\"{bootFile}\" " +
                        $"\"{sourceDirectory}\" \"{outputIsoPath}\"";
                    progress?.Report("Creating hybrid UEFI + BIOS bootable ISO");
                }
                else
                {
                    // UEFI-only boot
                    arguments = $"-m -o -u2 -udfver102 " +
                        $"-l\"{volumeLabel}\" " +
                        $"-bootdata:1#pEF,e,b\"{bootFile}\" " +
                        $"\"{sourceDirectory}\" \"{outputIsoPath}\"";
                    progress?.Report("Creating UEFI-only bootable ISO");
                }

                progress?.Report($"Running oscdimg...");
                Logger.Info($"oscdimg command: {oscdimgPath} {arguments}");

                var result = await ProcessHelper.ExecuteProcessAsync(oscdimgPath, arguments, cancellationToken);

                // Parse oscdimg output for common errors
                if (!string.IsNullOrEmpty(result.Error))
                {
                    // Common patterns
                    var stderr = result.Error.ToLowerInvariant();
                    if (stderr.Contains("access is denied") || stderr.Contains("permission denied") || stderr.Contains("access denied"))
                    {
                        progress?.Report("✗ oscdimg reported access denied. Check output path permissions and run as administrator if required.");
                    }
                    else if (stderr.Contains("no such file or directory") || stderr.Contains("cannot find the file"))
                    {
                        progress?.Report("✗ oscdimg reported missing files. Verify the source directory contains expected boot files.");
                    }
                    else if (stderr.Contains("file too large") || stderr.Contains("no space left on device") || stderr.Contains("out of disk space"))
                    {
                        progress?.Report("✗ oscdimg reported insufficient disk space. Free up disk or choose another output drive.");
                    }
                }

                if (result.Success)
                {
                    progress?.Report("✓ ISO created successfully!");
                    progress?.Report($"Output: {outputIsoPath}");

                    // Verify the ISO was created
                    if (File.Exists(outputIsoPath))
                    {
                        var fileInfo = new FileInfo(outputIsoPath);
                        var sizeMB = fileInfo.Length / (1024.0 * 1024.0);
                        progress?.Report($"ISO size: {sizeMB:F2} MB");
                        return true;
                    }
                    else
                    {
                        progress?.Report("✗ ISO file was not created");
                        return false;
                    }
                }
                else
                {
                    progress?.Report($"✗ oscdimg failed with exit code {result.ExitCode}");
                    if (!string.IsNullOrEmpty(result.Output))
                    {
                        progress?.Report($"Output: {result.Output}");
                    }
                    if (!string.IsNullOrEmpty(result.Error))
                    {
                        progress?.Report($"Error: {result.Error}");
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"✗ Error creating ISO: {ex.Message}");
                Logger.Error($"CreateBootableIsoAsync exception: {ex}");
                return false;
            }
        }
    }
}
