using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
                Logger.Info($"Checking for updates from: {VersionCheckUrl}");
                var response = await _httpClient.GetAsync(VersionCheckUrl, cancellationToken);

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
        /// Download and apply the update by launching the dedicated updater application
        /// </summary>
        public async Task<bool> DownloadAndApplyUpdateAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                progress?.Report("Preparing update...");

                // Get current EXE path and directory
                var currentExePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(currentExePath))
                {
                    progress?.Report("Error: Could not determine current EXE path");
                    return false;
                }

                var exeDirectory = Path.GetDirectoryName(currentExePath);
                if (string.IsNullOrEmpty(exeDirectory))
                {
                    progress?.Report("Error: Could not determine EXE directory");
                    return false;
                }

                // Look for the updater in the same directory as the main EXE
                var updaterPath = Path.Combine(exeDirectory, "WinImagePrep.Updater.exe");
                if (!File.Exists(updaterPath))
                {
                    progress?.Report("Error: Updater executable not found. Please reinstall the application.");
                    return false;
                }

                // Get current process info to pass to updater
                var currentProcess = Process.GetCurrentProcess();
                var currentProcessId = currentProcess.Id;
                var currentProcessName = currentProcess.ProcessName;

                // Download EXE URL
                var downloadUrl = ExeDownloadUrl;

                // Launch the updater with arguments: <targetExePath> <downloadUrl> <processName> <processId>
                progress?.Report("Launching updater...");

                var startInfo = new ProcessStartInfo
                {
                    FileName = updaterPath,
                    Arguments = $"\"{currentExePath}\" \"{downloadUrl}\" \"{currentProcessName}\" {currentProcessId}",
                    UseShellExecute = true,
                    WorkingDirectory = exeDirectory
                };

                try
                {
                    var process = Process.Start(startInfo);
                    if (process == null)
                    {
                        progress?.Report("Failed to start updater. Please try again.");
                        return false;
                    }

                    // Give the updater time to start
                    progress?.Report("Updater started. Application will close shortly...");
                    await Task.Delay(1000, cancellationToken);

                    // Signal that we should close
                    return true;
                }
                catch (Exception ex)
                {
                    progress?.Report($"Failed to launch updater: {ex.Message}");
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
        public string? ReleaseDate { get; set; }
        public string? ReleaseNotes { get; set; }
    }
}
