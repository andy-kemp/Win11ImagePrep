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

        public void UpdateStatus(string status)
        {
            Dispatcher.Invoke(() =>
            {
                txtStatus.Text = status;
            });
        }
    }
}
