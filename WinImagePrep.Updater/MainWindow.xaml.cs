using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace WinImagePrep.Updater;

/// <summary>
/// Updater window that downloads and applies updates for WinImagePrep
/// </summary>
public partial class MainWindow : Window
{
    private readonly string _targetExePath;
    private readonly string _downloadUrl;
    private readonly string _targetProcessName;
    private readonly int _targetProcessId;
    private bool _updateSuccessful = false;

    public MainWindow()
    {
        InitializeComponent();

        // Parse command line args: updater.exe <targetExePath> <downloadUrl> <processName> <processId>
        var args = Environment.GetCommandLineArgs();

        if (args.Length < 5)
        {
            MessageBox.Show(
                "Invalid arguments. Usage: updater.exe <targetExePath> <downloadUrl> <processName> <processId>",
                "Updater Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Application.Current.Shutdown(1);
            return;
        }

        _targetExePath = args[1];
        _downloadUrl = args[2];
        _targetProcessName = args[3];
        _targetProcessId = int.Parse(args[4]);

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await PerformUpdateAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Update failed: {ex.Message}";
            ProgressBar.Value = 0;
            CloseButton.Visibility = Visibility.Visible;
            MessageBox.Show(
                $"Failed to update WinImagePrep:\n\n{ex.Message}",
                "Update Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task PerformUpdateAsync()
    {
        var targetDirectory = Path.GetDirectoryName(_targetExePath);
        if (string.IsNullOrEmpty(targetDirectory))
        {
            throw new InvalidOperationException("Could not determine target directory");
        }

        // Step 1: Wait for the target process to exit
        UpdateStatus("Waiting for WinImagePrep to close...", 10);
        await WaitForProcessToExitAsync();

        // Step 2: Download all EXE files from the publish directory
        UpdateStatus("Downloading updates...", 20);

        // Download main EXE
        var tempMainExe = Path.Combine(Path.GetTempPath(), $"WinImagePrep_Update_{Guid.NewGuid()}.exe");
        await DownloadFileAsync(_downloadUrl, tempMainExe, 20, 50);

        // Download updater EXE
        var updaterUrl = _downloadUrl.Replace("WinImagePrep.exe", "WinImagePrep.Updater.exe");
        var tempUpdaterExe = Path.Combine(Path.GetTempPath(), $"WinImagePrep.Updater_Update_{Guid.NewGuid()}.exe");
        await DownloadFileAsync(updaterUrl, tempUpdaterExe, 50, 70);

        // Step 3: Replace the old EXEs
        UpdateStatus("Installing updates...", 70);
        await Task.Delay(500); // Brief pause

        // Replace main EXE
        File.Copy(tempMainExe, _targetExePath, overwrite: true);
        File.Delete(tempMainExe);

        // Replace updater EXE (ourselves!)
        var updaterPath = Path.Combine(targetDirectory, "WinImagePrep.Updater.exe");
        if (File.Exists(updaterPath))
        {
            File.Copy(tempUpdaterExe, updaterPath, overwrite: true);
        }
        File.Delete(tempUpdaterExe);

        // Step 4: Restart WinImagePrep
        UpdateStatus("Starting WinImagePrep...", 90);
        await Task.Delay(500);
        Process.Start(new ProcessStartInfo
        {
            FileName = _targetExePath,
            UseShellExecute = true
        });

        // Step 5: Complete
        UpdateStatus("Update complete!", 100);
        _updateSuccessful = true;
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