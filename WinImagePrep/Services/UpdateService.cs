using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

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
        /// Check if a newer version is available on GitHub
        /// </summary>
        public async Task<(bool updateAvailable, Version? latestVersion)> CheckForUpdateAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync(VersionCheckUrl, cancellationToken);

                if (!response.IsSuccessStatusCode)
                    return (false, null);

                var json = await response.Content.ReadAsStringAsync();
                var versionInfo = JsonSerializer.Deserialize<VersionInfo>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (versionInfo?.Version == null)
                    return (false, null);

                var latestVersion = Version.Parse(versionInfo.Version);
                var currentVersion = GetCurrentVersion();

                return (latestVersion > currentVersion, latestVersion);
            }
            catch
            {
                return (false, null);
            }
        }

        /// <summary>
        /// Download and apply the update by launching an elevated updater script
        /// </summary>
        public async Task<bool> DownloadAndApplyUpdateAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                progress?.Report("Preparing update...");

                // Get current EXE path
                var currentExePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(currentExePath))
                {
                    progress?.Report("Error: Could not determine current EXE path");
                    return false;
                }

                // Create temp directory for update files
                var tempDir = Path.Combine(Path.GetTempPath(), "WinImagePrep_Update");
                Directory.CreateDirectory(tempDir);

                var newExePath = Path.Combine(tempDir, "WinImagePrep_new.exe");
                var updaterScriptPath = Path.Combine(tempDir, "Update.ps1");

                // Download new EXE
                progress?.Report("Downloading update...");
                var response = await _httpClient.GetAsync(ExeDownloadUrl, cancellationToken);
                response.EnsureSuccessStatusCode();

                await using (var fileStream = File.Create(newExePath))
                {
                    await response.Content.CopyToAsync(fileStream, cancellationToken);
                }

                progress?.Report("Update downloaded. Preparing installation...");

                // Create updater script
                var updaterScript = CreateUpdaterScript(currentExePath, newExePath);
                await File.WriteAllTextAsync(updaterScriptPath, updaterScript, cancellationToken);

                // Launch updater script with admin elevation
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -File \"{updaterScriptPath}\"",
                    Verb = "runas", // Request elevation
                    UseShellExecute = true
                };

                Process.Start(startInfo);

                // Signal that we should close
                return true;
            }
            catch (Exception ex)
            {
                progress?.Report($"Update failed: {ex.Message}");
                return false;
            }
        }

        private string CreateUpdaterScript(string currentExePath, string newExePath)
        {
            return $@"
# WinImagePrep Update Script
Write-Host 'WinImagePrep Updater' -ForegroundColor Cyan
Write-Host '=====================' -ForegroundColor Cyan
Write-Host ''

$currentExe = '{currentExePath.Replace("'", "''")}'
$newExe = '{newExePath.Replace("'", "''")}'
$processName = 'WinImagePrep'

# Wait for main process to exit
Write-Host 'Waiting for application to close...'
$timeout = 30
$elapsed = 0
while ((Get-Process -Name $processName -ErrorAction SilentlyContinue) -and ($elapsed -lt $timeout)) {{
    Start-Sleep -Seconds 1
    $elapsed++
    Write-Host '.' -NoNewline
}}
Write-Host ''

if (Get-Process -Name $processName -ErrorAction SilentlyContinue) {{
    Write-Host 'Process did not exit in time. Please close WinImagePrep manually and try again.' -ForegroundColor Red
    Read-Host 'Press Enter to exit'
    exit 1
}}

# Backup current EXE
Write-Host 'Backing up current version...'
$backupPath = $currentExe + '.backup'
if (Test-Path $backupPath) {{
    Remove-Item $backupPath -Force
}}
Copy-Item $currentExe $backupPath -Force

# Replace with new version
Write-Host 'Installing update...'
try {{
    Copy-Item $newExe $currentExe -Force
    Write-Host 'Update installed successfully!' -ForegroundColor Green
    Write-Host ''
    Write-Host 'Starting updated application...'
    Start-Process $currentExe
    Start-Sleep -Seconds 2
}} catch {{
    Write-Host 'Update failed! Restoring backup...' -ForegroundColor Red
    Copy-Item $backupPath $currentExe -Force
    Write-Host 'Backup restored.' -ForegroundColor Yellow
    Read-Host 'Press Enter to exit'
    exit 1
}}

# Cleanup
Write-Host 'Cleaning up...'
Start-Sleep -Seconds 1
Remove-Item $newExe -Force -ErrorAction SilentlyContinue

Write-Host 'Update complete!' -ForegroundColor Green
Start-Sleep -Seconds 2
";
        }
    }

    internal class VersionInfo
    {
        public string? Version { get; set; }
        public string? ReleaseDate { get; set; }
        public string? ReleaseNotes { get; set; }
    }
}
