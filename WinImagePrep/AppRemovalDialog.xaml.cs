using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using WinImagePrep.Models;

namespace WinImagePrep
{
    public partial class AppRemovalDialog : Window
    {
        public ObservableCollection<WindowsApp> WindowsApps { get; set; }

        // Apps to keep as "essential" - these are useful system utilities
        private static readonly string[] EssentialApps = new[]
        {
            "Calculator",
            "Notepad",
            "ScreenSketch", // Snipping Tool
            "Paint",
            "Photos",
            "MSPaint",
            "WindowsTerminal",
            "WindowsNotepad",
            "WindowsCalculator",
            "DesktopAppInstaller", // WinGet
            "Store", // Microsoft Store
            "HEIFImageExtension",
            "HEVCVideoExtension",
            "VP9VideoExtensions",
            "WebpImageExtension",
            "WebMediaExtensions",
            "RawImageExtension",
            "AV1VideoExtension",
            "AVCEncoderVideoExtension",
            "MPEG2VideoExtension"
        };

        // Known bloatware/social/gaming apps
        private static readonly string[] BloatwareKeywords = new[]
        {
            "Xbox",
            "Solitaire",
            "Candy",
            "BingNews",
            "BingWeather",
            "BingSearch",
            "GetHelp",
            "Getstarted",
            "Tips",
            "Messaging",
            "MixedReality",
            "People",
            "Skype",
            "YourPhone",
            "Phone",
            "Zune",
            "Music",
            "Video",
            "Clipchamp",
            "Feedback",
            "Maps",
            "SoundRecorder",
            "Alarms",
            "Camera",
            "Sticky",
            "Wallet",
            "Cortana",
            "DevHome",
            "QuickAssist",
            "Teams", // MSTeams
            "MicrosoftTeams",
            "MSTeams",
            "OneDrive",
            "OneNote",
            "Outlook",
            "ToDo",
            "PowerAutomate",
            "Family",
            "CrossDevice"
        };

        public AppRemovalDialog(ObservableCollection<WindowsApp> apps)
        {
            InitializeComponent();

            // Create a working copy of the apps list
            WindowsApps = new ObservableCollection<WindowsApp>(
                apps.Select(a => new WindowsApp
                {
                    PackageName = a.PackageName,
                    DisplayName = a.DisplayName,
                    Description = a.Description,
                    IsSelected = a.IsSelected,
                    PackageNames = a.PackageNames
                })
            );

            lstApps.ItemsSource = WindowsApps;
            UpdateSelectedCount();
        }

        private void UpdateSelectedCount()
        {
            var count = WindowsApps.Count(a => a.IsSelected);
            txtSelectedCount.Text = $"{count} of {WindowsApps.Count} selected";

            // Update the Select All checkbox state without triggering events
            chkSelectAll.Checked -= SelectAll_Checked;
            chkSelectAll.Unchecked -= SelectAll_Unchecked;

            if (count == 0)
                chkSelectAll.IsChecked = false;
            else if (count == WindowsApps.Count)
                chkSelectAll.IsChecked = true;
            else
                chkSelectAll.IsChecked = null; // Indeterminate state

            chkSelectAll.Checked += SelectAll_Checked;
            chkSelectAll.Unchecked += SelectAll_Unchecked;
        }

        private void SelectAll_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var app in WindowsApps)
            {
                app.IsSelected = true;
            }
            UpdateSelectedCount();
        }

        private void SelectAll_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (var app in WindowsApps)
            {
                app.IsSelected = false;
            }
            UpdateSelectedCount();
        }

        private void AppCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateSelectedCount();
        }

        private void SelectBloatware_Click(object sender, RoutedEventArgs e)
        {
            // Deselect all first
            foreach (var app in WindowsApps)
            {
                app.IsSelected = false;
            }

            // Select only bloatware (non-essential apps)
            foreach (var app in WindowsApps)
            {
                var isEssential = EssentialApps.Any(essential =>
                    app.DisplayName.Contains(essential, System.StringComparison.OrdinalIgnoreCase) ||
                    app.PackageName.Contains(essential, System.StringComparison.OrdinalIgnoreCase));

                if (!isEssential)
                {
                    // Check if it matches bloatware keywords
                    var isBloatware = BloatwareKeywords.Any(keyword =>
                        app.DisplayName.Contains(keyword, System.StringComparison.OrdinalIgnoreCase) ||
                        app.PackageName.Contains(keyword, System.StringComparison.OrdinalIgnoreCase));

                    if (isBloatware)
                    {
                        app.IsSelected = true;
                    }
                }
            }

            UpdateSelectedCount();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var app in WindowsApps)
            {
                app.IsSelected = true;
            }
            UpdateSelectedCount();
        }

        private void SelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var app in WindowsApps)
            {
                app.IsSelected = false;
            }
            UpdateSelectedCount();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
