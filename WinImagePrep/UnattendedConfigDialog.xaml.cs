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
            // Autopilot mode
            AutopilotModeCheckBox.IsChecked = Config.AutopilotMode;
            UpdateAutopilotUI();

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

        private void AutopilotMode_Changed(object sender, RoutedEventArgs e)
        {
            UpdateAutopilotUI();
        }

        private void UpdateAutopilotUI()
        {
            bool isAutopilot = AutopilotModeCheckBox.IsChecked == true;

            // Hide/show panels based on Autopilot mode
            LocalAdminPanel.Visibility = isAutopilot ? Visibility.Collapsed : Visibility.Visible;
            ComputerNamePanel.Visibility = isAutopilot ? Visibility.Collapsed : Visibility.Visible;
            SetupExperiencePanel.Visibility = isAutopilot ? Visibility.Collapsed : Visibility.Visible;

            // Hide disk configuration in Autopilot mode (manual partition deletion required)
            if (DiskConfigPanel != null)
            {
                DiskConfigPanel.Visibility = isAutopilot ? Visibility.Collapsed : Visibility.Visible;
            }

            // Force sensible defaults for Autopilot
            if (isAutopilot)
            {
                AutoPartitionCheckBox.IsChecked = false; // No auto-partition in Autopilot mode
                HideEULACheckBox.IsChecked = true;
                HideWirelessCheckBox.IsChecked = false;
                SkipOOBECheckBox.IsChecked = false;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool isAutopilot = AutopilotModeCheckBox.IsChecked == true;

                // Validate (skip admin username check in Autopilot mode)
                if (!isAutopilot && string.IsNullOrWhiteSpace(AdminUsernameTextBox.Text))
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
                Config.AutopilotMode = isAutopilot;

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

                if (!isAutopilot)
                {
                    Config.AdminUsername = AdminUsernameTextBox.Text.Trim();
                    Config.AdminPassword = AdminPasswordBox.Password;

                    var computerName = ComputerNameTextBox.Text.Trim();
                    Config.ComputerName = string.IsNullOrWhiteSpace(computerName) ? null : computerName;

                    Config.HideEULA = HideEULACheckBox.IsChecked == true;
                    Config.HideWirelessSetup = HideWirelessCheckBox.IsChecked == true;
                    Config.SkipOOBE = SkipOOBECheckBox.IsChecked == true;
                }
                else
                {
                    // Autopilot mode: force sensible defaults
                    Config.ComputerName = null; // Autopilot will set this
                    Config.HideEULA = true;
                    Config.HideWirelessSetup = false;
                    Config.SkipOOBE = false;
                }

                Config.AutoPartitionDisk = AutoPartitionCheckBox.IsChecked == true;
                Config.TargetDiskId = diskId;

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
                else if (isAutopilot)
                {
                    // Info for Autopilot mode
                    MessageBox.Show(
                        "📋 Autopilot Mode - Manual Partition Deletion Required\n\n" +
                        "During Windows installation, you will need to:\n\n" +
                        "1. Boot from this USB drive\n" +
                        "2. Select language and proceed\n" +
                        "3. At the disk selection screen, manually DELETE all existing partitions\n" +
                        "4. Windows will create fresh partitions automatically\n" +
                        "5. Complete Autopilot enrollment after installation\n\n" +
                        "The autounattend.xml will handle EULA acceptance and language settings only.",
                        "Autopilot Information",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
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
