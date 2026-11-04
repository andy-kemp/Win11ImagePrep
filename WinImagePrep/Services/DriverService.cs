using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
        /// Extract drivers from ZIP file
        /// </summary>
        public async Task<bool> ExtractDriverZipAsync(
            string zipPath,
            string destinationPath,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(zipPath))
                    {
                        progress?.Report($"ZIP file not found: {zipPath}");
                        return false;
                    }

                    progress?.Report($"Extracting drivers from ZIP: {Path.GetFileName(zipPath)}");

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

                    // Extract ZIP using System.IO.Compression
                    ZipFile.ExtractToDirectory(zipPath, destinationPath, overwriteFiles: true);

                    progress?.Report("ZIP extraction completed");
                    return true;
                }
                catch (Exception ex)
                {
                    progress?.Report($"Error extracting ZIP: {ex.Message}");
                    return false;
                }
            }, cancellationToken);
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

        /// <summary>
        /// Extract every driver source exactly once into a staging folder.
        /// Folder sources are used in place. Returns the folders ready for DISM /Add-Driver /Recurse.
        /// </summary>
        public async Task<List<string>> StageDriverSourcesAsync(
            IReadOnlyList<DriverSourceInfo> sources,
            string stagingRoot,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var staged = new List<string>();
            FileSystemHelper.EnsureDirectoryExists(stagingRoot);

            for (int i = 0; i < sources.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var source = sources[i];
                var label = $"[{i + 1}/{sources.Count}] {source.DisplayName}";

                switch (source.Type)
                {
                    case DriverSourceType.Folder:
                        if (Directory.Exists(source.Path))
                        {
                            progress?.Report($"{label}: using folder in place");
                            staged.Add(source.Path);
                        }
                        else
                        {
                            progress?.Report($"✗ {label}: folder not found");
                        }
                        break;

                    case DriverSourceType.Msi:
                    case DriverSourceType.Zip:
                        var target = Path.Combine(stagingRoot, $"{i + 1:D2}_{FileSystemHelper.GetSafeFileName(Path.GetFileNameWithoutExtension(source.Path))}");
                        progress?.Report($"{label}: extracting (once)...");
                        var ok = source.Type == DriverSourceType.Msi
                            ? await ExtractDriverMsiAsync(source.Path, target, progress, cancellationToken)
                            : await ExtractDriverZipAsync(source.Path, target, progress, cancellationToken);
                        if (ok && Directory.Exists(target))
                        {
                            staged.Add(target);
                        }
                        else
                        {
                            progress?.Report($"✗ {label}: extraction failed, skipping");
                        }
                        break;
                }
            }

            return staged;
        }

        /// <summary>
        /// Build a reduced driver set for boot images (WinPE, Setup, WinRE) containing only INFs
        /// whose class is in <paramref name="bootClasses"/>. Each matching driver package folder is
        /// copied, and non-matching INFs that share the folder are removed so DISM skips them.
        /// Returns (folder, infCount); infCount is 0 when nothing matched.
        /// </summary>
        public async Task<(string Folder, int InfCount, int TotalInfs)> BuildBootDriverSetAsync(
            IReadOnlyList<string> stagedFolders,
            IEnumerable<string> bootClasses,
            string bootSetRoot,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var wanted = new HashSet<string>(bootClasses, StringComparer.OrdinalIgnoreCase);
                if (Directory.Exists(bootSetRoot))
                {
                    FileSystemHelper.DeleteDirectoryContents(bootSetRoot);
                }
                FileSystemHelper.EnsureDirectoryExists(bootSetRoot);

                int matched = 0, total = 0, pkg = 0;
                var classCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (var root in stagedFolders)
                {
                    // Group INFs by package folder so each folder is copied at most once
                    var byFolder = Directory.EnumerateFiles(root, "*.inf", SearchOption.AllDirectories)
                        .GroupBy(f => Path.GetDirectoryName(f)!, StringComparer.OrdinalIgnoreCase);

                    foreach (var folder in byFolder)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var infs = folder.ToList();
                        total += infs.Count;

                        var keep = new List<string>();
                        foreach (var inf in infs)
                        {
                            var cls = ReadInfClass(inf);
                            if (cls != null && wanted.Contains(cls))
                            {
                                keep.Add(Path.GetFileName(inf));
                                classCounts[cls] = classCounts.TryGetValue(cls, out var n) ? n + 1 : 1;
                            }
                        }
                        if (keep.Count == 0) continue;

                        var dest = Path.Combine(bootSetRoot, $"{++pkg:D4}");
                        CopyDriverPackage(folder.Key, dest);

                        // Remove INFs we don't want in boot images (they share this folder)
                        foreach (var inf in Directory.EnumerateFiles(dest, "*.inf", SearchOption.TopDirectoryOnly).ToList())
                        {
                            if (!keep.Contains(Path.GetFileName(inf), StringComparer.OrdinalIgnoreCase))
                            {
                                File.Delete(inf);
                            }
                        }
                        matched += keep.Count;
                    }
                }

                var breakdown = string.Join(", ", classCounts.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}"));
                progress?.Report($"Boot driver set: {matched} of {total} INFs ({breakdown})");
                return (bootSetRoot, matched, total);
            }, cancellationToken);
        }

        /// <summary>
        /// Read Class= from an INF's [Version] section. Returns null if not found.
        /// </summary>
        public static string? ReadInfClass(string infPath)
        {
            try
            {
                bool inVersion = false;
                foreach (var raw in File.ReadLines(infPath))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line[0] == ';') continue;
                    if (line[0] == '[')
                    {
                        if (inVersion) break; // end of [Version]
                        inVersion = line.StartsWith("[Version]", StringComparison.OrdinalIgnoreCase);
                        continue;
                    }
                    if (!inVersion) continue;

                    var eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    var key = line.Substring(0, eq).Trim();
                    if (!key.Equals("Class", StringComparison.OrdinalIgnoreCase)) continue;

                    var value = line.Substring(eq + 1);
                    var semi = value.IndexOf(';');
                    if (semi >= 0) value = value.Substring(0, semi);
                    value = value.Trim().Trim('"');
                    // Class=%Token% is defined in [Strings]
                    if (value.Length > 2 && value[0] == '%' && value[^1] == '%')
                        return ReadInfString(infPath, value.Trim('%')) ?? value.Trim('%');
                    return value;
                }
            }
            catch
            {
                // Unreadable INF - treat as no class
            }
            return null;
        }

        /// <summary>A value from an INF's [Strings] section, or null.</summary>
        private static string? ReadInfString(string infPath, string token)
        {
            try
            {
                bool inStrings = false;
                foreach (var raw in File.ReadLines(infPath))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line[0] == ';') continue;
                    if (line[0] == '[')
                    {
                        inStrings = line.Equals("[Strings]", StringComparison.OrdinalIgnoreCase);
                        continue;
                    }
                    if (!inStrings) continue;
                    var eq = line.IndexOf('=');
                    if (eq <= 0 || !line.Substring(0, eq).Trim().Equals(token, StringComparison.OrdinalIgnoreCase)) continue;
                    return line.Substring(eq + 1).Trim().Trim('"');
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Copy a driver package folder. Payload subfolders (e.g. x64\) are copied; subfolders that
        /// contain their own INFs are separate packages and are skipped (they get their own group).
        /// </summary>
        private static void CopyDriverPackage(string source, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.EnumerateFiles(source))
            {
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
            }
            foreach (var dir in Directory.EnumerateDirectories(source))
            {
                if (Directory.EnumerateFiles(dir, "*.inf", SearchOption.AllDirectories).Any()) continue;
                CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
            }
        }

        private static void CopyDirectory(string source, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.EnumerateFiles(source))
            {
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
            }
            foreach (var dir in Directory.EnumerateDirectories(source))
            {
                CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
            }
        }
    }
}
