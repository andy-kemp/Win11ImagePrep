using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WinImagePrep.Helpers;

namespace WinImagePrep.Services
{
    /// <summary>
    /// Service for checking and applying application updates from GitHub
    /// </summary>
    public class UpdateService
    {
        private readonly HttpClient _httpClient;
        private const string VersionCheckUrl = "https://raw.githubusercontent.com/andy-kemp/Win11ImagePrep/main/version.json";
        private const string ExeDownloadUrl = "https://github.com/andy-kemp/Win11ImagePrep/raw/main/publish/WinImagePrep.exe";
        private const string UpdaterDownloadUrl = "https://github.com/andy-kemp/Win11ImagePrep/raw/main/publish/WinImagePrep.Updater.exe";

        // Documentation download URLs
        private static readonly Dictionary<string, string> DocumentationUrls = new()
        {
            { "UserGuide.html", "https://raw.githubusercontent.com/andy-kemp/Win11ImagePrep/main/publish/docs/UserGuide.html" },
            { "README.md", "https://raw.githubusercontent.com/andy-kemp/Win11ImagePrep/main/publish/README.md" },
            { "CHANGELOG.md", "https://raw.githubusercontent.com/andy-kemp/Win11ImagePrep/main/publish/CHANGELOG.md" },
            { "ReleaseNotes.txt", "https://raw.githubusercontent.com/andy-kemp/Win11ImagePrep/main/publish/docs/ReleaseNotes.txt" },
            { "AUTOPILOT_MODE.md", "https://raw.githubusercontent.com/andy-kemp/Win11ImagePrep/main/publish/docs/AUTOPILOT_MODE.md" }
        };

        public UpdateService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// Get the current version of the running application
        /// </summary>
        public Version GetCurrentVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            return assembly.GetName().Version ?? new Version(0, 0, 0, 0);
        }

        /// <summary>
        /// Get the current version formatted as Major.Minor.Build
        /// </summary>
        public string GetCurrentVersionString()
        {
            var version = GetCurrentVersion();
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }

        /// <summary>
        /// Check if a newer version is available on GitHub
        /// </summary>
        public async Task<(bool updateAvailable, Version? latestVersion)> CheckForUpdateAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Add cache-busting parameter to ensure we get the latest version
                var versionCheckUrlWithCacheBust = $"{VersionCheckUrl}?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                Logger.Info($"Checking for updates from: {versionCheckUrlWithCacheBust}");

                // Create request with no-cache headers
                var request = new HttpRequestMessage(HttpMethod.Get, versionCheckUrlWithCacheBust);
                request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
                {
                    NoCache = true,
                    NoStore = true,
                    MaxAge = TimeSpan.Zero
                };
                request.Headers.Add("Pragma", "no-cache");

