using CommunityToolkit.Mvvm.ComponentModel;
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

        public AboutViewModel(ISettingsService settingsService)
        {
            WorkingFolder = settingsService.CurrentSettings.WorkingRoot;
        }
    }
}
