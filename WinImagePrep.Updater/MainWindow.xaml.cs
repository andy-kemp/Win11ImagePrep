using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace WinImagePrep.Updater;

/// <summary>
/// Updater window that downloads and applies updates for WinImagePrep
/// </summary>
public partial class MainWindow : Window
{
    private readonly string _targetExePath = string.Empty;
    private readonly string _downloadUrl = string.Empty;
    private readonly string _targetProcessName = string.Empty;
    private readonly int _targetProcessId;
    private bool _updateSuccessful = false;

    public MainWindow()
    {
        var fallbackLog = Path.Combine(Path.GetTempPath(), "WinImagePrep_Updater_Constructor.log");
        try
        {
            File.WriteAllText(fallbackLog, $"MainWindow constructor ENTRY at {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\n");

            App.Log("MainWindow constructor started");

            try
            {
                File.AppendAllText(fallbackLog, "Calling InitializeComponent...\n");
                InitializeComponent();
                File.AppendAllText(fallbackLog, "InitializeComponent completed\n");
                App.Log("InitializeComponent completed");
            }
            catch (Exception ex)
            {
                File.AppendAllText(fallbackLog, $"InitializeComponent ERROR: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n");
                App.Log($"ERROR in InitializeComponent: {ex.GetType().Name}: {ex.Message}");
                App.Log($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }
        catch (Exception ex)
        {
            File.AppendAllText(fallbackLog, $"Constructor ERROR: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n");
            throw;
        }

        // Don't rely on command-line arguments - they get lost through UAC elevation
        // Always read from a fixed location that both processes can access
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var updateInfoPath = Path.Combine(programData, "WinImagePrep_UpdateInfo.json");

        App.Log($"Looking for update info at: {updateInfoPath}");

        if (!File.Exists(updateInfoPath))
        {
            App.Log($"ERROR: Update info file not found at {updateInfoPath}");
            StatusText.Text = $"Update info file not found. Please try the update again.";
            CloseButton.Visibility = Visibility.Visible;
            return;
        }

        // Read update info from JSON file
        App.Log($"Reading update info from: {updateInfoPath}");

        try
        {
            var json = File.ReadAllText(updateInfoPath);
            App.Log($"JSON content: {json}");

            var updateInfo = System.Text.Json.JsonSerializer.Deserialize<UpdateInfo>(json);
            if (updateInfo == null)
            {
                throw new Exception("Failed to deserialize update info");
            }

            _targetExePath = updateInfo.TargetExePath ?? string.Empty;
            _downloadUrl = updateInfo.DownloadUrl ?? string.Empty;
            _targetProcessName = updateInfo.ProcessName ?? string.Empty;
            _targetProcessId = updateInfo.ProcessId;

            App.Log($"Target EXE: {_targetExePath}");
            App.Log($"Download URL: {_downloadUrl}");
            App.Log($"Process Name: {_targetProcessName}");
            App.Log($"Process ID: {_targetProcessId}");
        }
        catch (Exception ex)
        {
            App.Log($"ERROR reading update info: {ex.Message}");
            StatusText.Text = $"Failed to read update info: {ex.Message}";
            CloseButton.Visibility = Visibility.Visible;
            return;
        }

        Loaded += MainWindow_Loaded;
    }

    private class UpdateInfo
    {
        public string? TargetExePath { get; set; }
        public string? DownloadUrl { get; set; }
        public string? ProcessName { get; set; }
        public int ProcessId { get; set; }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            App.Log("MainWindow loaded, starting update process");
            await PerformUpdateAsync();
        }
        catch (Exception ex)
        {
            App.Log($"ERROR: Update failed - {ex.GetType().Name}: {ex.Message}");
            App.Log($"Stack trace: {ex.StackTrace}");
            StatusText.Text = $"Update failed: {ex.Message}";
            ProgressBar.Value = 0;
            CloseButton.Visibility = Visibility.Visible;

            var logPath = Path.Combine(Path.GetTempPath(), "WinImagePrep_Updater_*.log");
            MessageBox.Show(
                $"Failed to update WinImagePrep:\n\n{ex.Message}\n\nCheck log files in %TEMP%:\n{logPath}",
                "Update Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task PerformUpdateAsync()
    {
        App.Log("PerformUpdateAsync started");
        var targetDirectory = Path.GetDirectoryName(_targetExePath);
        if (string.IsNullOrEmpty(targetDirectory))
        {
            throw new InvalidOperationException("Could not determine target directory");
        }
        App.Log($"Target directory: {targetDirectory}");

        // Step 1: Wait for the target process to exit
        UpdateStatus("Waiting for WinImagePrep to close...", 10);
        App.Log("Waiting for target process to exit");
        await WaitForProcessToExitAsync();
        App.Log("Target process exited");

        // Step 2: Download all EXE files from the publish directory
        UpdateStatus("Downloading updates...", 20);
        App.Log("Starting downloads");

        // Download main EXE
        var tempMainExe = Path.Combine(Path.GetTempPath(), $"WinImagePrep_Update_{Guid.NewGuid()}.exe");
        App.Log($"Downloading main EXE from {_downloadUrl} to {tempMainExe}");
        await DownloadFileAsync(_downloadUrl, tempMainExe, 20, 50);
        App.Log("Main EXE downloaded");

        // Download updater EXE
        var updaterUrl = _downloadUrl.Replace("WinImagePrep.exe", "WinImagePrep.Updater.exe");
        var tempUpdaterExe = Path.Combine(Path.GetTempPath(), $"WinImagePrep.Updater_Update_{Guid.NewGuid()}.exe");
        App.Log($"Downloading updater EXE from {updaterUrl} to {tempUpdaterExe}");
        await DownloadFileAsync(updaterUrl, tempUpdaterExe, 50, 70);
        App.Log("Updater EXE downloaded");

        // Step 3: Replace the old EXEs
        UpdateStatus("Installing updates...", 70);
        await Task.Delay(500); // Brief pause

        // Replace main EXE
        App.Log($"Replacing main EXE: {tempMainExe} -> {_targetExePath}");
        File.Copy(tempMainExe, _targetExePath, overwrite: true);
        File.Delete(tempMainExe);
        App.Log("Main EXE replaced");

        // Replace updater EXE (ourselves!) - we need a helper script
        var updaterPath = Path.Combine(targetDirectory, "WinImagePrep.Updater.exe");
        if (File.Exists(updaterPath))
        {
            App.Log($"Need to replace updater EXE: {tempUpdaterExe} -> {updaterPath}");

            // Create a batch script to replace the updater after we exit
            var batchScript = Path.Combine(Path.GetTempPath(), $"UpdaterSelfUpdate_{Guid.NewGuid()}.bat");
            var batchContent = $@"@echo off
timeout /t 3 /nobreak >nul
copy /Y ""{tempUpdaterExe}"" ""{updaterPath}""
if exist ""{tempUpdaterExe}"" del ""{tempUpdaterExe}""
if exist ""{batchScript}"" del ""{batchScript}""
start """" ""{_targetExePath}""
exit
";
            File.WriteAllText(batchScript, batchContent);
            App.Log($"Created self-update batch script: {batchScript}");

            // Launch the batch script and exit
            App.Log("Launching self-update script");
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{batchScript}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            // Don't delete temp updater - the batch script will do it
            // Don't start main app - the batch script will do it
            App.Log("Self-update script launched, updater will exit now");
            _updateSuccessful = true;
            UpdateStatus("Update complete! Restarting...", 100);
            await Task.Delay(500);
            Application.Current.Shutdown(0);
            return; // Exit immediately
        }
        File.Delete(tempUpdaterExe);

        // Step 4: Restart WinImagePrep
        UpdateStatus("Starting WinImagePrep...", 90);
        await Task.Delay(500);
        App.Log($"Starting WinImagePrep: {_targetExePath}");
        Process.Start(new ProcessStartInfo
        {
            FileName = _targetExePath,
            UseShellExecute = true
        });
        App.Log("WinImagePrep started");

        // Step 5: Complete
        UpdateStatus("Update complete!", 100);
        _updateSuccessful = true;
        App.Log("Update completed successfully");
        await Task.Delay(1000);
        Application.Current.Shutdown(0);
    }

    private async Task WaitForProcessToExitAsync()
    {
        var timeout = TimeSpan.FromSeconds(30);
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                // Try to find the process by ID first (most reliable)
                var process = Process.GetProcessById(_targetProcessId);
                if (process.HasExited)
                {
                    break;
                }

                await Task.Delay(500);
            }
            catch (ArgumentException)
            {
                // Process with that ID doesn't exist anymore - it's gone
                break;
            }
        }

        // Extra safety: check by name as well
        var remainingProcesses = Process.GetProcessesByName(_targetProcessName);
        foreach (var proc in remainingProcesses)
        {
            try
            {
                if (!proc.HasExited)
                {
                    proc.WaitForExit(5000);
                }
            }
            catch { /* ignore */ }
            finally
            {
                proc.Dispose();
            }
        }

        // Give file system a moment to release locks
        await Task.Delay(1000);
    }

    private async Task DownloadFileAsync(string url, string destinationPath, int progressStart = 0, int progressEnd = 100)
    {
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromMinutes(5);

        var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        var canReportProgress = totalBytes > 0;
        var progressRange = progressEnd - progressStart;

        await using var contentStream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        var buffer = new byte[8192];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
            totalRead += bytesRead;

            if (canReportProgress)
            {
                var downloadProgress = (double)totalRead / totalBytes;
                var progressPercent = progressStart + (downloadProgress * progressRange);
                Dispatcher.Invoke(() => ProgressBar.Value = progressPercent);
            }
        }
    }

    private void UpdateStatus(string message, int progress)
    {
        StatusText.Text = message;
        ProgressBar.Value = progress;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown(_updateSuccessful ? 0 : 1);
    }
}
