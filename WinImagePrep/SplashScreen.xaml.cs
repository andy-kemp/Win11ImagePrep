using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace WinImagePrep
{
    public partial class SplashScreen : Window
    {
        public SplashScreen()
        {
            InitializeComponent();

            // Set version from assembly
            try
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                txtVersion.Text = version != null ? $"Version {version.Major}.{version.Minor}.{version.Build}" : "Version";
            }
            catch
            {
                txtVersion.Text = "Version";
            }
        }

        /// <summary>Turns the splash into the update window: "Updating", from and to versions, and a progress bar.</summary>
        public void ShowUpdate(string from, string to)
        {
            Title = "Updating Win11 Image Prep";
            txtUpdating.Visibility = Visibility.Visible;
            txtVersion.Text = $"Version {from} \u2192 {to}";
            txtTagline.Text = "The new version is checked against its published SHA-256 before it replaces this one. The app restarts when it's done.";
            Topmost = true;
        }

        /// <summary>Null shows the moving bar; a number (0-100) shows how far it's got.</summary>
        public void SetProgress(double? percent)
        {
            Dispatcher.Invoke(() =>
            {
                progressBar.IsIndeterminate = percent is null;
                if (percent is double p) progressBar.Value = Math.Clamp(p, 0, 100);
            });
        }

        public void UpdateStatus(string status)
        {
            Dispatcher.Invoke(() =>
            {
                txtStatus.Text = status;
            });
        }
    }
}
