using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WinImagePrep.Models;

namespace WinImagePrep
{
    /// <summary>
    /// Dialog for configuring unattended Windows installation options
    /// </summary>
    public partial class UnattendedConfigDialog : Window
    {
        public UnattendedConfig Config { get; private set; }

        public UnattendedConfigDialog(UnattendedConfig? existingConfig = null)
        {
            InitializeComponent();

            // Load defaults or existing configuration
            Config = existingConfig?.Clone() ?? new UnattendedConfig();

            InitializeComboBoxes();
            LoadConfiguration();
        }

        private void InitializeComboBoxes()
        {
            // Populate UI Language combo
            foreach (var locale in UnattendedConfig.CommonLocales)
            {
                UILanguageComboBox.Items.Add(locale);
            }

            // Populate Time Zone combo
            foreach (var tz in UnattendedConfig.CommonTimeZones)
            {
                TimeZoneComboBox.Items.Add(tz);
            }
        }

        private void LoadConfiguration()
        {
            // Edition
            if (!string.IsNullOrWhiteSpace(Config.TargetEdition))
            {
                EditionComboBox.Text = Config.TargetEdition;
            }

            // Locales
            UILanguageComboBox.SelectedItem = Config.UILanguage;
            if (UILanguageComboBox.SelectedItem == null)
                UILanguageComboBox.SelectedIndex = 0; // Default to first item

            // Time Zone
            TimeZoneComboBox.SelectedItem = Config.TimeZone;
            if (TimeZoneComboBox.SelectedItem == null)
                TimeZoneComboBox.SelectedIndex = 0; // Default to first item

            // Admin account
            AdminUsernameTextBox.Text = Config.AdminUsername;
            AdminPasswordBox.Password = Config.AdminPassword;

            // Computer name
            ComputerNameTextBox.Text = Config.ComputerName ?? string.Empty;

            // Disk configuration
            AutoPartitionCheckBox.IsChecked = Config.AutoPartitionDisk;
            DiskIdTextBox.Text = Config.TargetDiskId.ToString();

            // OOBE settings
            HideEULACheckBox.IsChecked = Config.HideEULA;
            HideWirelessCheckBox.IsChecked = Config.HideWirelessSetup;
            SkipOOBECheckBox.IsChecked = Config.SkipOOBE;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate
                if (string.IsNullOrWhiteSpace(AdminUsernameTextBox.Text))
                {
                    MessageBox.Show(
                        "Administrator username is required.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (!int.TryParse(DiskIdTextBox.Text, out int diskId) || diskId < 0)
                {
                    MessageBox.Show(
                        "Disk ID must be a valid non-negative number (usually 0).",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Save configuration
                var editionText = EditionComboBox.Text;
                if (editionText == "(Auto-detect / User will select)" || string.IsNullOrWhiteSpace(editionText))
                {
                    Config.TargetEdition = null; // Will prompt user during install
                }
                else
                {
                    Config.TargetEdition = editionText;
                }

                Config.UILanguage = UILanguageComboBox.SelectedItem?.ToString() ?? "en-US";
                Config.InputLocale = Config.UILanguage; // Use same as UI language
                Config.SystemLocale = Config.UILanguage;
                Config.UserLocale = Config.UILanguage;
                Config.TimeZone = TimeZoneComboBox.SelectedItem?.ToString() ?? "GMT Standard Time";

                Config.AdminUsername = AdminUsernameTextBox.Text.Trim();
                Config.AdminPassword = AdminPasswordBox.Password;

                var computerName = ComputerNameTextBox.Text.Trim();
                Config.ComputerName = string.IsNullOrWhiteSpace(computerName) ? null : computerName;

                Config.AutoPartitionDisk = AutoPartitionCheckBox.IsChecked == true;
                Config.TargetDiskId = diskId;

                Config.HideEULA = HideEULACheckBox.IsChecked == true;
                Config.HideWirelessSetup = HideWirelessCheckBox.IsChecked == true;
                Config.SkipOOBE = SkipOOBECheckBox.IsChecked == true;

                // Show confirmation if auto-partition is enabled
                if (Config.AutoPartitionDisk)
                {
                    var result = MessageBox.Show(
                        $"⚠️ FINAL WARNING ⚠️\n\n" +
                        $"Unattended installation is configured to automatically:\n\n" +
                        $"• WIPE ALL DATA on Disk {Config.TargetDiskId}\n" +
                        $"• Delete all existing partitions\n" +
                        $"• Create fresh partitions\n" +
                        $"• Install Windows without asking for confirmation\n\n" +
                        $"This action CANNOT be undone!\n\n" +
                        $"Are you absolutely sure you want to proceed?",
                        "Confirm Destructive Action",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error saving configuration: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
