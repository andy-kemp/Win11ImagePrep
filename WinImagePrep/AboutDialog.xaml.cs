using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using WinImagePrep.Services;
using WinImagePrep.ViewModels;

namespace WinImagePrep
{
    public partial class AboutDialog : Window
    {
        public AboutDialog(ISettingsService settingsService)
        {
            InitializeComponent();
            DataContext = new AboutViewModel(settingsService);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OpenWebsite_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://www.andykemp.com");
        }

        private void OpenDocumentation_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://docs.andykemp.com/win11-image-prep/");
        }

        private void WebsiteLink_Click(object sender, MouseButtonEventArgs e)
        {
            OpenUrl("https://www.andykemp.com");
        }

        private void DocumentationLink_Click(object sender, MouseButtonEventArgs e)
        {
            OpenUrl("https://docs.andykemp.com/win11-image-prep/");
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch
            {
                MessageBox.Show($"Could not open URL: {url}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
