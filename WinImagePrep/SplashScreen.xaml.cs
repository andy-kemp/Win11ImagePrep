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
                txtVersion.Text = $"Version 3.{version?.Minor ?? 0}.{version?.Build ?? 0}";
            }
            catch
            {
                txtVersion.Text = "Version 3.0.0";
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
