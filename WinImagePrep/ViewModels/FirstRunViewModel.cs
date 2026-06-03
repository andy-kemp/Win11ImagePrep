using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using WinImagePrep.Models;
using WinImagePrep.Services;

namespace WinImagePrep.ViewModels
{
    /// <summary>
    /// ViewModel for the first-run welcome window
    /// </summary>
    public partial class FirstRunViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;

        [ObservableProperty]
        private string _workingFolder = string.Empty;

        public FirstRunViewModel(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            WorkingFolder = _settingsService.CurrentSettings.WorkingRoot;
        }

        /// <summary>
        /// Opens the local User Guide HTML file
        /// </summary>
        [RelayCommand]
        private void OpenUserGuide()
        {
            try
            {
                var userGuidePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "docs", "UserGuide.html");

                if (File.Exists(userGuidePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = userGuidePath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    // Fallback to online documentation
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://docs.andykemp.com/win11-image-prep/",
                        UseShellExecute = true
                    });
                    MessageBox.Show(
                        "Local user guide not found. Opening online documentation instead.",
                        "User Guide",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not open User Guide: {ex.Message}\n\nVisit: https://docs.andykemp.com/win11-image-prep/",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Opens the Options window
        /// </summary>
        [RelayCommand]
        private void OpenOptions()
        {
            try
            {
                var optionsViewModel = new OptionsViewModel(_settingsService);
                var optionsWindow = new OptionsWindow(optionsViewModel);
                var result = optionsWindow.ShowDialog();

                if (result == true)
                {
                    // Reload working folder if changed
                    WorkingFolder = _settingsService.CurrentSettings.WorkingRoot;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not open Options: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
