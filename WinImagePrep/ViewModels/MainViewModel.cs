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
            BrowseDriverSourceCommand = new RelayCommand(BrowseDriverSource);
            SelectEditionsCommand = new RelayCommand(SelectEditions, () => !string.IsNullOrEmpty(SelectedIsoPath));
            InjectDriversCommand = new RelayCommand(async () => await InjectDriversAsync(), CanExecuteInject);
            CreateUsbFromIsoCommand = new RelayCommand(async () => await CreateUsbFromIsoAsync(), CanCreateUsbFromIso);
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

        private DriverSourceType _driverSourceType = DriverSourceType.Msi;
        public DriverSourceType DriverSourceType
        {
            get => _driverSourceType;
            set => SetProperty(ref _driverSourceType, value);
        }

        public bool IsDriverSourceMsi
        {
            get => DriverSourceType == DriverSourceType.Msi;
            set { if (value) DriverSourceType = DriverSourceType.Msi; }
        }

        public bool IsDriverSourceFolder
        {
            get => DriverSourceType == DriverSourceType.Folder;
            set { if (value) DriverSourceType = DriverSourceType.Folder; }
        }

        public bool IsDriverSourceZip
        {
            get => DriverSourceType == DriverSourceType.Zip;
            set { if (value) DriverSourceType = DriverSourceType.Zip; }
        }

        private string _selectedDriverSourcePath = string.Empty;
        public string SelectedDriverSourcePath
        {
            get => _selectedDriverSourcePath;
            set
            {
                if (SetProperty(ref _selectedDriverSourcePath, value))
                {
                    // Also update SelectedMsiPath for backward compatibility
                    SelectedMsiPath = value;
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
        public ICommand BrowseDriverSourceCommand { get; }
        public ICommand SelectEditionsCommand { get; }
        public ICommand InjectDriversCommand { get; }
        public ICommand CreateUsbFromIsoCommand { get; }
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

        private void BrowseDriverSource()
        {
            switch (DriverSourceType)
            {
                case DriverSourceType.Msi:
                    var msiDialog = new OpenFileDialog
                    {
                        Filter = "MSI Files (*.msi)|*.msi|All Files (*.*)|*.*",
                        Title = "Select Driver MSI File"
                    };
                    if (msiDialog.ShowDialog() == true)
                    {
                        SelectedDriverSourcePath = msiDialog.FileName;
                        AddLog($"Selected MSI: {Path.GetFileName(msiDialog.FileName)}");
                    }
                    break;

                case DriverSourceType.Folder:
                    var folderDialog = new System.Windows.Forms.FolderBrowserDialog
                    {
                        Description = "Select Driver Folder",
                        ShowNewFolderButton = false
                    };
                    if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        SelectedDriverSourcePath = folderDialog.SelectedPath;
                        AddLog($"Selected folder: {folderDialog.SelectedPath}");
                    }
                    break;

                case DriverSourceType.Zip:
                    var zipDialog = new OpenFileDialog
                    {
                        Filter = "ZIP Files (*.zip)|*.zip|All Files (*.*)|*.*",
                        Title = "Select Driver ZIP File"
                    };
                    if (zipDialog.ShowDialog() == true)
                    {
                        SelectedDriverSourcePath = zipDialog.FileName;
                        AddLog($"Selected ZIP: {Path.GetFileName(zipDialog.FileName)}");
                    }
                    break;
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

        private bool CanCreateUsbFromIso()
        {
            return !IsProcessing &&
                   !string.IsNullOrEmpty(SelectedIsoPath) &&
                   File.Exists(SelectedIsoPath) &&
                   SelectedUsbDrive != null;
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

                // Re-enable commands by setting IsProcessing = false
                IsProcessing = false;

                // Prompt user to create USB now or save for later
                var result = MessageBox.Show(
                    "✓ Driver injection completed successfully!\n\n" +
                    "What would you like to do next?\n\n" +
                    "• Click YES to create bootable USB now\n" +
                    "• Click NO to save project for later use\n" +
                    "• Click CANCEL to return to main screen",
                    "Driver Injection Complete",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Check if USB drive is inserted
                    RefreshUsbDrives();

                    if (SelectedUsbDrive == null || UsbDrives.Count == 0)
                    {
                        var insertUsbResult = MessageBox.Show(
                            "No USB drive detected.\n\n" +
                            "Please insert a USB drive (14GB or larger) now.\n\n" +
                            "Click OK after inserting the USB drive, or Cancel to skip.",
                            "Insert USB Drive",
                            MessageBoxButton.OKCancel,
                            MessageBoxImage.Information);

                        if (insertUsbResult == MessageBoxResult.OK)
                        {
                            // Refresh and check again
                            RefreshUsbDrives();

                            if (SelectedUsbDrive == null || UsbDrives.Count == 0)
                            {
                                MessageBox.Show(
                                    "No USB drive detected.\n\n" +
                                    "You can create the bootable USB later by clicking 'Create USB' button.",
                                    "No USB Drive",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                                return;
                            }
                        }
                        else
                        {
                            return; // User cancelled
                        }
                    }

                    // USB is ready, proceed with creation
                    // Note: CreateUsbAsync will set IsProcessing = true internally
                    await CreateUsbAsync();
                }
                else if (result == MessageBoxResult.No)
                {
                    // User wants to save project for later
                    await SaveProjectAsync();
                }
                // If Cancel, just return to main screen
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
            }), cancellationToken, deleteMountDirectory: true);

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
            }), cancellationToken, deleteMountDirectory: true);

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
                    }), cancellationToken, deleteMountDirectory: true);
                }

                CurrentOperationText = $"Unmounting {editionName}...";
                CurrentOperationProgress = 90;
                await _dismService.UnmountWimAsync(mountPath, true, new Progress<string>(msg => 
                { 
                    AddLog(msg); 
                    CurrentOperationText = msg;
                }), cancellationToken, deleteMountDirectory: true);

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

        private async Task CreateUsbFromIsoAsync()
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
                    "Administrator Required",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning);

                if (adminResult == MessageBoxResult.OK)
                {
                    AdminHelper.RestartAsAdministrator();
                    Application.Current.Shutdown();
                }
                return;
            }

            // Show warning dialog
            var result = MessageBox.Show(
                $"⚠ WARNING: All data on the selected USB drive will be permanently deleted!\n\n" +
                $"Drive: Disk {SelectedUsbDrive.DiskNumber} ({SelectedUsbDrive.FriendlyName})\n" +
                $"Capacity: {SelectedUsbDrive.SizeGB} GB\n\n" +
                $"This operation will:\n" +
                $"• Extract the Windows ISO\n" +
                $"• Split large image files if needed\n" +
                $"• Format the USB drive\n" +
                $"• Create bootable Windows installation media\n\n" +
                $"Do you want to continue?",
                "Create Bootable USB - Confirmation Required",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                AddLog("USB creation cancelled by user.");
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            IsProcessing = true;
            OverallProgress = 0;
            OverallProgressText = "Creating bootable USB from ISO...";
            CurrentOperationProgress = 0;
            CurrentOperationText = "";

            try
            {
                AddLog("=== Creating Bootable USB from ISO (No Driver Injection) ===");

                // Step 1: Get ISO label
                OverallProgress = 5;
                OverallProgressText = "Reading ISO label...";
                IsoVolumeLabel = await _isoService.GetIsoVolumeLabelAsync(
                    SelectedIsoPath,
                    new Progress<string>(AddLog),
                    _cancellationTokenSource.Token);

                // Step 2: Extract ISO
                OverallProgress = 10;
                OverallProgressText = "Extracting ISO...";
                CurrentOperationText = "Mounting and extracting ISO files...";
                await _isoService.ExtractIsoAsync(
                    SelectedIsoPath,
                    _config.Windows11Directory,
                    new Progress<string>(msg =>
                    {
                        AddLog(msg);
                        CurrentOperationText = msg;
                    }),
                    _cancellationTokenSource.Token);

                // Step 3: Split WIM if needed
                OverallProgress = 60;
                OverallProgressText = "Checking WIM size...";
                await SplitWimIfNeededAsync(_cancellationTokenSource.Token);

                // Step 4: Create USB
                OverallProgress = 70;
                OverallProgressText = "Creating bootable USB...";
                CurrentOperationText = "Formatting USB drive...";

                var usbProgress = new Progress<OperationProgress>(p =>
                {
                    CurrentOperationProgress = p.PercentComplete;
                    CurrentOperationText = p.CurrentOperation;
                    OverallProgress = Math.Min(70 + (int)(p.PercentComplete * 0.3), 100);
                    AddLog($"[{p.PercentComplete}%] {p.CurrentOperation}");
                });

                var success = await _usbService.CreateBootableUsbAsync(
                    SelectedUsbDrive.DiskNumber,
                    _config.Windows11Directory,
                    IsoVolumeLabel,
                    usbProgress);

                if (success)
                {
                    OverallProgress = 100;
                    OverallProgressText = "Complete!";
                    CurrentOperationText = "Bootable USB created successfully";
                    AddLog("=== USB Creation Complete ===");

                    MessageBox.Show(
                        "Bootable USB created successfully!\n\n" +
                        $"USB Drive: Disk {SelectedUsbDrive.DiskNumber} ({IsoVolumeLabel})\n\n" +
                        "You can now use this USB to install Windows.",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    throw new Exception("USB creation failed");
                }
            }
            catch (OperationCanceledException)
            {
                AddLog("Operation cancelled by user");
                MessageBox.Show("USB creation was cancelled.", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AddLog($"Error during USB creation: {ex.Message}");
                MessageBox.Show($"Error creating USB: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsProcessing = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
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

            // Calculate the size of the Windows 11 image directory
            double imageSizeGB = GetDirectorySizeGB(_config.Windows11Directory);
            string sizeWarning = imageSizeGB > 0 
                ? $"\n\nImage size: {imageSizeGB} GB\n(This will take several minutes to copy)" 
                : "";

            var result = MessageBox.Show(
                $"WARNING: This will ERASE all data on USB drive {SelectedUsbDrive.DiskNumber}!\n\n" +
                $"{SelectedUsbDrive.FriendlyName} - {SelectedUsbDrive.SizeGB} GB" +
                sizeWarning + "\n\n" +
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

                    // Offer three options: Save project, Delete temp files, or Keep temp files
                    var cleanupChoice = MessageBox.Show(
                        "What would you like to do with the temporary files?\n\n" +
                        "• YES - Save as a project for later use\n" +
                        "• NO - Delete temporary files (free ~6-8 GB)\n" +
                        "• CANCEL - Keep temporary files for now",
                        "Temporary Files",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (cleanupChoice == MessageBoxResult.Yes)
                    {
                        // Save project
                        await SaveProjectAsync();
                    }
                    else if (cleanupChoice == MessageBoxResult.No)
                    {
                        // Delete temp files
                        try
                        {
                            AddLog("Cleaning up temporary files...");
                            if (Directory.Exists(_config.Windows11Directory))
                            {
                                bool deleted = FileSystemHelper.ForceDeleteDirectory(_config.Windows11Directory, true);
                                if (deleted)
                                {
                                    AddLog($"✓ Deleted temporary directory: {_config.Windows11Directory}");
                                }
                                else
                                {
                                    AddLog($"⚠ Warning: Could not delete all temporary files");
                                    MessageBox.Show(
                                        "Some temporary files could not be deleted.\n\n" +
                                        "They may be in use or locked. Try closing all programs\n" +
                                        "and use the Repair/Cleanup option, or manually delete:\n\n" +
                                        _config.Windows11Directory,
                                        "Cleanup Warning",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Warning);
                                }
                            }
                        }
                        catch (Exception cleanupEx)
                        {
                            AddLog($"⚠ Warning: Failed to delete temporary files: {cleanupEx.Message}");
                            MessageBox.Show(
                                $"Could not delete temporary files:\n\n{cleanupEx.Message}\n\n" +
                                "You may need to manually delete the folder or use Repair/Cleanup.",
                                "Cleanup Warning",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                    }
                    else
                    {
                        AddLog("Temporary files kept for future use");
                    }
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

        private async Task SaveProjectAsync()
        {
            try
            {
                // Prompt for project name
                var inputDialog = new Dialogs.InputDialog(
                    "Save Project",
                    "Enter a name for this project:",
                    $"Win11_Drivers_{DateTime.Now:yyyyMMdd}");

                if (inputDialog.ShowDialog() != true || string.IsNullOrWhiteSpace(inputDialog.InputText))
                {
                    AddLog("Project save cancelled by user");
                    return;
                }

                // Sanitize project name
                var projectName = string.Join("_", inputDialog.InputText.Split(Path.GetInvalidFileNameChars()));

                var projectPath = Path.Combine(_config.SavedImagesDirectory, projectName);

                // Check if project already exists
                if (Directory.Exists(projectPath))
                {
                    var overwrite = MessageBox.Show(
                        $"A project named '{projectName}' already exists.\n\nDo you want to overwrite it?",
                        "Project Exists",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (overwrite != MessageBoxResult.Yes)
                    {
                        AddLog("Project save cancelled - project already exists");
                        return;
                    }

                    // Delete existing project
                    Directory.Delete(projectPath, true);
                }

                IsProcessing = true;
                OverallProgress = 0;
                OverallProgressText = "Saving project...";
                CurrentOperationText = "Copying files...";
                AddLog($"=== Saving Project: {projectName} ===");

                // Create project directory
                FileSystemHelper.EnsureDirectoryExists(projectPath);

                // Copy Windows11 directory contents
                AddLog($"Copying prepared Windows files to {projectPath}...");
                CurrentOperationProgress = 20;

                var arguments = $"\"{_config.Windows11Directory}\" \"{projectPath}\" /E /R:3 /W:5";
                var result = await ProcessHelper.ExecuteProcessAsync("robocopy.exe", arguments);

                if (result.ExitCode >= 0 && result.ExitCode < 8)
                {
                    // Save ISO label
                    var labelPath = Path.Combine(projectPath, "iso-label.txt");
                    File.WriteAllText(labelPath, IsoVolumeLabel);
                    AddLog($"Saved ISO label: {IsoVolumeLabel}");

                    CurrentOperationProgress = 100;
                    OverallProgress = 100;
                    OverallProgressText = "Project saved successfully!";
                    CurrentOperationText = "Complete";
                    AddLog($"✓ Project saved to: {projectPath}");

                    MessageBox.Show(
                        $"Project '{projectName}' saved successfully!\n\n" +
                        $"Location: {projectPath}\n\n" +
                        "You can now create a USB from this saved project using\n" +
                        "the 'From Saved Image' button.",
                        "Project Saved",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    AddLog($"✗ Failed to save project (Robocopy exit code: {result.ExitCode})");
                    MessageBox.Show(
                        "Failed to save project. Check the log for details.",
                        "Save Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                AddLog($"✗ Error saving project: {ex.Message}");
                MessageBox.Show(
                    $"Error saving project:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        #endregion

        #region Helpers

        public async Task CheckForExistingWorkAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    // Check if Windows11 directory exists and has content
                    if (!Directory.Exists(_config.Windows11Directory))
                        return;

                    var files = Directory.GetFiles(_config.Windows11Directory, "*", SearchOption.AllDirectories);
                    if (files.Length == 0)
                    {
                        // Directory exists but is empty, just delete it
                        try
                        {
                            Directory.Delete(_config.Windows11Directory, false);
                        }
                        catch { /* Ignore cleanup failures */ }
                        return;
                    }

                    // Found existing work - prompt user on UI thread
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        AddLog($"⚠ Found existing temporary files in {_config.Windows11Directory}");

                        var result = MessageBox.Show(
                            "Existing Windows 11 preparation files were found.\n\n" +
                            "Would you like to resume from the existing work?\n\n" +
                            "• YES - Keep files and resume (you can create USB from existing files)\n" +
                            "• NO - Delete files and start fresh",
                            "Existing Work Found",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result == MessageBoxResult.No)
                        {
                            // User wants to start fresh - delete the directory
                            try
                            {
                                AddLog("Deleting existing temporary files...");
                                bool deleted = FileSystemHelper.ForceDeleteDirectory(_config.Windows11Directory, true);
                                if (deleted)
                                {
                                    AddLog($"✓ Deleted temporary directory: {_config.Windows11Directory}");
                                }
                                else
                                {
                                    AddLog($"⚠ Warning: Could not delete all temporary files");
                                    MessageBox.Show(
                                        "Some temporary files could not be deleted.\n\n" +
                                        "They may be in use or locked. Try using the Repair/Cleanup option.",
                                        "Cleanup Warning",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Warning);
                                }
                            }
                            catch (Exception ex)
                            {
                                AddLog($"⚠ Warning: Failed to delete temporary files: {ex.Message}");
                                MessageBox.Show(
                                    $"Could not delete temporary files:\n\n{ex.Message}\n\n" +
                                    "You may need to manually delete the folder or use the Repair/Cleanup option.",
                                    "Cleanup Warning",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                            }
                        }
                        else
                        {
                            // User wants to resume - check for saved ISO label
                            AddLog("Resuming from existing temporary files");

                            var labelFile = Path.Combine(_config.Windows11Directory, "iso-label.txt");
                            if (File.Exists(labelFile))
                            {
                                try
                                {
                                    var savedLabel = File.ReadAllText(labelFile).Trim();
                                    if (!string.IsNullOrWhiteSpace(savedLabel))
                                    {
                                        IsoVolumeLabel = savedLabel;
                                        AddLog($"Restored ISO label: {savedLabel}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    AddLog($"⚠ Could not restore ISO label: {ex.Message}");
                                }
                            }

                            MessageBox.Show(
                                "You can now create a bootable USB from the existing files.\n\n" +
                                "Select a USB drive and click 'Create Bootable USB'.",
                                "Resume Workflow",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        AddLog($"⚠ Error checking for existing work: {ex.Message}");
                    });
                }
            });
        }

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

        private double GetDirectorySizeGB(string directoryPath)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                    return 0;

                var dirInfo = new DirectoryInfo(directoryPath);
                long totalBytes = dirInfo.GetFiles("*", SearchOption.AllDirectories)
                    .Sum(file => file.Length);

                return Math.Round(totalBytes / (1024.0 * 1024.0 * 1024.0), 2);
            }
            catch (Exception ex)
            {
                AddLog($"⚠ Could not calculate directory size: {ex.Message}");
                return 0;
            }
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
