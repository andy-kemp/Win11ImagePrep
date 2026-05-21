using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinImagePrep.Helpers;
using WinImagePrep.Models;

namespace WinImagePrep.Services
{
    public class DriverService
    {
        /// <summary>
        /// Extract drivers from MSI file
        /// </summary>
        public async Task<bool> ExtractDriverMsiAsync(
            string msiPath,
            string destinationPath,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (!File.Exists(msiPath))
                {
                    progress?.Report($"MSI file not found: {msiPath}");
                    return false;
                }

                progress?.Report($"Extracting drivers from MSI: {Path.GetFileName(msiPath)}");

                // Clean destination directory
                if (Directory.Exists(destinationPath))
                {
                    progress?.Report("Cleaning previous driver extraction...");
                    FileSystemHelper.DeleteDirectoryContents(destinationPath);
                }
                else
                {
                    Directory.CreateDirectory(destinationPath);
                }

                // Extract MSI using msiexec
                var arguments = $"/a \"{msiPath}\" /qn TARGETDIR=\"{destinationPath}\"";
                var result = await ProcessHelper.ExecuteProcessAsync("msiexec.exe", arguments, cancellationToken);

                if (result.Success)
                {
                    progress?.Report("MSI extraction completed");
                    return true;
                }
                else
                {
                    progress?.Report($"Failed to extract MSI: {result.Error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"Error extracting MSI: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Validate driver directory
        /// </summary>
        public DriverValidationResult ValidateDrivers(string driverPath)
        {
            var result = new DriverValidationResult
            {
                IsValid = false
            };

            try
            {
                if (!Directory.Exists(driverPath))
                {
                    result.Message = "Driver directory does not exist";
                    return result;
                }

                // Find all .inf files
                var infFiles = Directory.GetFiles(driverPath, "*.inf", SearchOption.AllDirectories);
                result.DriverCount = infFiles.Length;

                if (infFiles.Length == 0)
                {
                    result.Message = "No driver INF files found";
                    return result;
                }

                // Check each driver
                foreach (var infFile in infFiles)
                {
                    var driverInfo = new DriverInfo
                    {
                        Path = infFile,
                        Name = Path.GetFileName(infFile)
                    };

                    // Check for catalog file (indicates signed driver)
                    var catFile = Path.ChangeExtension(infFile, ".cat");
                    driverInfo.IsSigned = File.Exists(catFile);

                    if (driverInfo.IsSigned)
                    {
                        result.SignedCount++;
                    }
                    else
                    {
                        result.UnsignedCount++;
                    }

                    // Try to extract basic info from INF file
                    try
                    {
                        var infContent = File.ReadAllText(infFile);

                        // Extract provider
                        var providerLine = infContent.Split('\n')
                            .FirstOrDefault(l => l.Trim().StartsWith("Provider=", StringComparison.OrdinalIgnoreCase));
                        if (providerLine != null)
                        {
                            driverInfo.Provider = providerLine.Split('=')[1].Trim().Trim('%', '"');
                        }

                        // Extract class
                        var classLine = infContent.Split('\n')
                            .FirstOrDefault(l => l.Trim().StartsWith("Class=", StringComparison.OrdinalIgnoreCase));
                        if (classLine != null)
                        {
                            driverInfo.DriverClass = classLine.Split('=')[1].Trim().Trim('%', '"');
                        }

                        // Extract version
                        var versionLine = infContent.Split('\n')
                            .FirstOrDefault(l => l.Trim().StartsWith("DriverVer=", StringComparison.OrdinalIgnoreCase));
                        if (versionLine != null)
                        {
                            driverInfo.Version = versionLine.Split('=')[1].Trim();
                        }
                    }
                    catch
                    {
                        // Continue if we can't parse the INF
                    }

                    result.Drivers.Add(driverInfo);
                }

                result.IsValid = result.DriverCount > 0;
                result.Message = $"Found {result.DriverCount} driver(s): {result.SignedCount} signed, {result.UnsignedCount} unsigned";
            }
            catch (Exception ex)
            {
                result.Message = $"Error validating drivers: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// Check if a file is a valid MSI
        /// </summary>
        public bool IsValidMsiFile(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return false;

                var extension = Path.GetExtension(path).ToLowerInvariant();
                return extension == ".msi";
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get driver statistics for display
        /// </summary>
        public string GetDriverSummary(DriverValidationResult validation)
        {
            if (!validation.IsValid)
            {
                return "No valid drivers found";
            }

            var summary = $"{validation.DriverCount} driver(s) found\n";
            summary += $"Signed: {validation.SignedCount}\n";
            summary += $"Unsigned: {validation.UnsignedCount}";

            if (validation.Drivers.Any())
            {
                var uniqueClasses = validation.Drivers
                    .Where(d => !string.IsNullOrEmpty(d.DriverClass))
                    .Select(d => d.DriverClass)
                    .Distinct()
                    .Take(5);

                if (uniqueClasses.Any())
                {
                    summary += $"\n\nDriver Classes:\n{string.Join(", ", uniqueClasses)}";
                }
            }

            return summary;
        }
    }
}
