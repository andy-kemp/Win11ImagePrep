using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Navigation;
using WinImagePrep.Services;
using WinImagePrep.ViewModels;

namespace WinImagePrep
{
    public partial class AboutDialog : Window
    {
        private readonly AboutViewModel _viewModel;

        public AboutDialog(ISettingsService settingsService)
        {
            InitializeComponent();
            _viewModel = new AboutViewModel(settingsService);
            DataContext = _viewModel;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void OpenWebsite_Click(object sender, RoutedEventArgs e) => OpenUrl("https://www.andykemp.com");

        private void OpenDocumentation_Click(object sender, RoutedEventArgs e) => OpenUrl("https://docs.andykemp.com/win11-image-prep/");

        private void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            OpenUrl(e.Uri.AbsoluteUri);
            e.Handled = true;
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var folder = _viewModel.WorkingFolder;
            try
            {
                if (string.IsNullOrWhiteSpace(folder)) return;
                Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
            }
            catch
            {
                MessageBox.Show($"Could not open {folder}", "Win11 Image Prep", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch
            {
                MessageBox.Show($"Could not open {url}", "Win11 Image Prep", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
