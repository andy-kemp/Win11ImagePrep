using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using WinImagePrep.Models;

namespace WinImagePrep
{
    public partial class AppRemovalDialog : Window
    {
        public ObservableCollection<WindowsApp> WindowsApps { get; set; }

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
                    IsSelected = a.IsSelected
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
