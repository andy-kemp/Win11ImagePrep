using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using WinImagePrep.Helpers;
using WinImagePrep.Models;
using WinImagePrep.Services;

namespace WinImagePrep.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly AppConfiguration _config;
        private readonly IsoService _isoService;
        private readonly DriverService _driverService;
        private readonly DismService _dismService;
        private readonly UsbService _usbService;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _disposed;

        public MainViewModel()
        {
            _config = new AppConfiguration();
            _isoService = new IsoService();
            _driverService = new DriverService();
            _dismService = new DismService();
            _usbService = new UsbService();

            // Initialize commands
            BrowseIsoCommand = new RelayCommand(BrowseIso);
            VerifyIsoCommand = new RelayCommand(VerifyIso, () => !string.IsNullOrEmpty(SelectedIsoPath));
            BrowseMsiCommand = new RelayCommand(BrowseMsi);
            SelectEditionsCommand = new RelayCommand(SelectEditions, () => !string.IsNullOrEmpty(SelectedIsoPath));
            InjectDriversCommand = new RelayCommand(async () => await InjectDriversAsync(), CanExecuteInject);
            CreateUsbCommand = new RelayCommand(async () => await CreateUsbAsync(), CanCreateUsb);
            FromSavedImageCommand = new RelayCommand(OpenSavedImageDialog);
            RefreshUsbCommand = new RelayCommand(RefreshUsbDrives);
            RepairCleanupCommand = new RelayCommand(RepairCleanup);

            // Initialize collections
            LogEntries = new ObservableCollection<string>();
            UsbDrives = new ObservableCollection<UsbDriveInfo>();

            // Load initial USB drives
            RefreshUsbDrives();

            AddLog("Windows Image Preparation Tool - Ready");
            AddLog("Please select a Windows ISO and driver MSI file to begin");
        }

        #region Properties

        private string _selectedIsoPath = string.Empty;
        public string SelectedIsoPath
        {
            get => _selectedIsoPath;
            set
            {
                if (SetProperty(ref _selectedIsoPath, value))
                {
                    OnPropertyChanged(nameof(CanExecuteInject));
                }
            }
        }

        private string _isoVolumeLabel = "WIN11USB";
        public string IsoVolumeLabel
        {
            get => _isoVolumeLabel;
            set => SetProperty(ref _isoVolumeLabel, value);
        }

        private string _selectedMsiPath = string.Empty;
        public string SelectedMsiPath
        {
            get => _selectedMsiPath;
            set
            {
                if (SetProperty(ref _selectedMsiPath, value))
                {
                    OnPropertyChanged(nameof(CanExecuteInject));
                }
            }
        }

        private UsbDriveInfo? _selectedUsbDrive;
        public UsbDriveInfo? SelectedUsbDrive
        {
            get => _selectedUsbDrive;
            set
            {
                if (SetProperty(ref _selectedUsbDrive, value))
                {
                    UpdateUsbInfo();
                }
            }
        }

        private string _usbInfo = "No USB drive selected";
        public string UsbInfo
        {
            get => _usbInfo;
            set => SetProperty(ref _usbInfo, value);
        }

        private string _warningMessage = string.Empty;
        public string WarningMessage
        {
            get => _warningMessage;
            set => SetProperty(ref _warningMessage, value);
        }

        private bool _showWarning;
        public bool ShowWarning
        {
            get => _showWarning;
            set => SetProperty(ref _showWarning, value);
        }

        private bool _isProcessing;
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (SetProperty(ref _isProcessing, value))
                {
                    OnPropertyChanged(nameof(CanExecuteInject));
                }
            }
        }

        private int _overallProgress;
        public int OverallProgress
        {
            get => _overallProgress;
            set => SetProperty(ref _overallProgress, value);
        }

        private string _overallProgressText = string.Empty;
        public string OverallProgressText
        {
            get => _overallProgressText;
            set => SetProperty(ref _overallProgressText, value);
        }

        private int _currentOperationProgress;
        public int CurrentOperationProgress
        {
            get => _currentOperationProgress;
            set => SetProperty(ref _currentOperationProgress, value);
        }

        private string _currentOperationText = string.Empty;
        public string CurrentOperationText
        {
            get => _currentOperationText;
            set => SetProperty(ref _currentOperationText, value);
        }

        private List<int>? _selectedEditions;
        public List<int>? SelectedEditions
        {
            get => _selectedEditions;
            set => SetProperty(ref _selectedEditions, value);
        }

        public ObservableCollection<string> LogEntries { get; }
        public ObservableCollection<UsbDriveInfo> UsbDrives { get; }

        #endregion

        #region Commands

        public ICommand BrowseIsoCommand { get; }
        public ICommand VerifyIsoCommand { get; }
        public ICommand BrowseMsiCommand { get; }
        public ICommand SelectEditionsCommand { get; }
        public ICommand InjectDriversCommand { get; }
        public ICommand CreateUsbCommand { get; }
        public ICommand FromSavedImageCommand { get; }
        public ICommand RefreshUsbCommand { get; }
        public ICommand RepairCleanupCommand { get; }

        #endregion

        #region Command Implementations

        private void BrowseIso()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "ISO Files (*.iso)|*.iso|All Files (*.*)|*.*",
                Title = "Select Windows ISO File"
            };

            if (dialog.ShowDialog() == true)
            {
                SelectedIsoPath = dialog.FileName;
                AddLog($"Selected ISO: {Path.GetFileName(dialog.FileName)}");
            }
        }

        private async void VerifyIso()
        {
            if (string.IsNullOrEmpty(SelectedIsoPath))
                return;

            AddLog("Verifying ISO...");
            IsProcessing = true;

            try
            {
                var result = await _isoService.ValidateIsoAsync(
                    SelectedIsoPath,
                    new Progress<string>(AddLog));

                if (result.IsValid)
                {
                    AddLog("✓ ISO validation successful");
                    AddLog($"  - boot.wim: {(result.HasBootWim ? "Found" : "Missing")}");
                    AddLog($"  - install.wim: {(result.HasInstallWim ? "Found" : "Missing")}");
                    ShowWarning = false;
                }
                else
                {
                    AddLog($"✗ ISO validation failed: {result.Message}");
                    WarningMessage = result.Message;
                    ShowWarning = true;
                }
            }
            catch (Exception ex)
            {
                AddLog($"Error verifying ISO: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private void BrowseMsi()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "MSI Files (*.msi)|*.msi|All Files (*.*)|*.*",
                Title = "Select Driver MSI File"
            };

            if (dialog.ShowDialog() == true)
            {
                SelectedMsiPath = dialog.FileName;
                AddLog($"Selected MSI: {Path.GetFileName(dialog.FileName)}");
            }
        }

        private async void SelectEditions()
        {
            if (string.IsNullOrEmpty(SelectedIsoPath))
                return;

            AddLog("Loading Windows editions...");

            try
            {
                // Mount ISO temporarily to read install.wim
                var driveLetter = await _isoService.MountIsoAsync(SelectedIsoPath, new Progress<string>(AddLog));
                if (string.IsNullOrEmpty(driveLetter))
                {
                    AddLog("Failed to mount ISO");
                    return;
                }

                var installWimPath = $"{driveLetter}:\\Sources\\install.wim";
                var editions = await _dismService.GetWimInfoAsync(installWimPath);

                await _isoService.DismountIsoAsync(SelectedIsoPath);

                if (editions.Any())
                {
                    AddLog($"Found {editions.Count} Windows editions");

                    // Show edition selector dialog
                    var editionSelector = new Dialogs.EditionSelectorWindow(editions);
                    if (editionSelector.ShowDialog() == true)
                    {
                        SelectedEditions = editionSelector.SelectedEditionIndices;
                        AddLog($"Selected {SelectedEditions.Count} edition(s) for processing");
                    }
                }
                else
                {
                    AddLog("No editions found in install.wim");
                }
            }
            catch (Exception ex)
            {
                AddLog($"Error loading editions: {ex.Message}");
            }
        }

        private bool CanExecuteInject()
        {
            return !IsProcessing &&
                   !string.IsNullOrEmpty(SelectedIsoPath) &&
                   !string.IsNullOrEmpty(SelectedMsiPath) &&
                   File.Exists(SelectedIsoPath) &&
                   File.Exists(SelectedMsiPath);
        }

        private async Task InjectDriversAsync()
        {
            if (!CanExecuteInject())
                return;

            // Show time warning dialog
            var warningResult = MessageBox.Show(
                "This operation will prepare the Windows image with drivers and may take a significant amount of time (20-60 minutes or more).\n\n" +
                "The process will:\n" +
                "• Extract the Windows ISO\n" +
                "• Install driver MSI\n" +
                "• Inject drivers into all editions\n" +
                "• Process Windows Recovery Environment\n" +
                "• Split large image files if needed\n\n" +
                "Do you want to continue?",
                "Long Operation Warning",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information);

            if (warningResult != MessageBoxResult.OK)
            {
                AddLog("Image preparation cancelled by user.");
                return;
            }

            // CRITICAL: Check for administrator privileges
            if (!AdminHelper.IsRunningAsAdministrator())
            {
                AddLog("✗ ERROR: Administrator privileges are required for driver injection!");
                var result = MessageBox.Show(
                    "This operation requires administrator privileges.\n\n" +
                    "The application will now restart with elevated permissions.\n\n" +
                    "Click OK to restart as administrator, or Cancel to abort.",
                    "Administrator Rights Required",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.OK)
                {
                    try
                    {
                        AdminHelper.RestartAsAdministrator();
                    }
                    catch (Exception ex)
                    {
                        AddLog($"✗ Failed to restart as administrator: {ex.Message}");
                        MessageBox.Show(
                            $"Failed to restart as administrator:\n\n{ex.Message}\n\n" +
                            "Please manually run the application as administrator.",
                            "Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            IsProcessing = true;
            OverallProgress = 0;
            CurrentOperationProgress = 0;
            OverallProgressText = "Starting driver injection process...";
            CurrentOperationText = "Initializing...";
            AddLog("=== Starting Driver Injection Process ===");

            try
            {
                var progress = new Progress<OperationProgress>(p =>
                {
                    CurrentOperationProgress = p.PercentComplete;
                    CurrentOperationText = p.CurrentOperation;
                    AddLog($"[{p.PercentComplete}%] {p.CurrentOperation}");
                });

                // Check disk space on temp directory location (where ISO extraction happens)
                OverallProgress = 5;
                OverallProgressText = "Checking disk space...";
                var diskSpace = FileSystemHelper.CheckDiskSpace(_config.TempBaseDirectory, _config.RequiredFreeSpaceGB);
                if (!diskSpace.HasEnoughSpace)
                {
                    AddLog($"✗ Insufficient disk space. Required: {diskSpace.RequiredGB}GB, Available: {diskSpace.FreeSpaceGB}GB");
                    MessageBox.Show(
                        $"Insufficient disk space on {diskSpace.DriveLetter}\n\n" +
                        $"Required: {diskSpace.RequiredGB} GB\n" +
                        $"Available: {diskSpace.FreeSpaceGB} GB",
                        "Insufficient Disk Space",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Get ISO volume label before extraction
                OverallProgress = 8;
                OverallProgressText = "Reading ISO volume label...";
                CurrentOperationText = "Reading ISO volume label...";
                CurrentOperationProgress = 50;
                IsoVolumeLabel = await _isoService.GetIsoVolumeLabelAsync(
                    SelectedIsoPath,
                    new Progress<string>(msg => 
                    { 
                        AddLog(msg); 
                        CurrentOperationText = msg;
                    }),
                    _cancellationTokenSource.Token);
                AddLog($"ISO volume label: {IsoVolumeLabel}");

                // Extract ISO
                OverallProgress = 10;
                OverallProgressText = "Step 1/6: Extracting ISO contents...";
                CurrentOperationText = "Extracting ISO...";
                CurrentOperationProgress = 0;
                AddLog("Extracting ISO contents...");
                var extracted = await _isoService.ExtractIsoAsync(
                    SelectedIsoPath,
                    _config.Windows11Directory,
                    new Progress<string>(msg => 
                    { 
                        AddLog(msg); 
                        CurrentOperationText = msg;
                        CurrentOperationProgress = 50;
                    }),
                    _cancellationTokenSource.Token);

                if (!extracted)
                {
                    AddLog("✗ Failed to extract ISO");
                    return;
                }

                // Extract drivers
                OverallProgress = 25;
                OverallProgressText = "Step 2/6: Extracting drivers from MSI...";
                CurrentOperationText = "Extracting drivers from MSI...";
                CurrentOperationProgress = 0;
                AddLog("Extracting drivers from MSI...");
                var driversExtracted = await _driverService.ExtractDriverMsiAsync(
                    SelectedMsiPath,
                    _config.DriversDirectory,
                    new Progress<string>(msg => 
                    { 
                        AddLog(msg); 
                        CurrentOperationText = msg;
                        CurrentOperationProgress = 50;
                    }),
                    _cancellationTokenSource.Token);

                if (!driversExtracted)
                {
                    AddLog("✗ Failed to extract drivers");
                    return;
                }

                // Validate drivers
                OverallProgress = 30;
                OverallProgressText = "Step 3/6: Validating drivers...";
                var driverValidation = _driverService.ValidateDrivers(_config.DriversDirectory);
                if (!driverValidation.IsValid)
                {
                    AddLog($"✗ Driver validation failed: {driverValidation.Message}");
                    return;
                }

                AddLog($"✓ Found {driverValidation.DriverCount} driver(s)");

                // Inject drivers into WinPE and Setup
                OverallProgress = 40;
                OverallProgressText = "Step 4/6: Injecting drivers into boot images...";
                await InjectDriversToBootWimAsync(_cancellationTokenSource.Token);

                // Inject drivers into install.wim editions
                OverallProgress = 60;
                OverallProgressText = "Step 5/6: Injecting drivers into Windows editions...";
                await InjectDriversToInstallWimAsync(_cancellationTokenSource.Token);

                // Split WIM if needed
                OverallProgress = 90;
                OverallProgressText = "Step 6/6: Finalizing...";
                await SplitWimIfNeededAsync(_cancellationTokenSource.Token);

                OverallProgress = 100;
                OverallProgressText = "Driver injection completed successfully!";
                CurrentOperationProgress = 100;
                CurrentOperationText = "Complete";
                AddLog("=== Driver Injection Complete ===");
                AddLog("Ready to create bootable USB");

                // Prompt user to create USB now
                var createUsbNow = MessageBox.Show(
                    "Driver injection completed successfully!\n\n" +
                    "Would you like to create a bootable USB drive now?",
                    "Create Bootable USB?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (createUsbNow == MessageBoxResult.Yes)
                {
                    // User wants to create USB, trigger the CreateUsbAsync workflow
                    await CreateUsbAsync();
                }
            }
            catch (OperationCanceledException)
            {
                OverallProgressText = "Operation cancelled";
                CurrentOperationText = "Cancelled by user";
                AddLog("✗ Operation cancelled by user");
            }
            catch (Exception ex)
            {
                OverallProgressText = "Error occurred";
                CurrentOperationText = ex.Message;
                AddLog($"✗ Error: {ex.Message}");
                MessageBox.Show($"An error occurred:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsProcessing = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private async Task InjectDriversToBootWimAsync(CancellationToken cancellationToken)
        {
            var bootWimPath = Path.Combine(_config.Windows11Directory, "Sources", "boot.wim");

            // WinPE (Index 1)
            AddLog("Injecting drivers into WinPE...");
            CurrentOperationText = "Mounting WinPE...";
            CurrentOperationProgress = 0;
            var mountPE = _config.MountPEDirectory;
            FileSystemHelper.EnsureDirectoryExists(mountPE);

            await _dismService.MountWimAsync(bootWimPath, 1, mountPE, new Progress<string>(msg => 
            { 
                AddLog(msg); 
                CurrentOperationText = msg;
                CurrentOperationProgress = 25;
            }), cancellationToken);

            CurrentOperationText = "Adding drivers to WinPE...";
            CurrentOperationProgress = 50;
            await _dismService.AddDriversAsync(mountPE, _config.DriversDirectory, new Progress<string>(msg => 
            { 
                AddLog(msg); 
                CurrentOperationText = msg;
                CurrentOperationProgress = 75;
            }), cancellationToken);

            CurrentOperationText = "Unmounting WinPE...";
            await _dismService.UnmountWimAsync(mountPE, true, new Progress<string>(msg => 
            { 
                AddLog(msg); 
                CurrentOperationText = msg;
            }), cancellationToken);

            // Setup (Index 2)
            AddLog("Injecting drivers into Windows Setup...");
            CurrentOperationText = "Mounting Windows Setup...";
            CurrentOperationProgress = 0;
            var mountSetup = _config.MountSetupDirectory;
            FileSystemHelper.EnsureDirectoryExists(mountSetup);

            await _dismService.MountWimAsync(bootWimPath, 2, mountSetup, new Progress<string>(msg => 
            { 
                AddLog(msg); 
                CurrentOperationText = msg;
                CurrentOperationProgress = 25;
            }), cancellationToken);

            CurrentOperationText = "Adding drivers to Windows Setup...";
            CurrentOperationProgress = 50;
            await _dismService.AddDriversAsync(mountSetup, _config.DriversDirectory, new Progress<string>(msg => 
            { 
                AddLog(msg); 
                CurrentOperationText = msg;
                CurrentOperationProgress = 75;
            }), cancellationToken);

            CurrentOperationText = "Unmounting Windows Setup...";
            await _dismService.UnmountWimAsync(mountSetup, true, new Progress<string>(msg => 
            { 
                AddLog(msg); 
                CurrentOperationText = msg;
            }), cancellationToken);

            CurrentOperationProgress = 100;
        }

        private async Task InjectDriversToInstallWimAsync(CancellationToken cancellationToken)
        {
            var installWimPath = Path.Combine(_config.Windows11Directory, "Sources", "install.wim");
            var editions = await _dismService.GetWimInfoAsync(installWimPath, cancellationToken);

            var editionsToProcess = SelectedEditions ?? editions.Select(e => e.ImageIndex).ToList();

            int editionCount = 0;
            int totalEditions = editionsToProcess.Count;

            foreach (var editionIndex in editionsToProcess)
            {
                editionCount++;
                var edition = editions.FirstOrDefault(e => e.ImageIndex == editionIndex);
                var editionName = edition?.ImageName ?? $"Edition {editionIndex}";

                AddLog($"Injecting drivers into {editionName}...");
                CurrentOperationText = $"Mounting {editionName} ({editionCount}/{totalEditions})...";
                CurrentOperationProgress = 0;

                var mountPath = Path.Combine(_config.MountDirectory, $"Edition_{editionIndex}");
                FileSystemHelper.EnsureDirectoryExists(mountPath);

                await _dismService.MountWimAsync(installWimPath, editionIndex, mountPath, new Progress<string>(msg => 
                { 
                    AddLog(msg); 
                    CurrentOperationText = msg;
                    CurrentOperationProgress = 20;
                }), cancellationToken);

                CurrentOperationText = $"Adding drivers to {editionName} ({editionCount}/{totalEditions})...";
                CurrentOperationProgress = 40;
                await _dismService.AddDriversAsync(mountPath, _config.DriversDirectory, new Progress<string>(msg => 
                { 
                    AddLog(msg); 
                    CurrentOperationText = msg;
                }), cancellationToken);

                // Check for WinRE
                var winrePath = Path.Combine(mountPath, "Windows", "System32", "Recovery", "Winre.wim");
                if (File.Exists(winrePath))
                {
                    AddLog($"  Processing WinRE for {editionName}...");
                    CurrentOperationText = $"Mounting WinRE for {editionName}...";
                    CurrentOperationProgress = 60;
                    var mountWinRE = Path.Combine(_config.MountDirectory, $"WinRE_{editionIndex}");
                    FileSystemHelper.EnsureDirectoryExists(mountWinRE);

                    await _dismService.MountWimAsync(winrePath, 1, mountWinRE, new Progress<string>(msg => 
                    { 
                        AddLog(msg); 
                        CurrentOperationText = msg;
                    }), cancellationToken);

                    CurrentOperationText = $"Adding drivers to WinRE for {editionName}...";
                    CurrentOperationProgress = 70;
                    await _dismService.AddDriversAsync(mountWinRE, _config.DriversDirectory, new Progress<string>(msg => 
                    { 
                        AddLog(msg); 
                        CurrentOperationText = msg;
                    }), cancellationToken);

                    CurrentOperationText = $"Unmounting WinRE for {editionName}...";
                    CurrentOperationProgress = 80;
                    await _dismService.UnmountWimAsync(mountWinRE, true, new Progress<string>(msg => 
                    { 
                        AddLog(msg); 
                        CurrentOperationText = msg;
                    }), cancellationToken);
                }

                CurrentOperationText = $"Unmounting {editionName}...";
                CurrentOperationProgress = 90;
                await _dismService.UnmountWimAsync(mountPath, true, new Progress<string>(msg => 
                { 
                    AddLog(msg); 
                    CurrentOperationText = msg;
                }), cancellationToken);

                CurrentOperationProgress = 100;
            }
        }

        private async Task SplitWimIfNeededAsync(CancellationToken cancellationToken)
        {
            var installWimPath = Path.Combine(_config.Windows11Directory, "Sources", "install.wim");

            if (File.Exists(installWimPath))
            {
                var fileInfo = new FileInfo(installWimPath);
                if (fileInfo.Length > 4L * 1024 * 1024 * 1024) // > 4GB
                {
                    AddLog($"install.wim is {FileSystemHelper.FormatBytes(fileInfo.Length)} - splitting for FAT32...");

                    var swmPath = Path.Combine(_config.Windows11Directory, "Sources", "install.swm");
                    await _dismService.SplitWimAsync(installWimPath, swmPath, 3800, new Progress<string>(AddLog), cancellationToken);

                    if (File.Exists(swmPath))
                    {
                        try
                        {
                            // Clear read-only attribute before deleting
                            if (File.Exists(installWimPath))
                            {
                                var wimFileInfo = new FileInfo(installWimPath);
                                if (wimFileInfo.IsReadOnly)
                                {
                                    wimFileInfo.IsReadOnly = false;
                                    AddLog("Cleared read-only flag on install.wim before deletion");
                                }
                                File.Delete(installWimPath);
                                AddLog("✓ Deleted original install.wim after split");
                            }
                            AddLog("✓ WIM split successfully");
                        }
                        catch (Exception ex)
                        {
                            AddLog($"⚠ Warning: Could not delete install.wim: {ex.Message}");
                            AddLog("The split files (.swm) will be used instead");
                        }
                    }
                }
            }
        }

        private void RefreshUsbDrives()
        {
            UsbDrives.Clear();
            var drives = _usbService.GetUsbDrives();

            foreach (var drive in drives)
            {
                UsbDrives.Add(drive);
            }

            if (UsbDrives.Any())
            {
                SelectedUsbDrive = UsbDrives.First();
                AddLog($"Found {UsbDrives.Count} USB drive(s)");
            }
            else
            {
                AddLog("No USB drives detected");
                UsbInfo = "No USB drives detected. Please insert a USB drive.";
            }
        }

        private void RepairCleanup()
        {
            AddLog("Running repair and cleanup...");
            CleanupHelper.CleanupMountedImages();
            CleanupHelper.CleanupTemporaryFiles(_config.TempBaseDirectory);
            AddLog("✓ Cleanup complete");
            MessageBox.Show("Cleanup completed successfully.", "Cleanup", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private bool CanCreateUsb()
        {
            return !IsProcessing &&
                   SelectedUsbDrive != null &&
                   Directory.Exists(_config.Windows11Directory) &&
                   Directory.EnumerateFileSystemEntries(_config.Windows11Directory).Any();
        }

        private async Task CreateUsbAsync()
        {
            if (SelectedUsbDrive == null)
            {
                MessageBox.Show("Please select a USB drive.", "No USB Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // CRITICAL: Check for administrator privileges
            if (!AdminHelper.IsRunningAsAdministrator())
            {
                AddLog("✗ ERROR: Administrator privileges are required for USB creation!");
                var adminResult = MessageBox.Show(
                    "This operation requires administrator privileges.\n\n" +
                    "The application will now restart with elevated permissions.\n\n" +
                    "Click OK to restart as administrator, or Cancel to abort.",
                    "Administrator Rights Required",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning);

                if (adminResult == MessageBoxResult.OK)
                {
                    try
                    {
                        AdminHelper.RestartAsAdministrator();
                    }
                    catch (Exception ex)
                    {
                        AddLog($"✗ Failed to restart as administrator: {ex.Message}");
                        MessageBox.Show(
                            $"Failed to restart as administrator:\n\n{ex.Message}\n\n" +
                            "Please manually run the application as administrator.",
                            "Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                return;
            }

            if (SelectedUsbDrive.SizeGB < 14)
            {
                MessageBox.Show(
                    "Selected USB drive is less than 14GB. Please use a larger drive.",
                    "USB Too Small",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"WARNING: This will ERASE all data on USB drive {SelectedUsbDrive.DiskNumber}!\n\n" +
                $"{SelectedUsbDrive.FriendlyName} - {SelectedUsbDrive.SizeGB} GB\n\n" +
                "Do you want to continue?",
                "Confirm USB Creation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            IsProcessing = true;
            OverallProgress = 0;
            CurrentOperationProgress = 0;
            OverallProgressText = "Creating bootable USB...";
            CurrentOperationText = "Preparing...";
            AddLog("=== Creating Bootable USB ===");

            try
            {
                var progress = new Progress<OperationProgress>(p =>
                {
                    CurrentOperationProgress = p.PercentComplete;
                    CurrentOperationText = p.CurrentOperation;
                    OverallProgress = p.PercentComplete;
                    OverallProgressText = $"USB Creation: {p.CurrentOperation}";
                    AddLog($"[{p.PercentComplete}%] {p.CurrentOperation}");
                });

                var success = await _usbService.CreateBootableUsbAsync(
                    SelectedUsbDrive.DiskNumber,
                    _config.Windows11Directory,
                    IsoVolumeLabel,
                    progress);

                if (success)
                {
                    OverallProgress = 100;
                    CurrentOperationProgress = 100;
                    OverallProgressText = "USB creation completed successfully!";
                    CurrentOperationText = "Complete";
                    AddLog("=== USB Creation Complete ===");
                    MessageBox.Show(
                        "Bootable Windows 11 USB created successfully!",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    OverallProgressText = "USB creation failed";
                    CurrentOperationText = "Failed";
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
                OverallProgressText = "Error occurred";
                CurrentOperationText = ex.Message;
                AddLog($"✗ Error creating USB: {ex.Message}");
                MessageBox.Show($"Error creating USB:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private void OpenSavedImageDialog()
        {
            var savedImageWindow = new Dialogs.SavedImageWindow(_config.SavedImagesDirectory);
            savedImageWindow.ShowDialog();
        }

        private void UpdateUsbInfo()
        {
            if (SelectedUsbDrive == null)
            {
                UsbInfo = "No USB drive selected";
                return;
            }

            var drive = SelectedUsbDrive;
            UsbInfo = $"Drive: {drive.FriendlyName}\n" +
                     $"Size: {drive.SizeGB} GB\n" +
                     $"File System: {drive.FileSystem}\n" +
                     $"Label: {(string.IsNullOrEmpty(drive.Label) ? "(none)" : drive.Label)}";
        }

        #endregion

        #region Helpers

        private void AddLog(string message)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                var logEntry = $"[{timestamp}] {message}";
                LogEntries.Add(logEntry);

                // Limit log entries
                while (LogEntries.Count > _config.MaxLogEntries)
                {
                    LogEntries.RemoveAt(0);
                }
            });

            // Also log to file
            Logger.Info(message);
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // Cancel any ongoing operations
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;

                // Cleanup any mounted images (non-blocking, with timeout)
                Task.Run(() =>
                {
                    try
                    {
                        var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(2));
                        CleanupHelper.CleanupMountedImages();
                    }
                    catch
                    {
                        // Ignore cleanup errors during shutdown
                    }
                });
            }

            _disposed = true;
        }

        #endregion
    }

    // Simple RelayCommand implementation
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object? parameter) => _execute();
    }
}
