using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using WinImagePrep.Models;
using WinImagePrep.Services;

namespace WinImagePrep.Dialogs
{
    public partial class SavedImageWindow : Window
    {
        private readonly string _savedImagesDir;
        private readonly UsbService _usbService;

        public SavedImageWindow(string savedImagesDirectory)
        {
            InitializeComponent();
            _savedImagesDir = savedImagesDirectory;
            _usbService = new UsbService();

            LoadSavedImages();
            RefreshUsbDrives();
        }

        private void LoadSavedImages()
        {
            cmbSavedImages.Items.Clear();

            if (!Directory.Exists(_savedImagesDir))
            {
                Directory.CreateDirectory(_savedImagesDir);
                AddLog("No saved images found");
                return;
            }

            var savedDirs = Directory.GetDirectories(_savedImagesDir);

            if (savedDirs.Length == 0)
            {
                cmbSavedImages.Items.Add("No saved images found");
                cmbSavedImages.SelectedIndex = 0;
                cmbSavedImages.IsEnabled = false;
                AddLog("No saved images found");
                return;
            }

            foreach (var dir in savedDirs)
            {
                cmbSavedImages.Items.Add(Path.GetFileName(dir));
            }

            cmbSavedImages.SelectedIndex = 0;
            AddLog($"Found {savedDirs.Length} saved image(s)");
        }

        private void RefreshUsbDrives()
        {
            cmbUsbDrives.Items.Clear();
            var usbDrives = _usbService.GetUsbDrives();

            if (usbDrives.Count == 0)
            {
                cmbUsbDrives.Items.Add("No USB drives detected");
                cmbUsbDrives.SelectedIndex = 0;
                cmbUsbDrives.IsEnabled = false;
                AddLog("No USB drives detected");
            }
            else
            {
                foreach (var drive in usbDrives)
                {
                    cmbUsbDrives.Items.Add(drive);
                }
                cmbUsbDrives.DisplayMemberPath = "DisplayName";
                cmbUsbDrives.SelectedIndex = 0;
                cmbUsbDrives.IsEnabled = true;
                AddLog($"Found {usbDrives.Count} USB drive(s)");
            }
        }

        private void CmbSavedImages_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cmbSavedImages.SelectedItem is string selectedImage && 
                !selectedImage.Contains("No saved images"))
            {
                var imagePath = Path.Combine(_savedImagesDir, selectedImage);
                var labelFile = Path.Combine(imagePath, "iso-label.txt");

                if (File.Exists(labelFile))
                {
                    lblImageLabel.Text = File.ReadAllText(labelFile).Trim();
                }
                else
                {
                    lblImageLabel.Text = "(no label found)";
                }
            }
        }

        private void BtnRefreshUsb_Click(object sender, RoutedEventArgs e)
        {
            RefreshUsbDrives();
        }

        private async void BtnCreateUsb_Click(object sender, RoutedEventArgs e)
        {
            if (cmbSavedImages.SelectedItem is not string selectedImage || 
                selectedImage.Contains("No saved images"))
            {
                MessageBox.Show(
                    "Please select a saved image.",
                    "No Image Selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (cmbUsbDrives.SelectedItem is not UsbDriveInfo usbDrive)
            {
                MessageBox.Show(
                    "Please insert a USB drive and click Refresh.",
                    "No USB Drive",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (usbDrive.SizeGB < 14)
            {
                MessageBox.Show(
                    "Selected USB drive is less than 14GB. Please use a larger drive.",
                    "USB Too Small",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"WARNING: This will ERASE all data on USB drive {usbDrive.DiskNumber}!\n\n" +
                $"{usbDrive.FriendlyName} - {usbDrive.SizeGB} GB\n\n" +
                "Do you want to continue?",
                "Confirm USB Creation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            await CreateUsbFromSavedImageAsync(selectedImage, usbDrive);
        }

        private async Task CreateUsbFromSavedImageAsync(string imageName, UsbDriveInfo usbDrive)
        {
            try
            {
                AddLog("Starting USB creation...");

                var imagePath = Path.Combine(_savedImagesDir, imageName);
                var labelFile = Path.Combine(imagePath, "iso-label.txt");
                var label = File.Exists(labelFile) ? File.ReadAllText(labelFile).Trim() : "WIN11USB";

                var progress = new Progress<OperationProgress>(p => 
                {
                    AddLog($"[{p.PercentComplete}%] {p.CurrentOperation}");
                });

                var success = await _usbService.CreateBootableUsbAsync(
                    usbDrive.DiskNumber,
                    imagePath,
                    label,
                    progress);

                if (success)
                {
                    AddLog("✓ USB creation complete!");
                    MessageBox.Show(
                        "Bootable Windows 11 USB created successfully!",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    Close();
                }
                else
                {
                    AddLog("✗ USB creation failed");
                    MessageBox.Show(
                        "Failed to create bootable USB. Check the log for details.",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                AddLog($"✗ Error: {ex.Message}");
                MessageBox.Show(
                    $"An error occurred:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void AddLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                lstLog.Items.Add($"[{timestamp}] {message}");
                lstLog.ScrollIntoView(lstLog.Items[lstLog.Items.Count - 1]);
            });
        }
    }
}
