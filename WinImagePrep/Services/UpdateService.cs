using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    /// Service for checking and applying application updates from downloads.andykemp.com (GitHub as a fallback)
    /// </summary>
    public class UpdateService
    {
        private readonly HttpClient _httpClient;
        // Updates come from downloads.andykemp.com: win11-image-prep/latest.json names the version and the
        // app and updater EXEs, with their SHA-256 checksums. The files sit in the akc-download R2 bucket.
        // If that can't be read, the latest GitHub Release is tried, then version.json on main (5.9.0 and older).
        private const string DownloadsLatestUrl = "https://downloads.andykemp.com/win11-image-prep/latest.json";
        private const string DownloadsHost = "downloads.andykemp.com";
        private const string LatestReleaseApiUrl = "https://api.github.com/repos/andy-kemp/Win11ImagePrep/releases/latest";
        private static readonly string[] MainExeAssetNames = { "WinImagePrep_full.exe", "WinImagePrep.exe" };
        private const string UpdaterAssetName = "WinImagePrep.Updater.exe";
        private const string ChecksumsAssetName = "SHA256SUMS.txt";
        private ReleaseInfo? _latestRelease;

        private const string VersionCheckUrl = "https://raw.githubusercontent.com/andy-kemp/Win11ImagePrep/main/version.json";
        private const string ExeDownloadUrl = "https://github.com/andy-kemp/Win11ImagePrep/raw/main/publish/WinImagePrep.exe";
        private const string UpdaterDownloadUrl = "https://github.com/andy-kemp/Win11ImagePrep/raw/main/publish/WinImagePrep.Updater.exe";

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
        /// Check if a newer version is available
        /// </summary>
        public async Task<(bool updateAvailable, Version? latestVersion)> CheckForUpdateAsync(CancellationToken cancellationToken = default)
        {
            if (InstallRegistry.UpdatesManagedByOrganisation)
            {
                Logger.Info("Updates are managed by the organisation (ManagedUpdates); not checking");
                return (false, null);
            }

            var release = await GetLatestReleaseAsync(cancellationToken);
            if (release != null)
            {
                var current = GetCurrentVersion();
                var current3 = new Version(current.Major, current.Minor, Math.Max(current.Build, 0));
                Logger.Info($"Latest release: {release.Version}, current: {current3}");
                return (release.Version > current3, release.Version);
            }

            Logger.Warning("Latest release unavailable from downloads.andykemp.com and GitHub; falling back to version.json");
            return await CheckForUpdateFromVersionJsonAsync(cancellationToken);
        }

        private async Task<(bool updateAvailable, Version? latestVersion)> CheckForUpdateFromVersionJsonAsync(CancellationToken cancellationToken)
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
            var release = _latestRelease ?? await GetLatestReleaseAsync(cancellationToken);
            if (release != null)
            {
                // Installed with Win11ImagePrep_Setup.exe and automatic updates on: the SYSTEM task installs it,
                // with no UAC prompt
                var throughTask = TryUpdateThroughTask(release, progress);
                if (throughTask.HasValue) return throughTask.Value;

                // 5.9.7 and later update themselves in place (as Exchange Certificate Manager): one download,
                // no separate updater to start. The updater is only used if this folder can't be written to.
                var inPlace = await TryUpdateInPlaceAsync(release, progress, cancellationToken);
                if (inPlace.HasValue) return inPlace.Value;
                return await ApplyUpdateFromReleaseAsync(release, progress, cancellationToken);
            }

            Logger.Warning("Latest release unavailable; updating from main/publish instead");
            return await ApplyUpdateFromMainAsync(progress, cancellationToken);
        }

        private async Task<bool> ApplyUpdateFromMainAsync(IProgress<string>? progress, CancellationToken cancellationToken)
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
        // ------------------------------------------------------------------
        // In-place update
        // ------------------------------------------------------------------

        /// <summary>
        /// Windows lets a running EXE be renamed, so the new version is downloaded next to this one, checked against
        /// its published SHA-256, the running EXE is renamed to .old and the new one takes its name; the app then
        /// starts the new EXE and closes. The .old file is removed on the next start (CleanUpAfterUpdateAsync).
        /// Progress is shown in the splash, as the updater did.
        /// </summary>
        /// <returns>true when the new version has been started (the caller closes the app), false when the update
        /// failed and nothing was changed, null when this folder can't be written to (use the updater instead).</returns>
        private async Task<bool?> TryUpdateInPlaceAsync(ReleaseInfo release, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe) || !exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return null;
            var incoming = exe + ".new";
            var old = exe + ".old";
            var to = release.Version.ToString(3);

            SplashScreen? splash = null;
            Application.Current?.Dispatcher.Invoke(() =>
            {
                splash = new SplashScreen { Owner = Application.Current.MainWindow };
                splash.ShowUpdate(GetCurrentVersionString(), to);
                splash.Show();
            });
            void Status(string text, double? percent)
            {
                progress?.Report(text);
                splash?.UpdateStatus(text);
                splash?.SetProgress(percent);
            }
            void CloseSplash() => Application.Current?.Dispatcher.Invoke(() => splash?.Close());

            try
            {
                Status($"Downloading version {to}…", 0);
                try
                {
                    using var response = await _httpClient.GetAsync(release.MainExeUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    response.EnsureSuccessStatusCode();
                    var total = response.Content.Headers.ContentLength ?? 0;
                    await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
                    await using var output = new FileStream(incoming, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                    var buffer = new byte[81920];
                    long read = 0;
                    int n, lastPercent = -1;
                    while ((n = await input.ReadAsync(buffer, cancellationToken)) > 0)
                    {
                        await output.WriteAsync(buffer.AsMemory(0, n), cancellationToken);
                        read += n;
                        if (total > 0)
                        {
                            var percent = (int)(read * 100 / total);
                            if (percent != lastPercent)
                            {
                                lastPercent = percent;
                                splash?.UpdateStatus($"Downloading version {to}… {read / 1048576.0:0.0} of {total / 1048576.0:0.0} MB");
                                splash?.SetProgress(percent);
                            }
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    Logger.Info($"Can't write to {Path.GetDirectoryName(exe)}; using the updater, which runs as administrator");
                    CloseSplash();
                    return null;
                }

                Status("Checking the SHA-256…", null);
                var actual = await Sha256Async(incoming, cancellationToken);
                if (actual != release.MainExeSha256)
                {
                    File.Delete(incoming);
                    Logger.Error($"Checksum mismatch: expected {release.MainExeSha256}, got {actual}");
                    progress?.Report("✗ The download didn't match the published checksum, so it wasn't installed. Try again later.");
                    CloseSplash();
                    return false;
                }

                Status("Installing…", null);
                try { if (File.Exists(old)) File.Delete(old); } catch { /* left from an earlier update; renamed over below */ }
                File.Move(exe, old, overwrite: true);   // a running EXE can be renamed
                try { File.Move(incoming, exe); }
                catch
                {
                    File.Move(old, exe);                // put the original back
                    throw;
                }
                Logger.Info($"Updated in place to {to}; previous version kept as {Path.GetFileName(old)} until the next start");
                InstallRegistry.RecordInstalledVersion(exe, to);

                Status($"Updated to {to}. Restarting…", 100);
                await Task.Delay(1000, cancellationToken);
                // Without administrator rights, so drag and drop keeps working in the restarted app
                ProcessHelper.StartUnelevated(exe);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                try { if (File.Exists(incoming)) File.Delete(incoming); } catch { }
                Logger.Info("In-place update not allowed here; using the updater");
                CloseSplash();
                return null;
            }
            catch (Exception ex)
            {
                try { if (File.Exists(incoming)) File.Delete(incoming); } catch { }
                Logger.Error($"In-place update failed: {ex}");
                progress?.Report($"✗ Update failed: {ex.Message}");
                CloseSplash();
                return false;
            }
        }

        public const string UpdateTaskName = @"\Andy Kemp Consulting\Win11 Image Prep update";

        /// <summary>
        /// For a copy installed by Win11ImagePrep_Setup.exe with automatic updates on: start the scheduled task
        /// (it runs as SYSTEM, so no UAC prompt), which installs the new version once the app has closed, and start
        /// a small hidden helper that reopens the app when the new version is in place. Null if this copy wasn't
        /// installed that way, so the other update routes are used.
        /// </summary>
        private bool? TryUpdateThroughTask(ReleaseInfo release, IProgress<string>? progress)
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe) || !InstallRegistry.IsInstalledCopy(exe)) return null;
            if (RunHidden("schtasks.exe", $"/query /tn \"{UpdateTaskName}\"") != 0)
            {
                Logger.Info("Installed copy without the automatic update task; using the updater");
                return null;
            }

            try
            {
                // Tells the task the user asked for this update, so it waits for the app to close instead of skipping
                Directory.CreateDirectory(Models.AppSettings.SettingsDirectory);
                File.WriteAllText(Path.Combine(Models.AppSettings.SettingsDirectory, "update-now.flag"), release.Version.ToString(3));

                if (RunHidden("schtasks.exe", $"/run /tn \"{UpdateTaskName}\"") != 0)
                {
                    Logger.Warning("Could not start the update task; using the updater");
                    return null;
                }

                // Reopen the app (with the user's normal rights, through Explorer) once the new version is installed
                var to = release.Version.ToString(3);
                var script =
                    $"$v=[version]'{to}'; $exe='{exe.Replace("'", "''")}'; " +
                    "for ($i = 0; $i -lt 200; $i++) { Start-Sleep -Seconds 3; " +
                    "try { $f = (Get-Item $exe).VersionInfo; " +
                    "if ([version]::new($f.FileMajorPart, $f.FileMinorPart, $f.FileBuildPart) -ge $v) { Start-Sleep -Seconds 2; Start-Process explorer.exe -ArgumentList ('\"' + $exe + '\"'); break } } catch { } }";
                Process.Start(new ProcessStartInfo("powershell.exe",
                    $"-NoProfile -NonInteractive -WindowStyle Hidden -Command \"{script.Replace("\"", "\\\"")}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                Logger.Info($"Started the update task for {to}");
                progress?.Report($"Updating to {to} in the background. Win11 Image Prep will close and reopen when it's done (about a minute).");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Update through the task failed ({ex.Message}); using the updater");
                return null;
            }
        }

        private static int RunHidden(string file, string arguments)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo(file, arguments) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true });
                if (p == null) return -1;
                p.StandardOutput.ReadToEnd();
                p.WaitForExit(15000);
                return p.HasExited ? p.ExitCode : -1;
            }
            catch { return -1; }
        }

        /// <summary>Removes the .old and .new files an in-place update leaves next to the EXE. Run in the background on start.</summary>
        public static async Task CleanUpAfterUpdateAsync()
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return;
            foreach (var leftover in new[] { exe + ".old", exe + ".new" })
            {
                // The previous process may still be closing, so give it a few tries
                for (var i = 0; i < 10 && File.Exists(leftover); i++)
                {
                    try { File.Delete(leftover); break; }
                    catch { await Task.Delay(1000); }
                }
            }
        }

        // ------------------------------------------------------------------
        // GitHub Releases
        // ------------------------------------------------------------------

        /// <summary>
        /// Read the latest (non-pre-release) GitHub release: its version, the app and updater EXEs and
        /// their SHA-256 checksums from SHA256SUMS.txt. Returns null if any of that is missing.
        /// </summary>
        private async Task<ReleaseInfo?> GetLatestReleaseAsync(CancellationToken cancellationToken)
        {
            return await GetLatestFromDownloadsAsync(cancellationToken)
                ?? await GetLatestGitHubReleaseAsync(cancellationToken);
        }

        /// <summary>
        /// Read win11-image-prep/latest.json from downloads.andykemp.com. File URLs must be https on
        /// downloads.andykemp.com, and the checksums are checked again by the app and the updater.
        /// </summary>
        private async Task<ReleaseInfo?> GetLatestFromDownloadsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var url = $"{DownloadsLatestUrl}?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd("Win11ImagePrep-Updater");
                request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warning($"{DownloadsLatestUrl} returned {(int)response.StatusCode}");
                    return null;
                }

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                var root = doc.RootElement;
                var versionText = root.GetProperty("version").GetString() ?? string.Empty;
                if (!Version.TryParse(versionText, out var version))
                {
                    Logger.Warning($"latest.json version '{versionText}' is not a version");
                    return null;
                }

                static (string? Url, string? Sha) ReadFile(JsonElement root, string name) =>
                    root.TryGetProperty(name, out var f)
                        ? (f.GetProperty("url").GetString(), f.GetProperty("sha256").GetString()?.ToLowerInvariant())
                        : (null, null);
                var app = ReadFile(root, "app");
                var updater = ReadFile(root, "updater");
                if (!IsDownloadsUrl(app.Url) || !IsDownloadsUrl(updater.Url) || !IsSha256(app.Sha) || !IsSha256(updater.Sha))
                {
                    Logger.Warning("latest.json is missing the app or updater, or names a file outside downloads.andykemp.com");
                    return null;
                }

                string? notes = root.TryGetProperty("notes", out var n) ? n.GetString() : null;
                _latestRelease = new ReleaseInfo(
                    new Version(version.Major, version.Minor, Math.Max(version.Build, 0)),
                    $"v{versionText}", app.Url!, app.Sha!, updater.Url!, updater.Sha!, notes);
                Logger.Info($"Latest release from {DownloadsHost}: {_latestRelease.Version}");
                return _latestRelease;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Could not read {DownloadsLatestUrl}: {ex.Message}");
                return null;
            }
        }

        private static bool IsDownloadsUrl(string? url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps
            && string.Equals(u.Host, DownloadsHost, StringComparison.OrdinalIgnoreCase);

        private static bool IsSha256(string? s) => s != null && s.Length == 64 && s.All(Uri.IsHexDigit);

        private async Task<ReleaseInfo?> GetLatestGitHubReleaseAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApiUrl);
                request.Headers.UserAgent.ParseAdd("Win11ImagePrep-Updater");
                request.Headers.Accept.ParseAdd("application/vnd.github+json");
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Warning($"Releases API returned {(int)response.StatusCode}");
                    return null;
                }

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                var root = doc.RootElement;
                var tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
                if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version))
                {
                    Logger.Warning($"Release tag '{tag}' is not a version");
                    return null;
                }

                var assets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var asset in root.GetProperty("assets").EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString();
                    var url = asset.GetProperty("browser_download_url").GetString();
                    if (name != null && url != null) assets[name] = url;
                }

                var mainName = MainExeAssetNames.FirstOrDefault(assets.ContainsKey);
                if (mainName == null || !assets.ContainsKey(UpdaterAssetName) || !assets.ContainsKey(ChecksumsAssetName))
                {
                    Logger.Warning($"Release {tag} is missing the app, updater or {ChecksumsAssetName}");
                    return null;
                }

                var sums = await _httpClient.GetStringAsync(assets[ChecksumsAssetName], cancellationToken);
                var hashes = ParseChecksums(sums);
                if (!hashes.TryGetValue(mainName, out var mainSha) || !hashes.TryGetValue(UpdaterAssetName, out var updaterSha))
                {
                    Logger.Warning($"{ChecksumsAssetName} in release {tag} has no checksum for the app or updater");
                    return null;
                }

                string? notes = null;
                if (root.TryGetProperty("body", out var body)) notes = body.GetString();

                _latestRelease = new ReleaseInfo(
                    new Version(version.Major, version.Minor, Math.Max(version.Build, 0)),
                    tag, assets[mainName], mainSha, assets[UpdaterAssetName], updaterSha, notes);
                return _latestRelease;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Could not read the latest release: {ex.Message}");
                return null;
            }
        }

        private static Dictionary<string, string> ParseChecksums(string text)
        {
            // Lines like "<64 hex chars>  <file name>"
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                var space = line.IndexOf(' ');
                if (space != 64) continue;
                var hash = line.Substring(0, 64);
                var name = line.Substring(space).Trim().TrimStart('*');
                if (hash.All(Uri.IsHexDigit)) result[name] = hash.ToLowerInvariant();
            }
            return result;
        }

        private static async Task<string> Sha256Async(string path, CancellationToken cancellationToken)
        {
            await using var stream = File.OpenRead(path);
            var hash = await System.Security.Cryptography.SHA256.HashDataAsync(stream, cancellationToken);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private async Task DownloadToFileAsync(string url, string path, string label, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? 0;
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            var buffer = new byte[81920];
            long read = 0;
            int n;
            var lastPercent = -1;
            while ((n = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, n), cancellationToken);
                read += n;
                if (total > 0)
                {
                    var percent = (int)(read * 100 / total);
                    if (percent != lastPercent && percent % 5 == 0)
                    {
                        lastPercent = percent;
                        progress?.Report($"Downloading {label}: {read / 1048576.0:F1} MB / {total / 1048576.0:F1} MB ({percent}%)");
                    }
                }
            }
        }

        /// <summary>
        /// Update from a GitHub release. The updater runs as administrator, so it is only used if its
        /// checksum matches the release; the updater then checks the app's checksum before installing it.
        /// </summary>
        private async Task<bool> ApplyUpdateFromReleaseAsync(ReleaseInfo release, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            try
            {
                progress?.Report($"Preparing update to v{release.Version}...");
                var currentExePath = Process.GetCurrentProcess().MainModule?.FileName;
                var exeDirectory = Path.GetDirectoryName(currentExePath ?? string.Empty);
                if (string.IsNullOrEmpty(currentExePath) || string.IsNullOrEmpty(exeDirectory))
                {
                    progress?.Report("Error: Could not determine where the app is installed");
                    return false;
                }

                // 1. Make sure the local updater is exactly the release's updater
                var updaterPath = Path.Combine(exeDirectory, UpdaterAssetName);
                var updaterOk = File.Exists(updaterPath) && await Sha256Async(updaterPath, cancellationToken) == release.UpdaterSha256;
                if (!updaterOk)
                {
                    var temp = Path.Combine(Path.GetTempPath(), $"WinImagePrep.Updater_{Guid.NewGuid():N}.exe");
                    progress?.Report("Downloading the updater...");
                    await DownloadToFileAsync(release.UpdaterUrl, temp, "updater", progress, cancellationToken);
                    var actual = await Sha256Async(temp, cancellationToken);
                    if (actual != release.UpdaterSha256)
                    {
                        File.Delete(temp);
                        progress?.Report("✗ The downloaded updater didn't match the release checksum, so it wasn't used. Try again later.");
                        Logger.Error($"Updater checksum mismatch: expected {release.UpdaterSha256}, got {actual}");
                        return false;
                    }
                    File.Copy(temp, updaterPath, overwrite: true);
                    File.Delete(temp);
                    progress?.Report("✓ Updater downloaded and checked");
                }
                else
                {
                    progress?.Report("✓ Updater is up to date");
                }

                // 2. Hand over to the updater (through a file: arguments are lost through UAC)
                var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                var updateInfoPath = Path.Combine(programData, "WinImagePrep_UpdateInfo.json");
                var current = Process.GetCurrentProcess();
                File.WriteAllText(updateInfoPath, JsonSerializer.Serialize(new
                {
                    TargetExePath = currentExePath,
                    DownloadUrl = release.MainExeUrl,
                    ExpectedSha256 = release.MainExeSha256,
                    UpdaterDownloadUrl = release.UpdaterUrl,
                    ExpectedUpdaterSha256 = release.UpdaterSha256,
                    Version = release.Version.ToString(),
                    ProcessName = current.ProcessName,
                    ProcessId = current.Id
                }));

                progress?.Report("Launching updater...");
                try
                {
                    var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = updaterPath,
                        UseShellExecute = true,
                        Verb = "runas",
                        WorkingDirectory = exeDirectory
                    });
                    if (process == null)
                    {
                        progress?.Report("Update cancelled - Administrator privileges required.");
                        return false;
                    }
                    progress?.Report("Updater started. Application will close shortly...");
                    await Task.Delay(1000, cancellationToken);
                    return true;
                }
                catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
                {
                    progress?.Report("Update cancelled - Administrator privileges are required to update.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"Update failed: {ex.Message}");
                Logger.Error($"Release update failed: {ex}");
                return false;
            }
        }
    }

    internal record ReleaseInfo(
        Version Version,
        string Tag,
        string MainExeUrl,
        string MainExeSha256,
        string UpdaterUrl,
        string UpdaterSha256,
        string? Notes);

    internal class VersionInfo
    {
        public string? Version { get; set; }
        public string? UpdaterVersion { get; set; }
        public string? ReleaseDate { get; set; }
        public string? ReleaseNotes { get; set; }
    }
}
