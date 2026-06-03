using CommunityToolkit.Mvvm.ComponentModel;
using System.Reflection;
using WinImagePrep.Services;

namespace WinImagePrep.ViewModels
{
    /// <summary>
    /// ViewModel for the About dialog
    /// </summary>
    public partial class AboutViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _workingFolder = string.Empty;

        [ObservableProperty]
        private string _version = string.Empty;

        public AboutViewModel(ISettingsService settingsService)
        {
            WorkingFolder = settingsService.CurrentSettings.WorkingRoot;

            // Get version from assembly
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            Version = version != null ? $"Version {version.Major}.{version.Minor}.{version.Build}" : "Version Unknown";
        }
    }
}