                var response = await _httpClient.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warning($"Update check failed with status: {response.StatusCode}");
                    return (false, null);
                }

                var json = await response.Content.ReadAsStringAsync();
                Logger.Info($"Retrieved version info: {json}");

                var versionInfo = JsonSerializer.Deserialize<VersionInfo>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (versionInfo?.Version == null)
                {
                    Logger.Warning("Version info is null or missing version field");
                    return (false, null);
                }

                var latestVersion = Version.Parse(versionInfo.Version);
                var currentVersion = GetCurrentVersion();

                Logger.Info($"Current version: {currentVersion}, Latest version: {latestVersion}");
                Logger.Info($"Update available: {latestVersion > currentVersion}");

                return (latestVersion > currentVersion, latestVersion);
            }
            catch (Exception ex)
            {
                Logger.Error($"Update check exception: {ex.Message}");
                return (false, null);
            }
        }

        /// <summary>
        /// Download and apply the update by first downloading the latest updater (if needed), then launching it
        /// </summary>
        public async Task<bool> DownloadAndApplyUpdateAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                progress?.Report("Preparing update...");
                Logger.Info("Starting update process");

                // Get current EXE path and directory
                var currentExePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(currentExePath))
                {
                    progress?.Report("Error: Could not determine current EXE path");
                    Logger.Error("Could not determine current EXE path");
                    return false;
                }

                var exeDirectory = Path.GetDirectoryName(currentExePath);
                if (string.IsNullOrEmpty(exeDirectory))
                {
                    progress?.Report("Error: Could not determine EXE directory");
                    Logger.Error("Could not determine EXE directory");
                    return false;
                }

                var updaterPath = Path.Combine(exeDirectory, "WinImagePrep.Updater.exe");

                // Step 1: Check if we need to download the updater
                bool needsUpdaterDownload = false;

                if (!File.Exists(updaterPath))
                {
                    progress?.Report("Updater not found, will download...");
                    Logger.Info("Updater does not exist locally");
                    needsUpdaterDownload = true;
                }
                else
                {
                    // Check updater version
                    try
                    {
                        var localUpdaterVersion = FileVersionInfo.GetVersionInfo(updaterPath);
                        var localVersion = new Version(localUpdaterVersion.FileMajorPart, 
                                                      localUpdaterVersion.FileMinorPart, 
                                                      localUpdaterVersion.FileBuildPart);

                        Logger.Info($"Local updater version: {localVersion}");

                        // Fetch remote version info
                        var versionCheckUrlWithCacheBust = $"{VersionCheckUrl}?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                        var request = new HttpRequestMessage(HttpMethod.Get, versionCheckUrlWithCacheBust);
                        request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
                        {
                            NoCache = true,
                            NoStore = true,
                            MaxAge = TimeSpan.Zero
                        };

                        var response = await _httpClient.SendAsync(request, cancellationToken);
                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync();
                            var versionInfo = JsonSerializer.Deserialize<VersionInfo>(json, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });

                            if (!string.IsNullOrEmpty(versionInfo?.UpdaterVersion))
                            {
                                var remoteUpdaterVersion = Version.Parse(versionInfo.UpdaterVersion);
                                Logger.Info($"Remote updater version: {remoteUpdaterVersion}");

                                if (remoteUpdaterVersion > localVersion)
                                {
                                    progress?.Report($"Newer updater available: v{remoteUpdaterVersion}");
                                    Logger.Info($"Remote updater is newer ({remoteUpdaterVersion} > {localVersion})");
                                    needsUpdaterDownload = true;
                                }
                                else
                                {
                                    progress?.Report("Updater is up to date");
                                    Logger.Info("Local updater is current");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Could not check updater version, will re-download: {ex.Message}");
                        needsUpdaterDownload = true;
                    }
                }

                // Step 2: Download the updater if needed
                if (needsUpdaterDownload)
                {
                    progress?.Report("Downloading latest updater...");

                    // Add cache-busting parameter to ensure we get the latest updater
                    var updaterDownloadUrlWithCacheBust = $"{UpdaterDownloadUrl}?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                    Logger.Info($"Downloading updater from: {updaterDownloadUrlWithCacheBust}");
                    Logger.Info($"Updater destination: {updaterPath}");

                    try
                    {
                        var response = await _httpClient.GetAsync(updaterDownloadUrlWithCacheBust, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                        response.EnsureSuccessStatusCode();

                        var totalBytes = response.Content.Headers.ContentLength ?? 0;
                        var totalMB = totalBytes / (1024.0 * 1024.0);

                        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                        await using var fileStream = new FileStream(updaterPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                        var buffer = new byte[8192];
                        long totalRead = 0;
                        int bytesRead;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
                        {
                            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                            totalRead += bytesRead;

                            if (totalBytes > 0)
                            {
                                var readMB = totalRead / (1024.0 * 1024.0);
                                var percent = (int)((totalRead * 100) / totalBytes);
                                progress?.Report($"Downloading updater: {readMB:F1} MB / {totalMB:F1} MB ({percent}%)");
                            }
                        }

                        Logger.Info($"Updater downloaded successfully ({totalRead} bytes)");
                        progress?.Report("Updater downloaded successfully");
                    }
                    catch (Exception ex)
                    {
                        progress?.Report($"Failed to download updater: {ex.Message}");
                        Logger.Error($"Failed to download updater: {ex.Message}");
                        return false;
                    }
                }

                // Step 3: Verify the updater exists
                if (!File.Exists(updaterPath))
                {
                    progress?.Report("Error: Updater not available");
                    Logger.Error("Updater file does not exist");
                    return false;
                }

                // Get current process info to pass to updater
                var currentProcess = Process.GetCurrentProcess();
                var currentProcessId = currentProcess.Id;
                var currentProcessName = currentProcess.ProcessName;

                // Step 4: Write update info to a file (arguments can get mangled through UAC)
                // Use ProgramData which is accessible to both user and elevated processes
                var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                var updateInfoPath = Path.Combine(programData, "WinImagePrep_UpdateInfo.json");

                // Add cache-busting parameter to download URL to ensure we get the latest version
                var exeDownloadUrlWithCacheBust = $"{ExeDownloadUrl}?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

                var updateInfo = new
                {
                    TargetExePath = currentExePath,
                    DownloadUrl = exeDownloadUrlWithCacheBust,
                    ProcessName = currentProcessName,
                    ProcessId = currentProcessId
                };
                File.WriteAllText(updateInfoPath, JsonSerializer.Serialize(updateInfo));
                Logger.Info($"Wrote update info to: {updateInfoPath}");

                // Step 5: Launch the updater WITHOUT arguments (they get lost through UAC)
                // The updater will look for the JSON in the fixed ProgramData location
                progress?.Report("Launching updater...");
                Logger.Info($"Starting updater: {updaterPath}");

                var startInfo = new ProcessStartInfo
                {
                    FileName = updaterPath,
                    Arguments = "", // Don't pass arguments - they get lost through UAC elevation
                    UseShellExecute = true,
                    Verb = "runas", // Request admin elevation
                    WorkingDirectory = exeDirectory
                };

                Logger.Info($"Working directory: {exeDirectory}");
                Logger.Info("Requesting admin elevation for updater");

                try
                {
                    var process = Process.Start(startInfo);
                    if (process == null)
                    {
                        progress?.Report("Update cancelled - Administrator privileges required.");
                        Logger.Error("Process.Start returned null - UAC was declined by user");
                        return false;
                    }

                    // Give the updater time to start
                    progress?.Report("Updater started. Application will close shortly...");
                    Logger.Info("Updater process started successfully");
                    await Task.Delay(1000, cancellationToken);

                    // Signal that we should close
                    return true;
                }
                catch (System.ComponentModel.Win32Exception win32Ex) when (win32Ex.NativeErrorCode == 1223)
                {
                    // ERROR_CANCELLED = 1223 - The operation was canceled by the user (UAC prompt declined)
                    progress?.Report("Update cancelled - Administrator privileges are required to update.");
                    Logger.Info("User cancelled UAC elevation prompt for updater");
                    return false;
                }
                catch (Exception ex)
                {
                    progress?.Report($"Failed to launch updater: {ex.Message}");
                    Logger.Error($"Failed to launch updater: {ex.Message}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"Update failed: {ex.Message}");
                return false;
            }
        }
    }

    internal class VersionInfo
    {
        public string? Version { get; set; }
        public string? UpdaterVersion { get; set; }
        public string? ReleaseDate { get; set; }
        public string? ReleaseNotes { get; set; }
    }
}
