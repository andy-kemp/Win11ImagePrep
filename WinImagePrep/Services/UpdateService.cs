using System;
using System.Collections.Generic;
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

                try
                {
                    var process = Process.Start(startInfo);
                    if (process == null)
                    {
                        progress?.Report("Update cancelled or failed to start. UAC prompt may have been declined.");
                        return false;
                    }

                    // Give the process a moment to start
                    await Task.Delay(500, cancellationToken);

                    // Signal that we should close
                    return true;
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // User cancelled UAC prompt
                    progress?.Report("Update cancelled. Administrator permission is required.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                progress?.Report($"Update failed: {ex.Message}");
                return false;
            }
        }

        private string CreateUpdaterScript(string currentExePath, string newExePath)
        {
            // Build documentation URLs array for PowerShell
            var docUrlsArray = new System.Text.StringBuilder();
            docUrlsArray.AppendLine("$docFiles = @(");
            foreach (var doc in DocumentationUrls)
            {
                docUrlsArray.AppendLine($"    @{{ Name = '{doc.Key}'; Url = '{doc.Value}' }},");
            }
            docUrlsArray.Append(")");

            return $@"
# WinImagePrep Update Script
$ErrorActionPreference = 'Continue'
$Host.UI.RawUI.WindowTitle = 'WinImagePrep Updater'
Write-Host 'WinImagePrep Updater' -ForegroundColor Cyan
Write-Host '=====================' -ForegroundColor Cyan
Write-Host ''

$currentExe = '{currentExePath.Replace("'", "''")}'
$newExe = '{newExePath.Replace("'", "''")}'
$processName = 'WinImagePrep'

# Verify files exist
if (-not (Test-Path $currentExe)) {{
    Write-Host ""ERROR: Current EXE not found: $currentExe"" -ForegroundColor Red
    Read-Host 'Press Enter to exit'
    exit 1
}}

if (-not (Test-Path $newExe)) {{
    Write-Host ""ERROR: New EXE not found: $newExe"" -ForegroundColor Red
    Read-Host 'Press Enter to exit'
    exit 1
}}

Write-Host ""Current EXE: $currentExe""
Write-Host ""New EXE: $newExe""
Write-Host ""New EXE size: $((Get-Item $newExe).Length / 1MB) MB""
Write-Host ''

# Download documentation files
Write-Host 'Downloading documentation...'
{docUrlsArray}

$tempDocDir = Join-Path $env:TEMP 'WinImagePrep_Update\docs'
if (-not (Test-Path $tempDocDir)) {{
    New-Item -ItemType Directory -Path $tempDocDir -Force | Out-Null
}}

$docDownloadSuccess = 0
$docDownloadFailed = 0

foreach ($doc in $docFiles) {{
    try {{
        $outPath = Join-Path $tempDocDir $doc.Name
        Write-Host ""  Downloading $($doc.Name)..."" -NoNewline
        Invoke-WebRequest -Uri $doc.Url -OutFile $outPath -ErrorAction Stop
        Write-Host ' Done' -ForegroundColor Green
        $docDownloadSuccess++
    }} catch {{
        Write-Host "" Failed: $($_.Exception.Message)"" -ForegroundColor Yellow
        $docDownloadFailed++
    }}
}}

Write-Host ""Downloaded $docDownloadSuccess/$($docFiles.Count) documentation files""
Write-Host ''

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
    Write-Host 'Process did not exit in time. Attempting to force close...' -ForegroundColor Yellow
    try {{
        Stop-Process -Name $processName -Force -ErrorAction Stop
        Start-Sleep -Seconds 2
        Write-Host 'Process closed.' -ForegroundColor Green
    }} catch {{
        Write-Host 'Could not close process. Please close WinImagePrep manually.' -ForegroundColor Red
        Read-Host 'Press Enter to exit'
        exit 1
    }}
}}

# Backup current EXE
Write-Host 'Backing up current version...'
$backupPath = $currentExe + '.backup'
try {{
    if (Test-Path $backupPath) {{
        Remove-Item $backupPath -Force -ErrorAction Stop
    }}
    Copy-Item $currentExe $backupPath -Force -ErrorAction Stop
    Write-Host 'Backup created.' -ForegroundColor Green
}} catch {{
    Write-Host ""Failed to create backup: $($_.Exception.Message)"" -ForegroundColor Red
    Read-Host 'Press Enter to exit'
    exit 1
}}

# Replace with new version
Write-Host 'Installing update...'
try {{
    # Remove any existing file lock
    [System.GC]::Collect()
    [System.GC]::WaitForPendingFinalizers()

    Copy-Item $newExe $currentExe -Force -ErrorAction Stop
    Write-Host 'Update installed successfully!' -ForegroundColor Green

    # Install documentation files
    $exeDir = Split-Path $currentExe -Parent
    $docsDir = Join-Path $exeDir 'docs'
    if (-not (Test-Path $docsDir)) {{
        New-Item -ItemType Directory -Path $docsDir -Force | Out-Null
    }}

    Write-Host 'Installing documentation...'
    $installedDocs = 0
    Get-ChildItem -Path $tempDocDir -File | ForEach-Object {{
        try {{
            $destPath = Join-Path $docsDir $_.Name
            Copy-Item $_.FullName $destPath -Force -ErrorAction Stop
            $installedDocs++
        }} catch {{
            Write-Host ""  Failed to copy $($_.Name): $($_.Exception.Message)"" -ForegroundColor Yellow
        }}
    }}

    # Also copy README and CHANGELOG to root
    $readmePath = Join-Path $tempDocDir 'README.md'
    $changelogPath = Join-Path $tempDocDir 'CHANGELOG.md'
    if (Test-Path $readmePath) {{
        Copy-Item $readmePath (Join-Path $exeDir 'README.md') -Force -ErrorAction SilentlyContinue
    }}
    if (Test-Path $changelogPath) {{
        Copy-Item $changelogPath (Join-Path $exeDir 'CHANGELOG.md') -Force -ErrorAction SilentlyContinue
    }}

    Write-Host ""Installed $installedDocs documentation files"" -ForegroundColor Green
    Write-Host ''
    Write-Host 'Starting updated application...'
    Start-Process $currentExe -ErrorAction Stop
    Start-Sleep -Seconds 2
}} catch {{
    Write-Host ""Update failed: $($_.Exception.Message)"" -ForegroundColor Red
    Write-Host 'Restoring backup...' -ForegroundColor Yellow
    try {{
        Copy-Item $backupPath $currentExe -Force -ErrorAction Stop
        Write-Host 'Backup restored.' -ForegroundColor Green
    }} catch {{
        Write-Host ""Failed to restore backup: $($_.Exception.Message)"" -ForegroundColor Red
    }}
    Read-Host 'Press Enter to exit'
    exit 1
}}

# Cleanup
Write-Host 'Cleaning up...'
Start-Sleep -Seconds 1
Remove-Item $newExe -Force -ErrorAction SilentlyContinue
Remove-Item $tempDocDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ''
Write-Host 'Update complete!' -ForegroundColor Green
Write-Host 'You can close this window.' -ForegroundColor Cyan
Start-Sleep -Seconds 3
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
