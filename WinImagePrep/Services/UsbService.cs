using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using WinImagePrep.Helpers;
using WinImagePrep.Models;

namespace WinImagePrep.Services
{
    public class UsbService
    {
        /// <summary>
        /// Get all USB drives
        /// </summary>
        public List<UsbDriveInfo> GetUsbDrives()
        {
            var usbDrives = new List<UsbDriveInfo>();

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_DiskDrive WHERE InterfaceType='USB'");

                foreach (ManagementObject drive in searcher.Get())
                {
                    try
                    {
                        var usbInfo = new UsbDriveInfo
                        {
                            DiskNumber = Convert.ToUInt32(drive["Index"]),
                            FriendlyName = drive["Caption"]?.ToString() ?? "Unknown USB Drive",
                            SizeBytes = Convert.ToUInt64(drive["Size"]),
                            MediaType = drive["MediaType"]?.ToString() ?? "Unknown",
                            InterfaceType = drive["InterfaceType"]?.ToString() ?? "USB",
                            IsRemovable = true
                        };

                        // Get partition info
                        using var partitionSearcher = new ManagementObjectSearcher(
                            $"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{drive["DeviceID"]}'}} " +
                            "WHERE AssocClass=Win32_DiskDriveToDiskPartition");

                        foreach (ManagementObject partition in partitionSearcher.Get())
                        {
                            using var logicalSearcher = new ManagementObjectSearcher(
                                $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} " +
                                "WHERE AssocClass=Win32_LogicalDiskToPartition");

                            foreach (ManagementObject logical in logicalSearcher.Get())
                            {
                                usbInfo.FileSystem = logical["FileSystem"]?.ToString() ?? "Unknown";
                                usbInfo.Label = logical["VolumeName"]?.ToString() ?? "";
                                break; // Take first partition
                            }
                            break; // Take first partition
                        }

                        usbDrives.Add(usbInfo);
                    }
                    catch
                    {
                        // Continue with next drive
                    }
                }
            }
            catch
            {
                // Return what we have
            }

