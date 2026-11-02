using System;
using System.Threading.Tasks;
using System.Windows;
using WinImagePrep.Services;

namespace WinImagePrep.Helpers
{
    /// <summary>
    /// Makes sure oscdimg.exe (Windows ADK Deployment Tools) is available before creating an ISO,
    /// offering to install it. Shared by the main window and the saved-image window.
    /// </summary>
    public static class IsoToolsPrompt
    {
        /// <summary>
        /// Return the path to oscdimg.exe, offering to install the Deployment Tools if they're missing.
        /// Returns null if they aren't installed (declined, cancelled or failed).
        /// </summary>
        /// <param name="log">Receives progress lines for the caller's log.</param>
        /// <param name="setBusy">Called with true while installing and false afterwards, to disable buttons.</param>
        public static async Task<string?> EnsureAsync(IsoService isoService, string? adkSetupUrl, Action<string> log, Action<bool>? setBusy = null)
        {
            var existing = isoService.FindOscdimgPath();
            if (!string.IsNullOrEmpty(existing))
            {
                log($"✓ Found oscdimg at: {existing}");
                return existing;
            }

            var answer = MessageBox.Show(
                "Creating an ISO file needs oscdimg.exe, part of Microsoft's Windows ADK Deployment Tools, " +
                "and it isn't installed.\n\n" +
                "Install it now? Win11 Image Prep downloads the setup from Microsoft, checks it is signed by " +
                "Microsoft, and installs only the Deployment Tools. It takes a few minutes, and Windows may ask " +
                "for administrator permission.\n\n" +
                "Yes: install now\nNo: open Microsoft's download page instead",
                "Windows ADK Deployment Tools needed",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (answer == MessageBoxResult.No)
            {
                OpenUrl(PrerequisiteService.AdkDownloadPage, log);
                return null;
            }
            if (answer != MessageBoxResult.Yes)
            {
                return null;
            }

            return await InstallAsync(isoService, adkSetupUrl, log, setBusy);
        }

        public static async Task<string?> InstallAsync(IsoService isoService, string? adkSetupUrl, Action<string> log, Action<bool>? setBusy = null)
        {
            setBusy?.Invoke(true);
            try
            {
                var path = await new PrerequisiteService(isoService).InstallAdkDeploymentToolsAsync(
                    adkSetupUrl, new Progress<string>(log));

                if (path == null)
                {
                    MessageBox.Show(
                        "The Deployment Tools didn't install. The log has the details.\n\n" +
                        "You can install them yourself from Microsoft's Windows ADK page: choose only " +
                        "'Deployment Tools'.",
                        "Install failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                return path;
            }
            catch (OperationCanceledException ex)
            {
                log($"ADK install cancelled: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                log($"✗ ADK install failed: {ex.Message}");
                MessageBox.Show($"The Deployment Tools couldn't be installed:\n\n{ex.Message}",
                    "Install failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
            finally
            {
                setBusy?.Invoke(false);
            }
        }

        private static void OpenUrl(string url, Action<string> log)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                log($"Failed to open browser: {ex.Message}");
            }
        }
    }
}
