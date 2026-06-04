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
                if (Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, true);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
                Directory.CreateDirectory(tempDir);

                var newExePath = Path.Combine(tempDir, "WinImagePrep_new.exe");
                var updaterScriptPath = Path.Combine(tempDir, "Update.ps1");

                // Download new EXE first
                progress?.Report("Downloading update (1/2)...");
                var response = await _httpClient.GetAsync(ExeDownloadUrl, cancellationToken);
                response.EnsureSuccessStatusCode();

                await using (var fileStream = File.Create(newExePath))
                {
                    await response.Content.CopyToAsync(fileStream, cancellationToken);
                }

                progress?.Report($"Downloaded EXE: {new FileInfo(newExePath).Length / 1024 / 1024:F1} MB");

                // Download documentation files
                progress?.Report("Downloading documentation (2/2)...");
                var tempDocDir = Path.Combine(tempDir, "docs");
                Directory.CreateDirectory(tempDocDir);

                int docSuccess = 0;
                int docFailed = 0;

                foreach (var doc in DocumentationUrls)
                {
                    try
                    {
                        var docPath = Path.Combine(tempDocDir, doc.Key);
                        var docResponse = await _httpClient.GetAsync(doc.Value, cancellationToken);

                        if (docResponse.IsSuccessStatusCode)
                        {
                            await using var docStream = File.Create(docPath);
                            await docResponse.Content.CopyToAsync(docStream, cancellationToken);
                            docSuccess++;
                        }
                        else
                        {
                            docFailed++;
                        }
                    }
                    catch
                    {
                        docFailed++;
                    }
                }

                progress?.Report($"Downloaded {docSuccess}/{DocumentationUrls.Count} documentation files");

                // Create updater script (now that everything is downloaded)
                progress?.Report("Preparing installation script...");
                var updaterScript = CreateUpdaterScript(currentExePath, newExePath, tempDocDir);
                await File.WriteAllTextAsync(updaterScriptPath, updaterScript, cancellationToken);

                // Launch updater script
                progress?.Report("Launching updater...");

                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -WindowStyle Normal -File \"{updaterScriptPath}\"",
                    UseShellExecute = true,
                    WorkingDirectory = tempDir
                };

                try
                {
                    var process = Process.Start(startInfo);
                    if (process == null)
                    {
                        progress?.Report("Update failed to start. Please try again.");
                        return false;
                    }

                    // Give the PowerShell window time to open and display
                    progress?.Report("Updater started. Application will close in 2 seconds...");
                    await Task.Delay(2000, cancellationToken);

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

        private string CreateUpdaterScript(string currentExePath, string newExePath, string docsSourcePath)
        {
            return $@"
# WinImagePrep Update Script
$ErrorActionPreference = 'Continue'
$Host.UI.RawUI.WindowTitle = 'WinImagePrep Updater'
Write-Host 'WinImagePrep Updater' -ForegroundColor Cyan
Write-Host '=====================' -ForegroundColor Cyan
Write-Host ''

$currentExe = '{currentExePath.Replace("'", "''")}'
$newExe = '{newExePath.Replace("'", "''")}'
$docsSource = '{docsSourcePath.Replace("'", "''")}'
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
Write-Host ""Docs source: $docsSource""
Write-Host ''

# Wait for main process to exit
Write-Host 'Waiting for application to close...'
$timeout = 30
$elapsed = 0
while ((Get-Process -Name $processName -ErrorAction SilentlyContinue) -and ($elapsed -lt $timeout)) {{
    Start-Sleep -Seconds 1
    $elapsed++
    if ($elapsed % 5 -eq 0) {{
        Write-Host ""  Still waiting... ($elapsed seconds)"" -ForegroundColor Yellow
    }}
}}
Write-Host ''

if (Get-Process -Name $processName -ErrorAction SilentlyContinue) {{
    Write-Host 'Process did not exit in time. Attempting to force close...' -ForegroundColor Yellow
    try {{
        Stop-Process -Name $processName -Force -ErrorAction Stop
        Start-Sleep -Seconds 2
        Write-Host 'Process closed.' -ForegroundColor Green
    }} catch {{
        Write-Host 'Could not close process. Please close WinImagePrep manually and press Enter.' -ForegroundColor Red
        Read-Host 'Press Enter to continue'
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

    # Install documentation files if they exist
    if (Test-Path $docsSource) {{
        $exeDir = Split-Path $currentExe -Parent
        $docsDir = Join-Path $exeDir 'docs'
        if (-not (Test-Path $docsDir)) {{
            New-Item -ItemType Directory -Path $docsDir -Force | Out-Null
        }}

        Write-Host 'Installing documentation...'
        $installedDocs = 0
        Get-ChildItem -Path $docsSource -File | ForEach-Object {{
            try {{
                $destPath = Join-Path $docsDir $_.Name
                Copy-Item $_.FullName $destPath -Force -ErrorAction Stop
                Write-Host ""  Installed: $($_.Name)"" -ForegroundColor Gray
                $installedDocs++
            }} catch {{
                Write-Host ""  Failed to copy $($_.Name): $($_.Exception.Message)"" -ForegroundColor Yellow
            }}
        }}

        # Also copy README and CHANGELOG to root
        $readmePath = Join-Path $docsSource 'README.md'
        $changelogPath = Join-Path $docsSource 'CHANGELOG.md'
        if (Test-Path $readmePath) {{
            Copy-Item $readmePath (Join-Path $exeDir 'README.md') -Force -ErrorAction SilentlyContinue
            Write-Host '  Installed: README.md' -ForegroundColor Gray
        }}
        if (Test-Path $changelogPath) {{
            Copy-Item $changelogPath (Join-Path $exeDir 'CHANGELOG.md') -Force -ErrorAction SilentlyContinue
            Write-Host '  Installed: CHANGELOG.md' -ForegroundColor Gray
        }}

        Write-Host ""Installed $installedDocs documentation files"" -ForegroundColor Green
    }} else {{
        Write-Host 'No documentation files found to install.' -ForegroundColor Yellow
    }}

    Write-Host ''
    Write-Host 'Starting updated application...'
    Start-Process $currentExe -ErrorAction Stop
    Start-Sleep -Seconds 1
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
Write-Host 'Cleaning up temporary files...'
Start-Sleep -Seconds 1
$tempUpdateDir = Split-Path $newExe -Parent
Remove-Item $tempUpdateDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ''
Write-Host 'Update complete!' -ForegroundColor Green
Write-Host 'This window will close in 5 seconds...' -ForegroundColor Cyan
Start-Sleep -Seconds 5
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
