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
                txtVersion.Text = $"Version {version?.Major ?? 4}.{version?.Minor ?? 0}.{version?.Build ?? 1}";
            }
            catch
            {
                txtVersion.Text = "Version 4.0.1";
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