            return usbDrives;
        }

        /// <summary>
        /// Create bootable Windows USB drive
        /// </summary>
        public async Task<bool> CreateBootableUsbAsync(
            uint diskNumber,
            string sourcePath,
            string volumeLabel,
            IProgress<OperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Validate volume label
                volumeLabel = SanitizeVolumeLabel(volumeLabel);

                // Report progress
                ReportProgress(progress, 5, "Preparing USB drive...", OperationStage.CreatingUSB);

                // Clean the disk
                if (!await CleanDiskAsync(diskNumber, progress, cancellationToken))
                {
                    return false;
                }

                ReportProgress(progress, 20, "Initializing disk...", OperationStage.CreatingUSB);

                // Initialize disk as MBR
                if (!await InitializeDiskAsync(diskNumber, progress, cancellationToken))
                {
                    return false;
                }

                ReportProgress(progress, 30, "Creating partition...", OperationStage.CreatingUSB);

                // Create partition
                var driveLetter = await CreatePartitionAsync(diskNumber, progress, cancellationToken);
                if (string.IsNullOrEmpty(driveLetter))
                {
                    return false;
                }

                ReportProgress(progress, 45, $"Formatting as FAT32 (Drive {driveLetter}:)...", OperationStage.CreatingUSB);

                // Format as FAT32
                if (!await FormatPartitionAsync(driveLetter, volumeLabel, progress, cancellationToken))
                {
                    return false;
                }

                ReportProgress(progress, 60, "Copying Windows files to USB...", OperationStage.CreatingUSB);

                // Copy files
                if (!await CopyFilesToUsbAsync(sourcePath, $"{driveLetter}:\\", progress, cancellationToken))
                {
                    return false;
                }

                ReportProgress(progress, 100, "USB creation complete!", OperationStage.Complete);
                return true;
            }
            catch (Exception ex)
            {
                ReportProgress(progress, 0, $"Error: {ex.Message}", OperationStage.Error);
                return false;
            }
        }

        /// <summary>
        /// Clean all partitions from a disk
        /// </summary>
        private async Task<bool> CleanDiskAsync(
            uint diskNumber,
            IProgress<OperationProgress>? progress,
            CancellationToken cancellationToken)
        {
            try
            {
                var script = $@"
                    Get-Disk -Number {diskNumber} | Clear-Disk -RemoveData -Confirm:$false -ErrorAction SilentlyContinue
                    Start-Sleep -Seconds 2
                ";

                var result = await ProcessHelper.ExecuteProcessAsync(
                    "powershell.exe",
                    $"-Command \"{script}\"",
                    cancellationToken);

                return result.ExitCode == 0 || result.Success;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Initialize disk with MBR partition style
        /// </summary>
        private async Task<bool> InitializeDiskAsync(
            uint diskNumber,
            IProgress<OperationProgress>? progress,
            CancellationToken cancellationToken)
        {
            try
            {
                var script = $@"
                    $disk = Get-Disk -Number {diskNumber}
                    if ($disk.PartitionStyle -eq 'RAW') {{
                        Initialize-Disk -Number {diskNumber} -PartitionStyle MBR -ErrorAction Stop
                    }}
                    Start-Sleep -Seconds 2
                ";

                var result = await ProcessHelper.ExecuteProcessAsync(
                    "powershell.exe",
                    $"-Command \"{script}\"",
                    cancellationToken);

                return result.ExitCode == 0 || result.Success;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Create a new partition on the disk
        /// </summary>
        private async Task<string?> CreatePartitionAsync(
            uint diskNumber,
            IProgress<OperationProgress>? progress,
            CancellationToken cancellationToken)
        {
            try
            {
                var script = $@"
                    $partition = New-Partition -DiskNumber {diskNumber} -Size 14GB -AssignDriveLetter
                    Start-Sleep -Seconds 2
                    $driveLetter = ($partition | Get-Volume).DriveLetter
                    Write-Output $driveLetter
                ";

                var result = await ProcessHelper.ExecuteProcessAsync(
                    "powershell.exe",
                    $"-Command \"{script}\"",
                    cancellationToken);

                if (result.Success && !string.IsNullOrWhiteSpace(result.Output))
                {
                    return result.Output.Trim();
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Format partition as FAT32
        /// </summary>
        private async Task<bool> FormatPartitionAsync(
            string driveLetter,
            string label,
            IProgress<OperationProgress>? progress,
            CancellationToken cancellationToken)
        {
            try
            {
                var script = $@"
                    Get-Volume -DriveLetter {driveLetter} | Format-Volume -FileSystem FAT32 -NewFileSystemLabel '{label}' -Confirm:$false
                    Start-Sleep -Seconds 3
                ";

                var result = await ProcessHelper.ExecuteProcessAsync(
                    "powershell.exe",
                    $"-Command \"{script}\"",
                    cancellationToken);

                return result.ExitCode == 0 || result.Success;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Copy files to USB drive
        /// </summary>
        private async Task<bool> CopyFilesToUsbAsync(
            string sourcePath,
            string destinationPath,
            IProgress<OperationProgress>? progress,
            CancellationToken cancellationToken)
        {
            try
            {
                // Remove trailing backslash from destination
                destinationPath = destinationPath.TrimEnd('\\');

                Logger.Info($"Copying files from {sourcePath} to {destinationPath}");
                ReportProgress(progress, 65, $"Copying files to {destinationPath}...", OperationStage.CreatingUSB);

                var arguments = $"\"{sourcePath}\" \"{destinationPath}\" /E /R:3 /W:5";
                Logger.Info($"Robocopy command: robocopy.exe {arguments}");

                var result = await ProcessHelper.ExecuteProcessAsync("robocopy.exe", arguments, cancellationToken);

                Logger.Info($"Robocopy exit code: {result.ExitCode}");
                if (!string.IsNullOrEmpty(result.Output))
                {
                    Logger.Info($"Robocopy output: {result.Output.Substring(0, Math.Min(500, result.Output.Length))}");
                }
                if (!string.IsNullOrEmpty(result.Error))
                {
                    Logger.Error($"Robocopy error: {result.Error}");
                }

                ReportProgress(progress, 95, "File copy complete, finalizing...", OperationStage.CreatingUSB);

                // Robocopy exit codes 0-7 are success
                // 0 = No files copied (files already exist)
                // 1 = Files copied successfully
                // 2 = Extra files or directories detected
                // 3 = Files copied + extra files
                // 4 = Mismatched files
                // 5 = Files copied + mismatched files
                // 6 = Extra + mismatched files
                // 7 = Files copied + extra + mismatched files
                // 8+ = Errors
                bool success = result.ExitCode >= 0 && result.ExitCode < 8;
                Logger.Info($"Robocopy result: {(success ? "Success" : "Failed")}");
                return success;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error copying files to USB: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sanitize volume label for FAT32
        /// </summary>
        private string SanitizeVolumeLabel(string label)
        {
            // FAT32 labels must be 11 characters or less
            if (label.Length > 11)
            {
                label = label.Substring(0, 11);
            }

            // Remove invalid characters
            var invalidChars = new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' };
            foreach (var c in invalidChars)
            {
                label = label.Replace(c, '_');
            }

            return string.IsNullOrWhiteSpace(label) ? "WIN11USB" : label;
        }

        /// <summary>
        /// Report progress helper
        /// </summary>
        private void ReportProgress(
            IProgress<OperationProgress>? progress,
            int percent,
            string message,
            OperationStage stage)
        {
            progress?.Report(new OperationProgress
            {
                PercentComplete = percent,
                CurrentOperation = message,
                StatusMessage = message,
                Stage = stage
            });
        }
    }
}
