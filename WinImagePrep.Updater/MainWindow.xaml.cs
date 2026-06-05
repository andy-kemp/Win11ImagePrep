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
        App.Log("MainWindow constructor started");

        try
        {
            InitializeComponent();
            App.Log("InitializeComponent completed");
        }
        catch (Exception ex)
        {
            App.Log($"ERROR in InitializeComponent: {ex.GetType().Name}: {ex.Message}");
            App.Log($"Stack trace: {ex.StackTrace}");
            throw;
        }

        // Parse command line args: updater.exe <targetExePath> <downloadUrl> <processName> <processId>
        var args = Environment.GetCommandLineArgs();
        App.Log($"Command line args count: {args.Length}");
        for (int i = 0; i < args.Length; i++)
        {
            App.Log($"  args[{i}] = '{args[i]}'");
        }

        if (args.Length < 5)
        {
            App.Log($"ERROR: Invalid arguments. Expected 5, got {args.Length}");
            var errorMsg = $"Invalid arguments.\n\nExpected: updater.exe <targetExePath> <downloadUrl> <processName> <processId>\n\n" +
                          $"Received {args.Length} arguments:\n" + string.Join("\n", args.Select((a, i) => $"[{i}] {a}"));

            App.Log($"Showing error dialog: {errorMsg}");
            MessageBox.Show(errorMsg, "Updater Error", MessageBoxButton.OK, MessageBoxImage.Error);

            // Keep window open to see the error
            StatusText.Text = "Invalid command-line arguments. See logs in %TEMP%\\WinImagePrep_Updater_*.log";
            CloseButton.Visibility = Visibility.Visible;
            return;
        }

        _targetExePath = args[1];
        _downloadUrl = args[2];
        _targetProcessName = args[3];
        _targetProcessId = int.Parse(args[4]);

        App.Log($"Target EXE: {_targetExePath}");
        App.Log($"Download URL: {_downloadUrl}");
        App.Log($"Process Name: {_targetProcessName}");
        App.Log($"Process ID: {_targetProcessId}");

        Loaded += MainWindow_Loaded;
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

        // Replace updater EXE (ourselves!)
        var updaterPath = Path.Combine(targetDirectory, "WinImagePrep.Updater.exe");
        if (File.Exists(updaterPath))
        {
            App.Log($"Replacing updater EXE: {tempUpdaterExe} -> {updaterPath}");
            File.Copy(tempUpdaterExe, updaterPath, overwrite: true);
            App.Log("Updater EXE replaced");
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
