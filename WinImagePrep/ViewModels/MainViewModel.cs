using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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
        private readonly AppListService _appListService;
        private readonly UpdateService _updateService;
        private readonly SettingsService _settingsService;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _disposed;

        public MainViewModel()
        {
            // Load settings and create configuration
            _settingsService = new SettingsService();
            // Settings are already loaded by App.xaml.cs, so just use current settings
            var settings = _settingsService.CurrentSettings;
            _config = new AppConfiguration(settings);

            _isoService = new IsoService();
            _driverService = new DriverService();
            _dismService = new DismService();
            _usbService = new UsbService();
            _appListService = new AppListService();
            _updateService = new UpdateService(new System.Net.Http.HttpClient());

            // Initialize commands
            BrowseIsoCommand = new RelayCommand(BrowseIso);
            VerifyIsoCommand = new RelayCommand(VerifyIso, () => !string.IsNullOrEmpty(SelectedIsoPath));
            ClearIsoCommand = new RelayCommand(ClearIso, () => !string.IsNullOrEmpty(SelectedIsoPath));
            BrowseMsiCommand = new RelayCommand(BrowseMsi);
            BrowseDriverSourceCommand = new RelayCommand(BrowseDriverSource);
            BrowseDriverFolderCommand = new RelayCommand(BrowseDriverFolder);
            RemoveDriverSourceCommand = new RelayCommand<DriverSourceInfo>(RemoveDriverSource);
            ClearDriverSourceCommand = new RelayCommand(ClearDriverSource, () => !string.IsNullOrEmpty(SelectedDriverSourcePath));
            SelectEditionsCommand = new RelayCommand(SelectEditions, () => !string.IsNullOrEmpty(SelectedIsoPath));
            SelectAppsToRemoveCommand = new RelayCommand(SelectAppsToRemove, () => WindowsApps?.Count > 0);
            LoadAppsCommand = new RelayCommand(async () => await LoadAppsAsync(), () => !IsProcessing);
            ScanAppsFromIsoCommand = new RelayCommand(async () => await ScanAppsFromIsoAsync(), () => !string.IsNullOrEmpty(SelectedIsoPath) && !IsProcessing);
            InjectDriversCommand = new RelayCommand(async () => await InjectDriversAsync(), CanExecuteInject);
            CreateUsbFromIsoCommand = new RelayCommand(async () => await CreateUsbFromIsoAsync(), CanCreateUsbFromIso);
            CreateUsbCommand = new RelayCommand(async () => await CreateUsbAsync(), CanCreateUsb);
            CreateCustomIsoCommand = new RelayCommand(async () => await CreateCustomIsoAsync(), CanCreateCustomIso);
            FromSavedImageCommand = new RelayCommand(OpenSavedImageDialog);
            RefreshUsbCommand = new RelayCommand(RefreshUsbDrives);
            RepairCleanupCommand = new RelayCommand(RepairCleanup);
            ConfigureUnattendedCommand = new RelayCommand(ConfigureUnattendedInstall, () => EnableUnattendedInstall);

            // Menu commands
            SaveConfigCommand = new RelayCommand(SaveConfiguration);
            OpenConfigCommand = new RelayCommand(OpenConfiguration);
            ExitCommand = new RelayCommand(ExitApplication);
            OpenOptionsCommand = new RelayCommand(OpenOptions);
            CheckForUpdatesCommand = new RelayCommand(async () => await CheckForUpdatesAsync());
            CheckPrerequisitesCommand = new RelayCommand(async () => await CheckPrerequisitesAsync());
            OpenUserGuideCommand = new RelayCommand(OpenUserGuide);
            OpenOnlineDocumentationCommand = new RelayCommand(OpenOnlineDocumentation);
            OpenGitHubReadmeCommand = new RelayCommand(OpenGitHubReadme);
            OpenReportIssueCommand = new RelayCommand(OpenReportIssue);
            OpenReleaseNotesCommand = new RelayCommand(OpenReleaseNotes);
            ShowAboutCommand = new RelayCommand(ShowAbout);

            // Initialize collections
            LogEntries = new ObservableCollection<string>();
            UsbDrives = new ObservableCollection<UsbDriveInfo>();
            WindowsApps = new ObservableCollection<WindowsApp>();
            DriverSources = new ObservableCollection<DriverSourceInfo>();

            // Load initial USB drives
            RefreshUsbDrives();

            // Load saved app removal settings
            RemoveWindowsApps = settings.RemoveWindowsApps;

            // Load saved unattended install settings
            EnableUnattendedInstall = settings.EnableUnattendedInstall;

            AddLog("Win11 Image Prep - Ready");
            AddLog("NEW: Windows app removal + Edition selection features");
            AddLog("Please select a Windows ISO and driver source to begin");
        }

        /// <summary>
        /// Capture current application state for preservation across admin elevation
        /// </summary>
        private AppState CaptureCurrentState()
        {
            return new AppState
            {
                IsoPath = SelectedIsoPath,
                IsoVolumeLabel = IsoVolumeLabel,
                DriverPaths = DriverSources?.Select(d => d.Path).Where(p => !string.IsNullOrEmpty(p)).ToList(),
                RemoveWindowsApps = RemoveWindowsApps,
                SelectedAppsForRemoval = WindowsApps?
                    .Where(a => a.IsSelected)
                    .SelectMany(a => a.PackageNames)
                    .ToList(),
                EnableUnattendedInstall = EnableUnattendedInstall,
                SelectedEditions = SelectedEditions?.ToList()
            };
        }

        /// <summary>
        /// Restore application state from command-line arguments (used after elevation restart)
        /// </summary>
        public void RestoreState(AppState? state)
        {
            if (state == null)
                return;

            try
            {
                // Restore ISO path
                if (!string.IsNullOrEmpty(state.IsoPath) && File.Exists(state.IsoPath))
                {
                    SelectedIsoPath = state.IsoPath;
                    AddLog($"✓ Restored ISO path: {Path.GetFileName(state.IsoPath)}");
                }

                // Restore ISO volume label
                if (!string.IsNullOrEmpty(state.IsoVolumeLabel))
                {
                    IsoVolumeLabel = state.IsoVolumeLabel;
                    AddLog($"✓ Restored ISO volume label: {state.IsoVolumeLabel}");
                }

                // Restore driver sources
                if (state.DriverPaths != null)
                {
                    foreach (var driverPath in state.DriverPaths)
                    {
                        if (!string.IsNullOrEmpty(driverPath))
                        {
                            DriverSourceType sourceType;
                            if (File.Exists(driverPath))
                            {
                                sourceType = Path.GetExtension(driverPath).ToLowerInvariant() switch
                                {
                                    ".msi" => DriverSourceType.Msi,
                                    ".zip" => DriverSourceType.Zip,
                                    _ => DriverSourceType.Folder
                                };
                            }
                            else if (Directory.Exists(driverPath))
                            {
                                sourceType = DriverSourceType.Folder;
                            }
                            else
                            {
                                continue; // Skip invalid paths
                            }

                            var driverSource = new DriverSourceInfo
                            {
                                Path = driverPath,
                                Type = sourceType
                            };

                            DriverSources.Add(driverSource);
                            AddLog($"✓ Restored driver source: {Path.GetFileName(driverPath)}");
                        }
                    }
                }

                // Restore app removal setting
                RemoveWindowsApps = state.RemoveWindowsApps;
                if (state.RemoveWindowsApps)
                {
                    AddLog($"✓ Restored Windows app removal setting: ENABLED");
                }

                // Restore selected apps for removal (will be applied when apps are loaded)
                if (state.SelectedAppsForRemoval != null && state.SelectedAppsForRemoval.Any())
                {
                    // Store for later restoration when WindowsApps collection is populated
                    _pendingAppSelectionsRestore = state.SelectedAppsForRemoval;
                    AddLog($"✓ Will restore {state.SelectedAppsForRemoval.Count} app selections once app list loads");
                }

                // Restore unattended install setting
                EnableUnattendedInstall = state.EnableUnattendedInstall;
                if (state.EnableUnattendedInstall)
                {
                    AddLog($"✓ Restored unattended install setting: ENABLED");
                }

                // Restore selected editions
                if (state.SelectedEditions != null && state.SelectedEditions.Any())
                {
                    SelectedEditions = state.SelectedEditions;
                    AddLog($"✓ Restored {state.SelectedEditions.Count} selected edition(s)");
                }

                AddLog("✓ Application state fully restored after elevation");
            }
            catch (Exception ex)
            {
                AddLog($"⚠ Warning: Failed to restore some state: {ex.Message}");
            }
        }

        private List<string>? _pendingAppSelectionsRestore;

        /// <summary>
        /// Apply pending app selections after WindowsApps collection is loaded
        /// </summary>
        private void ApplyPendingAppSelections()
        {
            if (_pendingAppSelectionsRestore == null || !_pendingAppSelectionsRestore.Any())
                return;

            try
            {
                int restoredCount = 0;
                foreach (var app in WindowsApps)
                {
                    // Check if any of this app's packages were selected
                    if (app.PackageNames.Any(pkg => _pendingAppSelectionsRestore.Contains(pkg)))
                    {
                        app.IsSelected = true;
                        restoredCount++;
                    }
                }

                if (restoredCount > 0)
                {
                    AddLog($"✓ Restored {restoredCount} app selection(s)");
                    OnPropertyChanged(nameof(SelectedAppsCountText));
                }

                _pendingAppSelectionsRestore = null; // Clear after applying
            }
            catch (Exception ex)
            {
                AddLog($"⚠ Failed to restore app selections: {ex.Message}");
            }
        }

        #region Properties

        public string WindowTitle
        {
            get
            {
                var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                return $"Win11 Image Prep v{version?.Major}.{version?.Minor}.{version?.Build}";
            }
        }

        private string _selectedIsoPath = string.Empty;
        public string SelectedIsoPath
        {
            get => _selectedIsoPath;
            set
            {
                if (SetProperty(ref _selectedIsoPath, value))
                {
                    OnPropertyChanged(nameof(CanExecuteInject));
                    OnPropertyChanged(nameof(HasIsoPath));
                }
            }
        }

        public bool HasIsoPath => !string.IsNullOrEmpty(SelectedIsoPath);

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
            set
            {
                if (SetProperty(ref _driverSourceType, value))
                {
                    OnPropertyChanged(nameof(DriverSourceTypeLabel));
                }
            }
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
                    OnPropertyChanged(nameof(HasDriverSourcePath));
                    OnPropertyChanged(nameof(DriverSourceTypeLabel));
                }
            }
        }

        public bool HasDriverSourcePath => !string.IsNullOrEmpty(SelectedDriverSourcePath);

        public string DriverSourceTypeLabel
        {
            get
            {
                if (IsDriverSourceMsi) return "(MSI)";
                if (IsDriverSourceZip) return "(ZIP)";
                if (IsDriverSourceFolder) return "(Folder)";
                return "";
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
                // When processing completes, evaluate image-ready state
                if (!value)
                {
                    UpdateImageReadyState();
                }
                }
            }
        }

        /// <summary>Running as administrator: Windows blocks drag and drop from Explorer into the window</summary>
        public bool IsRunningElevated { get; } = AdminHelper.IsRunningAsAdministrator();

        private bool _isImageReady;
        public bool IsImageReady
        {
            get => _isImageReady;
            set => SetProperty(ref _isImageReady, value);
        }

        private void UpdateImageReadyState()
        {
            try
            {
                IsImageReady = Directory.Exists(_config.Windows11Directory) && !IsDiscardedWork(_config.Windows11Directory) &&
                               (File.Exists(Path.Combine(_config.Windows11Directory, "boot", "efisys.bin")) ||
                                File.Exists(Path.Combine(_config.Windows11Directory, "boot", "efisys_noprompt.bin")));
            }
            catch { IsImageReady = false; }
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

        private bool _removeWindowsApps;
        public bool RemoveWindowsApps
        {
            get => _removeWindowsApps;
            set
            {
                if (SetProperty(ref _removeWindowsApps, value))
                {
                    // Save to settings (clone, modify, save)
                    var settings = _settingsService.CurrentSettings.Clone();
                    settings.RemoveWindowsApps = value;
                    _ = _settingsService.SaveSettingsAsync(settings);

                    // Auto-load apps when checkbox is checked
                    if (value && WindowsApps.Count == 0)
                    {
                        _ = LoadAppsAsync();
                    }
                }
            }
        }

        private bool _enableUnattendedInstall;
        public bool EnableUnattendedInstall
        {
            get => _enableUnattendedInstall;
            set
            {
                if (SetProperty(ref _enableUnattendedInstall, value))
                {
                    // Save to settings (clone, modify, save)
                    var settings = _settingsService.CurrentSettings.Clone();
                    settings.EnableUnattendedInstall = value;
                    _ = _settingsService.SaveSettingsAsync(settings);

                    OnPropertyChanged(nameof(CanConfigureUnattended));
                }
            }
        }

        private bool _pendingUpdate;
        public bool PendingUpdate
        {
            get => _pendingUpdate;
            set => SetProperty(ref _pendingUpdate, value);
        }

        private Version? _pendingUpdateVersion;
        public Version? PendingUpdateVersion
        {
            get => _pendingUpdateVersion;
            set => SetProperty(ref _pendingUpdateVersion, value);
        }

        public bool CanConfigureUnattended => EnableUnattendedInstall;


        private string _appLoadingStatusText = string.Empty;
        public string AppLoadingStatusText
        {
            get => _appLoadingStatusText;
            set => SetProperty(ref _appLoadingStatusText, value);
        }

        public string SelectedAppsCountText
        {
            get
            {
                var count = WindowsApps?.Count(a => a.IsSelected) ?? 0;
                return count > 0 ? $"({count} app{(count == 1 ? "" : "s")} selected)" : string.Empty;
            }
        }

        public ObservableCollection<string> LogEntries { get; }
        public ObservableCollection<UsbDriveInfo> UsbDrives { get; }
        public ObservableCollection<WindowsApp> WindowsApps { get; }
        public ObservableCollection<DriverSourceInfo> DriverSources { get; }

        public bool HasDriverSources => DriverSources.Count > 0;

        #endregion

        #region Commands

        public ICommand BrowseIsoCommand { get; }
        public ICommand VerifyIsoCommand { get; }
        public ICommand ClearIsoCommand { get; }
        public ICommand BrowseMsiCommand { get; }
        public ICommand BrowseDriverSourceCommand { get; }
        public ICommand BrowseDriverFolderCommand { get; }
        public ICommand RemoveDriverSourceCommand { get; }
        public ICommand ClearDriverSourceCommand { get; }
        public ICommand SelectEditionsCommand { get; }
        public ICommand SelectAppsToRemoveCommand { get; }
        public ICommand LoadAppsCommand { get; }
        public ICommand ScanAppsFromIsoCommand { get; }
        public ICommand InjectDriversCommand { get; }
        public ICommand CreateUsbFromIsoCommand { get; }
        public ICommand CreateUsbCommand { get; }
        public ICommand CreateCustomIsoCommand { get; }
        public ICommand FromSavedImageCommand { get; }
        public ICommand RefreshUsbCommand { get; }
        public ICommand RepairCleanupCommand { get; }
        public ICommand ConfigureUnattendedCommand { get; }

        // Menu commands
        public ICommand SaveConfigCommand { get; }
        public ICommand OpenConfigCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand OpenOptionsCommand { get; }
        public ICommand CheckForUpdatesCommand { get; }
        public ICommand CheckPrerequisitesCommand { get; }
        public ICommand OpenUserGuideCommand { get; }
        public ICommand OpenOnlineDocumentationCommand { get; }
        public ICommand OpenGitHubReadmeCommand { get; }
        public ICommand OpenReportIssueCommand { get; }
        public ICommand OpenReleaseNotesCommand { get; }
        public ICommand ShowAboutCommand { get; }

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

        private void ClearIso()
        {
            SelectedIsoPath = string.Empty;
            AddLog("ISO file removed");
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
                    AddLog($"  - install image: {result.InstallImageName ?? "Missing"}");
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
            // Show dialog to select multiple driver packs - for now just one at a time
            var dialog = new OpenFileDialog
            {
                Filter = "Driver Packs (*.msi;*.zip)|*.msi;*.zip|MSI Files (*.msi)|*.msi|ZIP Files (*.zip)|*.zip|All Files (*.*)|*.*",
                Title = "Select Driver Pack(s)",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    DriverSourceType type = ext switch
                    {
                        ".msi" => DriverSourceType.Msi,
                        ".zip" => DriverSourceType.Zip,
                        _ => DriverSourceType.Folder
                    };

                    var driverSource = new DriverSourceInfo
                    {
                        Path = file,
                        Type = type
                    };

                    DriverSources.Add(driverSource);
                    AddLog($"📦 Added driver pack: {driverSource.DisplayName} ({driverSource.TypeLabel})");
                }

                OnPropertyChanged(nameof(HasDriverSources));
                OnPropertyChanged(nameof(CanExecuteInject));
            }
        }

        private void BrowseDriverFolder()
        {
            var folderDialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select Driver Folder",
                ShowNewFolderButton = false
            };

            if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var driverSource = new DriverSourceInfo
                {
                    Path = folderDialog.SelectedPath,
                    Type = DriverSourceType.Folder
                };

                DriverSources.Add(driverSource);
                AddLog($"📁 Added driver folder: {driverSource.DisplayName}");

                OnPropertyChanged(nameof(HasDriverSources));
                OnPropertyChanged(nameof(CanExecuteInject));
            }
        }

        private void RemoveDriverSource(DriverSourceInfo? driverSource)
        {
            if (driverSource != null && DriverSources.Contains(driverSource))
            {
                DriverSources.Remove(driverSource);
                AddLog($"🗑️ Removed driver pack: {driverSource.DisplayName}");

                OnPropertyChanged(nameof(HasDriverSources));
                OnPropertyChanged(nameof(CanExecuteInject));
            }
        }

        private void ClearDriverSource()
        {
            SelectedDriverSourcePath = string.Empty;
            IsDriverSourceMsi = false;
            IsDriverSourceFolder = false;
            IsDriverSourceZip = false;
            AddLog("Driver source removed");
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

                // install.wim, install.esd or a split install.swm
                var installWimPath = Services.IsoService.FindInstallImage($"{driveLetter}:\\Sources")
                                     ?? $"{driveLetter}:\\Sources\\install.wim";
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
                    AddLog("No editions found in the ISO's install image");
                }
            }
            catch (Exception ex)
            {
                AddLog($"Error loading editions: {ex.Message}");
            }
        }

        private void SelectAppsToRemove()
        {
            var dialog = new AppRemovalDialog(WindowsApps);
            if (dialog.ShowDialog() == true)
            {
                // Update the main collection with the selections from the dialog
                for (int i = 0; i < WindowsApps.Count && i < dialog.WindowsApps.Count; i++)
                {
                    WindowsApps[i].IsSelected = dialog.WindowsApps[i].IsSelected;
                }

                // Save selections to settings
                SaveAppSelections();

                // Update the count display
                OnPropertyChanged(nameof(SelectedAppsCountText));

                var selectedCount = WindowsApps.Count(a => a.IsSelected);
                AddLog($"App removal selection updated: {selectedCount} app(s) selected");
            }
        }

        /// <summary>
        /// Load app list from GitHub/cache (fast method)
        /// </summary>
        private async Task LoadAppsAsync()
        {
            try
            {
                IsProcessing = true;
                AppLoadingStatusText = "Loading apps...";
                AddLog("Loading Windows app list from GitHub...");

                var apps = await _appListService.LoadAppListAsync(
                    new Progress<string>(AddLog),
                    _cancellationTokenSource?.Token ?? default);

                if (apps.Any())
                {
                    WindowsApps.Clear();
                    foreach (var app in apps.OrderBy(a => a.DisplayName))
                    {
                        WindowsApps.Add(app);
                    }

                    AddLog($"✓ Loaded {WindowsApps.Count} apps from GitHub");

                    // Restore saved app selections from settings
                    RestoreSavedAppSelections();

                    // Apply any pending app selections from elevation state restoration
                    ApplyPendingAppSelections();

                    AppLoadingStatusText = $"✓ {WindowsApps.Count} apps loaded";

                    // Trigger command re-evaluation
                    OnPropertyChanged(nameof(SelectedAppsCountText));
                    CommandManager.InvalidateRequerySuggested();
                }
                else
                {
                    AddLog("⚠ No apps available - GitHub/cache unavailable");
                    AppLoadingStatusText = "⚠ Unable to load apps";
                    MessageBox.Show(
                        "Unable to load app list from GitHub or cache.\n\n" +
                        "Please check your internet connection and try again.",
                        "App List Unavailable", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                AddLog($"✗ Error loading apps: {ex.Message}");
                AppLoadingStatusText = "✗ Failed to load apps";
                MessageBox.Show($"Error loading app list:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        /// <summary>
        /// Scan apps from ISO (slow method - extracts and mounts)
        /// </summary>
        private async Task ScanAppsFromIsoAsync()
        {
            // Step 0: Verify admin rights
            if (!AdminHelper.IsRunningAsAdministrator())
            {
                AddLog("✗ ERROR: Administrator privileges required");
                MessageBox.Show(
                    "This operation requires administrator privileges.\n\n" +
                    "To scan apps from ISO:\n" +
                    "1. Close this application\n" +
                    "2. Right-click on WinImagePrep and select 'Run as administrator'\n" +
                    "3. Your settings and selections will be preserved\n\n" +
                    "Note: Running as administrator will allow all features to work, " +
                    "but may affect drag-and-drop from File Explorer. You can use the " +
                    "Browse buttons or Load Config instead.",
                    "Administrator Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrEmpty(SelectedIsoPath))
            {
                MessageBox.Show("Please select a Windows ISO first.", "No ISO Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!File.Exists(SelectedIsoPath))
            {
                MessageBox.Show("The selected ISO file does not exist.", "File Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                IsProcessing = true;
                AddLog("Loading apps from ISO...");
                OverallProgressText = "Loading apps from ISO";
                OverallProgress = 0;

                string? tempIsoExtractPath = null;
                string? tempMountPath = null;

                try
                {
                    // Step 1: Extract ISO to temp folder (DISM cannot mount WIM from read-only ISO)
                    tempIsoExtractPath = Path.Combine(_config.TempBaseDirectory, $"iso_extract_{Guid.NewGuid():N}");
                    Directory.CreateDirectory(tempIsoExtractPath);
                    AddLog($"Created temp ISO extract folder: {tempIsoExtractPath}");
                    OverallProgress = 5;

                    AddLog("Extracting ISO contents (this may take a few minutes)...");
                    var extractSuccess = await _isoService.ExtractIsoAsync(
                        SelectedIsoPath,
                        tempIsoExtractPath,
                        new Progress<string>(AddLog),
                        _cancellationTokenSource?.Token ?? default);

                    if (!extractSuccess)
                    {
                        AddLog("✗ Failed to extract ISO");
                        MessageBox.Show("Failed to extract ISO contents.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    AddLog("✓ ISO extracted successfully");
                    OverallProgress = 30;

                    // Step 2: Locate install.wim in extracted folder
                    var installWimPath = Path.Combine(tempIsoExtractPath, "sources", "install.wim");
                    if (!File.Exists(installWimPath))
                    {
                        installWimPath = Path.Combine(tempIsoExtractPath, "sources", "install.esd");
                        if (!File.Exists(installWimPath))
                        {
                            AddLog("✗ install.wim/install.esd not found in extracted ISO");
                            MessageBox.Show("Could not find install.wim or install.esd in the ISO.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }

                    AddLog($"Found image: {Path.GetFileName(installWimPath)}");
                    OverallProgress = 35;

                    // Step 3: Get editions to find index 1
                    AddLog("Reading Windows editions...");
                    var editions = await _dismService.GetWimInfoAsync(installWimPath, _cancellationTokenSource?.Token ?? default);

                    if (!editions.Any())
                    {
                        AddLog("✗ No Windows editions found in image");
                        MessageBox.Show("No Windows editions found in the image file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    var firstEdition = editions.First();
                    AddLog($"Using edition: {firstEdition.ImageName} (Index {firstEdition.ImageIndex})");
                    OverallProgress = 40;

                    // Step 4: Create temp mount directory
                    tempMountPath = Path.Combine(_config.TempBaseDirectory, $"mount_{Guid.NewGuid():N}");
                    Directory.CreateDirectory(tempMountPath);
                    AddLog($"Created temp mount point: {tempMountPath}");
                    OverallProgress = 45;

                    // Step 5: Mount the WIM
                    AddLog($"Mounting Windows image (this may take a minute)...");
                    var mountSuccess = await _dismService.MountWimAsync(
                        installWimPath,
                        firstEdition.ImageIndex,
                        tempMountPath,
                        new Progress<string>(AddLog),
                        _cancellationTokenSource?.Token ?? default);

                    if (!mountSuccess)
                    {
                        AddLog("✗ Failed to mount Windows image");
                        MessageBox.Show("Failed to mount the Windows image.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    AddLog("✓ Windows image mounted");
                    OverallProgress = 70;

                    // Step 6: Get provisioned apps from mounted image
                    AddLog("Scanning for provisioned apps...");
                    var provisionedApps = await _dismService.GetProvisionedAppsDetailedAsync(
                        tempMountPath,
                        new Progress<string>(AddLog),
                        _cancellationTokenSource?.Token ?? default);

                    if (!provisionedApps.Any())
                    {
                        AddLog("⚠ No provisioned apps found in image");
                        MessageBox.Show("No provisioned apps were found in the Windows image.", "No Apps Found", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    AddLog($"✓ Found {provisionedApps.Count} provisioned app(s)");
                    OverallProgress = 90;

                    // Step 7: Populate WindowsApps collection
                    WindowsApps.Clear();

                    foreach (var app in provisionedApps.OrderBy(a => a.DisplayName))
                    {
                        // Create a friendly display name from the package display name
                        var displayName = app.DisplayName;
                        var shortName = displayName;

                        // Try to make it more readable - remove publisher prefix if present
                        if (displayName.Contains('.'))
                        {
                            var parts = displayName.Split('.');
                            if (parts.Length >= 2)
                            {
                                // Use last meaningful part (e.g., "Microsoft.WindowsCalculator" -> "WindowsCalculator")
                                shortName = parts[parts.Length - 1];
                            }
                        }

                        WindowsApps.Add(new WindowsApp
                        {
                            PackageName = app.PackageName,  // Full package name for removal
                            DisplayName = shortName,         // Friendly name for UI
                            Description = $"{app.DisplayName} (v{app.Version})",
                            IsSelected = false
                        });
                    }

                    AddLog($"✓ Loaded {WindowsApps.Count} apps into selection list");
                    OnPropertyChanged(nameof(SelectedAppsCountText));
                    OverallProgress = 100;

                    // Save scanned apps to cache for future use
                    try
                    {
                        await _appListService.SaveScannedAppsAsync(WindowsApps.ToList());
                        AddLog("✓ Saved app list to cache");
                    }
                    catch (Exception ex)
                    {
                        AddLog($"⚠ Cache save warning: {ex.Message}");
                    }

                    MessageBox.Show(
                        $"Successfully loaded {WindowsApps.Count} provisioned apps from the ISO.\n\n" +
                        "You can now select which apps to remove using the 'Select Apps to Remove...' button.",
                        "Apps Loaded",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                finally
                {
                    // Step 8: Cleanup - unmount WIM
                    if (!string.IsNullOrEmpty(tempMountPath) && Directory.Exists(tempMountPath))
                    {
                        try
                        {
                            AddLog("Unmounting Windows image...");
                            await _dismService.UnmountWimAsync(
                                tempMountPath, 
                                commit: false, 
                                new Progress<string>(AddLog), 
                                _cancellationTokenSource?.Token ?? default,
                                deleteMountDirectory: true);
                            AddLog("✓ Windows image unmounted");
                        }
                        catch (Exception ex)
                        {
                            AddLog($"⚠ Cleanup warning: {ex.Message}");
                        }
                    }

                    // Step 9: Clean up extracted ISO folder
                    if (!string.IsNullOrEmpty(tempIsoExtractPath) && Directory.Exists(tempIsoExtractPath))
                    {
                        try
                        {
                            AddLog("Cleaning up extracted ISO files...");
                            FileSystemHelper.DeleteDirectoryContents(tempIsoExtractPath);
                            Directory.Delete(tempIsoExtractPath);
                            AddLog("✓ Temporary files cleaned up");
                        }
                        catch (Exception ex)
                        {
                            AddLog($"⚠ Cleanup warning: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"✗ Error loading apps: {ex.Message}");
                MessageBox.Show($"An error occurred while loading apps from the ISO:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsProcessing = false;
                OverallProgressText = string.Empty;
                OverallProgress = 0;
            }
        }

        private bool CanExecuteInject()
        {
            return !IsProcessing &&
                   !string.IsNullOrEmpty(SelectedIsoPath) &&
                   HasDriverSources &&
                   File.Exists(SelectedIsoPath);
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

            // CRITICAL: Check for administrator privileges - auto-restart if needed
            if (!AdminHelper.IsRunningAsAdministrator())
            {
                AddLog("✗ ERROR: Administrator privileges are required for driver injection!");
                AddLog("Restarting application as administrator...");

                // Save current state before restarting
                var currentState = new AppState
                {
                    IsoPath = SelectedIsoPath,
                    DriverPaths = DriverSources.Select(d => d.Path).ToList(),
                    RemoveWindowsApps = RemoveWindowsApps,
                    SelectedAppsForRemoval = WindowsApps?.Where(a => a.IsSelected).Select(a => a.PackageName).ToList() ?? new List<string>(),
                    EnableUnattendedInstall = EnableUnattendedInstall
                };

                AdminHelper.RestartAsAdministrator(currentState);
                Application.Current.Shutdown();
                return;
            }

            // Get available editions and let user select
            AddLog("Loading Windows editions from ISO...");
            try
            {
                var editions = await _dismService.GetWimInfoFromIsoAsync(
                    SelectedIsoPath,
                    new Progress<string>(AddLog));

                if (editions == null || editions.Count == 0)
                {
                    MessageBox.Show(
                        "Failed to read Windows editions from ISO.\n\nPlease verify the ISO file is valid.",
                        "Edition Detection Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                AddLog($"Found {editions.Count} edition(s) in ISO");

                // Show edition selector dialog
                var editionDialog = new Dialogs.EditionSelectorWindow(editions);
                if (editionDialog.ShowDialog() != true)
                {
                    AddLog("Image preparation cancelled by user.");
                    return;
                }

                SelectedEditions = editionDialog.SelectedEditionIndices;
                AddLog($"Selected {SelectedEditions.Count} edition(s) for driver injection:");
                foreach (var index in SelectedEditions)
                {
                    var edition = editions.FirstOrDefault(e => e.ImageIndex == index);
                    if (edition != null)
                    {
                        AddLog($"  • {edition.ImageName} (Index {index})");
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"Error loading editions: {ex.Message}");
                MessageBox.Show(
                    $"Failed to load Windows editions:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            // Auto-prompt for app loading if Windows app removal is enabled
            if (RemoveWindowsApps)
            {
                // Check if user has already loaded apps from ISO or selected apps
                var hasSelectedApps = WindowsApps.Any(app => app.IsSelected);
                var hasIsoApps = WindowsApps.Any(app => app.PackageName.Contains("_") && app.PackageName.Split('_').Length >= 3);

                if (!hasSelectedApps && !hasIsoApps)
                {
                    // User has "Remove Windows apps" checked but hasn't loaded or selected any apps
                    var loadAppsResult = MessageBox.Show(
                        "Windows app removal is enabled, but no apps have been loaded from the ISO.\n\n" +
                        "Would you like to scan the ISO and load all provisioned apps now?\n\n" +
                        "• Click 'Yes' to scan the ISO for apps (recommended)\n" +
                        "• Click 'No' to use the default app list\n" +
                        "• Click 'Cancel' to abort and select apps manually",
                        "Load Apps from ISO?",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (loadAppsResult == MessageBoxResult.Cancel)
                    {
                        AddLog("Image preparation cancelled - user will select apps manually.");
                        return;
                    }
                    else if (loadAppsResult == MessageBoxResult.Yes)
                    {
                        AddLog("Loading apps automatically...");
                        try
                        {
                            await LoadAppsAsync();

                            // After loading, open the app selection dialog
                            if (WindowsApps.Any())
                            {
                                AddLog("Opening app selection dialog...");
                                var appDialog = new AppRemovalDialog(WindowsApps);
                                if (appDialog.ShowDialog() == true)
                                {
                                    // Update the main collection with selections
                                    for (int i = 0; i < WindowsApps.Count && i < appDialog.WindowsApps.Count; i++)
                                    {
                                        WindowsApps[i].IsSelected = appDialog.WindowsApps[i].IsSelected;
                                    }
                                    OnPropertyChanged(nameof(SelectedAppsCountText));

                                    var selectedCount = WindowsApps.Count(a => a.IsSelected);
                                    AddLog($"User selected {selectedCount} app(s) for removal");

                                    if (selectedCount == 0)
                                    {
                                        var proceedResult = MessageBox.Show(
                                            "No apps were selected for removal.\n\n" +
                                            "Do you want to continue without removing any apps?",
                                            "No Apps Selected",
                                            MessageBoxButton.YesNo,
                                            MessageBoxImage.Question);

                                        if (proceedResult != MessageBoxResult.Yes)
                                        {
                                            AddLog("Image preparation cancelled - no apps selected.");
                                            return;
                                        }
                                        else
                                        {
                                            // User wants to continue without removing apps
                                            RemoveWindowsApps = false;
                                            AddLog("Continuing without app removal.");
                                        }
                                    }
                                }
                                else
                                {
                                    AddLog("App selection cancelled by user.");
                                    return;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            AddLog($"✗ Failed to load apps from ISO: {ex.Message}");
                            var continueResult = MessageBox.Show(
                                $"Failed to load apps from ISO:\n\n{ex.Message}\n\n" +
                                "Do you want to continue with the default app list?",
                                "App Loading Failed",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning);

                            if (continueResult != MessageBoxResult.Yes)
                            {
                                AddLog("Image preparation cancelled.");
                                return;
                            }
                            else
                            {
                                AddLog("Continuing with default app list.");
                            }
                        }
                    }
                    else
                    {
                        // User chose No - use default list
                        AddLog("Using default app list.");
                    }
                }
                else if (hasIsoApps && !hasSelectedApps)
                {
                    // Apps were loaded but none selected - offer to open selection dialog
                    var selectResult = MessageBox.Show(
                        $"You have loaded {WindowsApps.Count} apps from the ISO, but none are selected for removal.\n\n" +
                        "Would you like to select apps now?",
                        "Select Apps?",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (selectResult == MessageBoxResult.Cancel)
                    {
                        AddLog("Image preparation cancelled.");
                        return;
                    }
                    else if (selectResult == MessageBoxResult.Yes)
                    {
                        var appDialog = new AppRemovalDialog(WindowsApps);
                        if (appDialog.ShowDialog() == true)
                        {
                            for (int i = 0; i < WindowsApps.Count && i < appDialog.WindowsApps.Count; i++)
                            {
                                WindowsApps[i].IsSelected = appDialog.WindowsApps[i].IsSelected;
                            }
                            OnPropertyChanged(nameof(SelectedAppsCountText));

                            var selectedCount = WindowsApps.Count(a => a.IsSelected);
                            AddLog($"User selected {selectedCount} app(s) for removal");

                            if (selectedCount == 0)
                            {
                                RemoveWindowsApps = false;
                                AddLog("No apps selected - continuing without app removal.");
                            }
                        }
                        else
                        {
                            AddLog("App selection cancelled by user.");
                            return;
                        }
                    }
                    else
                    {
                        // User chose No - disable app removal
                        RemoveWindowsApps = false;
                        AddLog("Continuing without app removal.");
                    }
                }
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
                ResetStageTimings();
                BeginStage("Extract ISO");
                OverallProgressText = "Step 1/7: Extracting ISO contents...";
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
                if (extracted) ClearDiscardedWork();

                if (!extracted)
                {
                    AddLog("✗ Failed to extract ISO");
                    return;
                }

                // Prepare driver sources
                OverallProgress = 25;
                OverallProgressText = $"Step 2/7: Validating {DriverSources.Count} driver source(s)...";
                CurrentOperationText = $"Validating {DriverSources.Count} driver source(s)...";
                CurrentOperationProgress = 0;
                AddLog($"Preparing {DriverSources.Count} driver source(s)...");

                // Validate all driver sources exist
                int validSources = 0;
                foreach (var source in DriverSources)
                {
                    if (source.Type == DriverSourceType.Folder)
                    {
                        if (Directory.Exists(source.Path))
                        {
                            validSources++;
                            AddLog($"  ✓ Valid folder: {source.DisplayName}");
                        }
                        else
                        {
                            AddLog($"  ✗ Folder not found: {source.Path}");
                        }
                    }
                    else // MSI or ZIP
                    {
                        if (File.Exists(source.Path))
                        {
                            validSources++;
                            AddLog($"  ✓ Valid {source.TypeLabel}: {source.DisplayName}");
                        }
                        else
                        {
                            AddLog($"  ✗ File not found: {source.Path}");
                        }
                    }
                }

                if (validSources == 0)
                {
                    AddLog("✗ No valid driver sources found");
                    return;
                }

                AddLog($"✓ Found {validSources}/{DriverSources.Count} valid driver source(s)");
                OverallProgress = 30;

                var ct = _cancellationTokenSource.Token;
                var settings = _settingsService.CurrentSettings;

                // Step 3: Cut install.wim down to the selected editions BEFORE servicing,
                // so we never mount editions that are about to be thrown away
                BeginStage("Prepare install image");
                OverallProgress = 32;
                OverallProgressText = "Step 3/7: Preparing install image (selected editions only)...";
                var editionIndices = await PrepareInstallImageAsync(ct);
                if (editionIndices == null)
                {
                    return;
                }

                // Step 4: Extract each driver pack ONCE, then build the boot-critical subset
                BeginStage("Stage drivers");
                OverallProgress = 36;
                OverallProgressText = "Step 4/7: Extracting driver packs (once)...";
                CurrentOperationText = "Extracting driver packs...";
                var stagedDrivers = await _driverService.StageDriverSourcesAsync(
                    DriverSources.ToList(),
                    Path.Combine(_config.DriversDirectory, "Staged"),
                    new Progress<string>(msg => { AddLog($"  {msg}"); CurrentOperationText = msg; }),
                    ct);
                if (stagedDrivers.Count == 0)
                {
                    AddLog("✗ No driver packs could be prepared");
                    return;
                }

                List<string> bootDrivers;
                if (settings.AllDriversInBootImages)
                {
                    AddLog("Boot images: every driver (Options > Put every driver in the boot images)");
                    bootDrivers = stagedDrivers;
                }
                else
                {
                    CurrentOperationText = "Selecting boot-critical drivers...";
                    var bootSet = await _driverService.BuildBootDriverSetAsync(
                        stagedDrivers,
                        settings.BootDriverClasses,
                        Path.Combine(_config.DriversDirectory, "BootSet"),
                        new Progress<string>(msg => AddLog($"  {msg}")),
                        ct);
                    bootDrivers = bootSet.InfCount > 0 ? new List<string> { bootSet.Folder } : new List<string>();
                    if (bootDrivers.Count == 0)
                    {
                        AddLog("⚠ No boot-critical drivers found in the packs - boot images will not be changed");
                    }
                }

                // Step 5: boot.wim (WinPE + Setup) gets the boot-critical subset only
                BeginStage("Boot images");
                OverallProgress = 40;
                OverallProgressText = "Step 5/7: Injecting drivers into boot images...";
                await InjectDriversToBootWimAsync(bootDrivers, ct);

                // Step 6: install.wim editions get the full set; WinRE is serviced once and reused
                BeginStage("Install image");
                OverallProgress = 55;
                OverallProgressText = RemoveWindowsApps && WindowsApps.Any(a => a.IsSelected)
                    ? "Step 6/7: Injecting drivers and removing apps..."
                    : "Step 6/7: Injecting drivers into Windows editions...";
                await InjectDriversToInstallWimAsync(editionIndices, stagedDrivers, bootDrivers, ct);

                // Step 7: Compact (drops superseded data) then split for FAT32 if needed
                BeginStage("Finalise image");
                OverallProgress = 85;
                OverallProgressText = "Step 7/7: Finalising image...";
                if (settings.CompactImageAfterServicing)
                {
                    await CompactInstallWimAsync(ct);
                }
                await SplitWimIfNeededAsync(ct);

                // Inject autounattend.xml if enabled
                if (EnableUnattendedInstall)
                {
                    AddLog("Generating unattended installation file...");
                    var unattendedService = new UnattendedInstallService();
                    var autounattendPath = Path.Combine(_config.Windows11Directory, "autounattend.xml");

                    var config = _settingsService.CurrentSettings.UnattendedInstallConfig ?? new Models.UnattendedConfig();

                    // Only include edition selection if multiple editions are in the image
                    // If user selected only one edition, don't specify it (let Windows install that one)
                    if (SelectedEditions != null && SelectedEditions.Count == 1)
                    {
                        // Single edition in image - don't specify edition in autounattend
                        // This prevents edition selection prompt
                        config = config.Clone();
                        config.TargetEdition = null;
                        AddLog("Single edition detected - edition selection will be automatic");
                    }
                    else if (SelectedEditions != null && SelectedEditions.Count > 1)
                    {
                        AddLog($"Multiple editions in image ({SelectedEditions.Count}) - user will select during install");
                    }

                    if (unattendedService.GenerateAutounattendXml(config, autounattendPath))
                    {
                        AddLog("✓ Autounattend.xml created successfully");
                    }
                    else
                    {
                        AddLog("⚠ Warning: Failed to create autounattend.xml");
                    }
                }

                OverallProgress = 100;
                var completionMsg = RemoveWindowsApps && WindowsApps.Any(a => a.IsSelected)
                    ? "Driver injection and app removal completed successfully!"
                    : "Driver injection completed successfully!";
                OverallProgressText = completionMsg;
                CurrentOperationProgress = 100;
                CurrentOperationText = "Complete";
                AddLog("=== Processing Complete ===");
                EndStage();
                LogStageSummary();
                if (RemoveWindowsApps && WindowsApps.Any(a => a.IsSelected))
                {
                    AddLog($"✓ Removed {WindowsApps.Count(a => a.IsSelected)} Windows app(s)");
                }
                AddLog("Ready to create bootable USB");

                // Re-enable commands by setting IsProcessing = false
                IsProcessing = false;

                // Check for pending update after operation completes
                await CheckPendingUpdateAsync();

                // Prompt user to create USB now or save for later
                var dialogMsg = RemoveWindowsApps && WindowsApps.Any(a => a.IsSelected)
                    ? "✓ Driver injection and app removal completed successfully!\n\n"
                    : "✓ Driver injection completed successfully!\n\n";

                var result = MessageBox.Show(
                    dialogMsg +
                    "What would you like to do next?\n\n" +
                    "• Click YES to create bootable USB now\n" +
                    "• Click NO to save project for later use\n" +
                    "• Click CANCEL to return to main screen",
                    "Processing Complete",
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

                // Check for pending update after operation completes
                await CheckPendingUpdateAsync();
            }
        }

        // ---------------------------------------------------------------------
        // Stage timing - logged so build times can be compared run to run
        // ---------------------------------------------------------------------
        private readonly List<(string Name, TimeSpan Elapsed)> _stageTimings = new();
        private Stopwatch? _stageWatch;
        private string? _stageName;
        private Stopwatch? _runWatch;

        private void ResetStageTimings()
        {
            _stageTimings.Clear();
            _stageWatch = null;
            _stageName = null;
            _runWatch = Stopwatch.StartNew();
        }

        private void BeginStage(string name)
        {
            EndStage();
            _stageName = name;
            _stageWatch = Stopwatch.StartNew();
        }

        private void EndStage()
        {
            if (_stageWatch != null && _stageName != null)
            {
                _stageWatch.Stop();
                _stageTimings.Add((_stageName, _stageWatch.Elapsed));
                AddLog($"⏱ {_stageName}: {_stageWatch.Elapsed:hh\\:mm\\:ss}");
            }
            _stageWatch = null;
            _stageName = null;
        }

        private void LogStageSummary()
        {
            if (_stageTimings.Count == 0) return;
            AddLog("=== Build timings ===");
            foreach (var (name, elapsed) in _stageTimings)
            {
                AddLog($"  {name,-24} {elapsed:hh\\:mm\\:ss}");
            }
            if (_runWatch != null)
            {
                AddLog($"  {"Total",-24} {_runWatch.Elapsed:hh\\:mm\\:ss}");
            }
        }

        /// <summary>
        /// Reduce install.wim/install.esd to only the selected editions before any servicing.
        /// Also converts install.esd to install.wim (ESD can't be mounted).
        /// Returns the indices to service in the resulting install.wim, or null on failure.
        /// </summary>
        private async Task<List<int>?> PrepareInstallImageAsync(CancellationToken cancellationToken)
        {
            var sourcesDir = Path.Combine(_config.Windows11Directory, "Sources");
            var wimPath = Path.Combine(sourcesDir, "install.wim");
            var esdPath = Path.Combine(sourcesDir, "install.esd");

            var isEsd = !File.Exists(wimPath) && File.Exists(esdPath);
            var sourcePath = isEsd ? esdPath : wimPath;
            if (!File.Exists(sourcePath))
            {
                AddLog("✗ install.wim / install.esd not found in extracted ISO");
                return null;
            }

            // Files copied from an ISO are often read-only
            File.SetAttributes(sourcePath, FileAttributes.Normal);

            var editions = await _dismService.GetWimInfoAsync(sourcePath, cancellationToken);
            if (editions.Count == 0)
            {
                AddLog("✗ Could not read editions from install image");
                return null;
            }

            var wanted = (SelectedEditions ?? new List<int>())
                .Where(i => editions.Any(e => e.ImageIndex == i))
                .Distinct()
                .OrderBy(i => i)
                .ToList();
            if (wanted.Count == 0)
            {
                wanted = editions.Select(e => e.ImageIndex).ToList();
                AddLog($"No specific editions selected - servicing all {wanted.Count}");
            }

            if (!isEsd && wanted.Count == editions.Count)
            {
                AddLog($"Keeping all {wanted.Count} edition(s); nothing to trim");
                return wanted;
            }

            AddLog(isEsd
                ? $"Converting install.esd to install.wim with {wanted.Count} of {editions.Count} edition(s)..."
                : $"Trimming install.wim to {wanted.Count} of {editions.Count} edition(s) before servicing...");
            foreach (var index in wanted)
            {
                AddLog($"  ✓ {editions.First(e => e.ImageIndex == index).ImageName}");
            }

            var tempPath = Path.Combine(sourcesDir, "install.prep.wim");
            var ok = await _dismService.ExportImagesAsync(
                sourcePath,
                wanted,
                tempPath,
                isEsd ? "max" : null, // keep existing compression for WIM - much faster
                new Progress<string>(msg => { AddLog($"  {msg}"); CurrentOperationText = msg; }),
                cancellationToken);
            if (!ok)
            {
                AddLog("✗ Failed to prepare install image");
                return null;
            }

            File.Delete(sourcePath);
            File.Move(tempPath, wimPath);
            AddLog($"✓ install.wim now holds {wanted.Count} edition(s)");

            // Exported images are renumbered 1..N
            return Enumerable.Range(1, wanted.Count).ToList();
        }

        /// <summary>
        /// Inject the given driver folders into a mounted image (one DISM call per folder)
        /// </summary>
        private async Task InjectDriverFoldersAsync(string mountPath, IReadOnlyList<string> driverFolders, CancellationToken cancellationToken)
        {
            for (int i = 0; i < driverFolders.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddLog($"  [{i + 1}/{driverFolders.Count}] Adding drivers from {driverFolders[i]}");
                var ok = await _dismService.AddDriversAsync(
                    mountPath,
                    driverFolders[i],
                    new Progress<string>(msg => AddLog($"    {msg}")),
                    cancellationToken);
                if (!ok)
                {
                    AddLog($"  ⚠ DISM reported a problem adding drivers from {driverFolders[i]} - continuing");
                }
            }
        }

        private async Task InjectDriversToBootWimAsync(IReadOnlyList<string> bootDrivers, CancellationToken cancellationToken)
        {
            if (bootDrivers.Count == 0)
            {
                AddLog("Skipping boot.wim - no boot-critical drivers to add");
                return;
            }

            var bootWimPath = Path.Combine(_config.Windows11Directory, "Sources", "boot.wim");
            if (!File.Exists(bootWimPath))
            {
                AddLog("⚠ boot.wim not found, skipping boot image drivers");
                return;
            }
            File.SetAttributes(bootWimPath, FileAttributes.Normal);

            var targets = new[]
            {
                (Index: 1, Name: "WinPE", Mount: _config.MountPEDirectory),
                (Index: 2, Name: "Windows Setup", Mount: _config.MountSetupDirectory)
            };

            foreach (var target in targets)
            {
                AddLog($"Injecting boot drivers into {target.Name}...");
                CurrentOperationText = $"Mounting {target.Name}...";
                CurrentOperationProgress = 0;
                FileSystemHelper.EnsureDirectoryExists(target.Mount);

                var mounted = await _dismService.MountWimAsync(bootWimPath, target.Index, target.Mount,
                    new Progress<string>(msg => { AddLog(msg); CurrentOperationText = msg; }), cancellationToken);
                if (!mounted)
                {
                    AddLog($"✗ Could not mount {target.Name}, skipping");
                    continue;
                }

                try
                {
                    CurrentOperationText = $"Adding drivers to {target.Name}...";
                    CurrentOperationProgress = 50;
                    await InjectDriverFoldersAsync(target.Mount, bootDrivers, cancellationToken);
                }
                finally
                {
                    CurrentOperationText = $"Unmounting {target.Name}...";
                    await _dismService.UnmountWimAsync(target.Mount, !cancellationToken.IsCancellationRequested,
                        new Progress<string>(msg => { AddLog(msg); CurrentOperationText = msg; }),
                        CancellationToken.None, deleteMountDirectory: true);
                }
            }

            CurrentOperationProgress = 100;
        }

        private async Task InjectDriversToInstallWimAsync(
            IReadOnlyList<int> editionIndices,
            IReadOnlyList<string> fullDrivers,
            IReadOnlyList<string> bootDrivers,
            CancellationToken cancellationToken)
        {
            var installWimPath = Path.Combine(_config.Windows11Directory, "Sources", "install.wim");
            var editions = await _dismService.GetWimInfoAsync(installWimPath, cancellationToken);

            // WinRE is (almost always) identical across editions in one ISO. Service it once,
            // then copy the result into every edition whose WinRE matches the original.
            string? servicedWinRe = null;
            string? originalWinReHash = null;
            var winReWorkDir = Path.Combine(_config.MountDirectory, "WinRE_Work");

            int count = 0;
            foreach (var editionIndex in editionIndices)
            {
                cancellationToken.ThrowIfCancellationRequested();
                count++;
                var editionName = editions.FirstOrDefault(e => e.ImageIndex == editionIndex)?.ImageName ?? $"Edition {editionIndex}";
                var mountPath = Path.Combine(_config.MountDirectory, $"Edition_{editionIndex}");
                FileSystemHelper.EnsureDirectoryExists(mountPath);

                AddLog($"Servicing {editionName} ({count}/{editionIndices.Count})...");
                CurrentOperationText = $"Mounting {editionName} ({count}/{editionIndices.Count})...";
                CurrentOperationProgress = 0;

                var mounted = await _dismService.MountWimAsync(installWimPath, editionIndex, mountPath,
                    new Progress<string>(msg => { AddLog(msg); CurrentOperationText = msg; }), cancellationToken);
                if (!mounted)
                {
                    AddLog($"✗ Could not mount {editionName}, skipping");
                    continue;
                }

                var commit = false;
                try
                {
                    CurrentOperationText = $"Adding drivers to {editionName} ({count}/{editionIndices.Count})...";
                    CurrentOperationProgress = 20;
                    await InjectDriverFoldersAsync(mountPath, fullDrivers, cancellationToken);

                    if (RemoveWindowsApps)
                    {
                        var appsToRemove = WindowsApps
                            .Where(app => app.IsSelected)
                            .SelectMany(app => app.PackageNames.Any() ? app.PackageNames : new List<string> { app.PackageName })
                            .ToList();

                        if (appsToRemove.Any())
                        {
                            AddLog($"Removing {appsToRemove.Count} Windows app package(s) from {editionName}...");
                            CurrentOperationText = $"Removing Windows apps from {editionName}...";
                            CurrentOperationProgress = 50;
                            await _dismService.RemoveProvisionedAppsAsync(mountPath, appsToRemove,
                                new Progress<string>(msg => { AddLog(msg); CurrentOperationText = msg; }), cancellationToken);
                        }
                    }

                    var winRePath = Path.Combine(mountPath, "Windows", "System32", "Recovery", "Winre.wim");
                    if (File.Exists(winRePath) && bootDrivers.Count > 0)
                    {
                        CurrentOperationProgress = 65;
                        var hash = await FileSystemHelper.ComputeSha256Async(winRePath, cancellationToken);

                        if (originalWinReHash == null)
                        {
                            AddLog($"  Servicing WinRE once (from {editionName})...");
                            servicedWinRe = await ServiceWinReAsync(winRePath, bootDrivers, winReWorkDir, cancellationToken);
                            originalWinReHash = hash;
                        }

                        if (hash == originalWinReHash && servicedWinRe == null)
                        {
                            AddLog($"  ⚠ WinRE servicing failed earlier - leaving WinRE unchanged in {editionName}");
                        }
                        else if (hash == originalWinReHash)
                        {
                            AddLog($"  Applying serviced WinRE to {editionName}");
                            var attrs = File.GetAttributes(winRePath);
                            File.SetAttributes(winRePath, FileAttributes.Normal);
                            File.Copy(servicedWinRe, winRePath, overwrite: true);
                            File.SetAttributes(winRePath, attrs);
                        }
                        else
                        {
                            // Different WinRE in this edition (unusual) - service it on its own
                            AddLog($"  WinRE in {editionName} differs - servicing separately");
                            var separate = await ServiceWinReAsync(winRePath, bootDrivers,
                                Path.Combine(_config.MountDirectory, $"WinRE_Work_{editionIndex}"), cancellationToken);
                            if (separate != null)
                            {
                                var attrs = File.GetAttributes(winRePath);
                                File.SetAttributes(winRePath, FileAttributes.Normal);
                                File.Copy(separate, winRePath, overwrite: true);
                                File.SetAttributes(winRePath, attrs);
                            }
                        }
                    }

                    commit = true;
                }
                finally
                {
                    CurrentOperationText = $"Unmounting {editionName}...";
                    CurrentOperationProgress = 90;
                    await _dismService.UnmountWimAsync(mountPath, commit && !cancellationToken.IsCancellationRequested,
                        new Progress<string>(msg => { AddLog(msg); CurrentOperationText = msg; }),
                        CancellationToken.None, deleteMountDirectory: true);
                }

                CurrentOperationProgress = 100;
            }

            try
            {
                if (Directory.Exists(winReWorkDir)) Directory.Delete(winReWorkDir, true);
            }
            catch
            {
                // Temp folder - cleaned with the rest of Temp later
            }
        }

        /// <summary>
        /// Copy a WinRE image out, add boot drivers, and export it compacted (WinRE has to fit
        /// in the recovery partition, so keeping it small matters). Returns the serviced file path.
        /// </summary>
        private async Task<string?> ServiceWinReAsync(string winRePath, IReadOnlyList<string> bootDrivers, string workDir, CancellationToken cancellationToken)
        {
            FileSystemHelper.EnsureDirectoryExists(workDir);
            var workWim = Path.Combine(workDir, "winre.wim");
            var compactWim = Path.Combine(workDir, "winre.serviced.wim");
            var mountDir = Path.Combine(workDir, "mount");
            FileSystemHelper.EnsureDirectoryExists(mountDir);

            File.Copy(winRePath, workWim, overwrite: true);
            File.SetAttributes(workWim, FileAttributes.Normal);

            var mounted = await _dismService.MountWimAsync(workWim, 1, mountDir,
                new Progress<string>(msg => AddLog($"    {msg}")), cancellationToken);
            if (!mounted)
            {
                AddLog("  ✗ Could not mount WinRE - leaving it unchanged");
                return null;
            }

            var commit = false;
            try
            {
                await InjectDriverFoldersAsync(mountDir, bootDrivers, cancellationToken);
                commit = true;
            }
            finally
            {
                await _dismService.UnmountWimAsync(mountDir, commit && !cancellationToken.IsCancellationRequested,
                    new Progress<string>(msg => AddLog($"    {msg}")), CancellationToken.None, deleteMountDirectory: true);
            }

            var exported = await _dismService.ExportImagesAsync(workWim, new[] { 1 }, compactWim, "max",
                new Progress<string>(msg => AddLog($"    {msg}")), cancellationToken);
            var result = exported ? compactWim : workWim;
            AddLog($"  ✓ WinRE serviced ({FileSystemHelper.FormatBytes(new FileInfo(result).Length)})");
            return result;
        }

        /// <summary>
        /// Export install.wim into a fresh file to drop data superseded by servicing
        /// </summary>
        private async Task CompactInstallWimAsync(CancellationToken cancellationToken)
        {
            var sourcesDir = Path.Combine(_config.Windows11Directory, "Sources");
            var wimPath = Path.Combine(sourcesDir, "install.wim");
            if (!File.Exists(wimPath)) return;

            var editions = await _dismService.GetWimInfoAsync(wimPath, cancellationToken);
            if (editions.Count == 0) return;

            var before = new FileInfo(wimPath).Length;
            AddLog($"Compacting install.wim ({FileSystemHelper.FormatBytes(before)})...");
            CurrentOperationText = "Compacting install.wim...";

            var tempPath = Path.Combine(sourcesDir, "install.compact.wim");
            var ok = await _dismService.ExportImagesAsync(wimPath,
                editions.Select(e => e.ImageIndex).OrderBy(i => i).ToList(),
                tempPath, "max",
                new Progress<string>(msg => { AddLog($"  {msg}"); CurrentOperationText = msg; }),
                cancellationToken);

            if (!ok)
            {
                AddLog("⚠ Compaction failed - keeping the uncompacted install.wim");
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                return;
            }

            File.SetAttributes(wimPath, FileAttributes.Normal);
            File.Delete(wimPath);
            File.Move(tempPath, wimPath);
            var after = new FileInfo(wimPath).Length;
            AddLog($"✓ install.wim compacted: {FileSystemHelper.FormatBytes(before)} → {FileSystemHelper.FormatBytes(after)}");
        }

        /// <summary>
        /// Split install.wim into .swm files. Always done, whatever its size, so the image works on a
        /// FAT32 USB, in an ISO and when saved for later.
        /// </summary>
        private async Task SplitWimIfNeededAsync(CancellationToken cancellationToken)
        {
            await _dismService.SplitInstallWimAsync(
                Path.Combine(_config.Windows11Directory, "Sources"),
                new Progress<string>(AddLog),
                cancellationToken);
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

        /// <summary>
        /// Save currently selected apps to settings
        /// </summary>
        private void SaveAppSelections()
        {
            try
            {
                var selectedPackages = new List<string>();

                foreach (var app in WindowsApps.Where(a => a.IsSelected))
                {
                    // Store all package names for this app (handles grouped apps with multiple packages)
                    selectedPackages.AddRange(app.PackageNames);
                }

                // Clone, modify, and save
                var settings = _settingsService.CurrentSettings.Clone();
                settings.SelectedAppsForRemoval = selectedPackages;
                _ = _settingsService.SaveSettingsAsync(settings);

                AddLog($"✓ Saved {selectedPackages.Count} package selections");
            }
            catch (Exception ex)
            {
                AddLog($"⚠ Failed to save app selections: {ex.Message}");
            }
        }

        /// <summary>
        /// Restore previously selected apps from settings
        /// </summary>
        private void RestoreSavedAppSelections()
        {
            try
            {
                var savedPackages = _settingsService.CurrentSettings.SelectedAppsForRemoval;

                if (savedPackages == null || !savedPackages.Any())
                    return;

                int restoredCount = 0;

                foreach (var app in WindowsApps)
                {
                    // Check if any of this app's package names are in the saved list
                    if (app.PackageNames.Any(pkg => savedPackages.Contains(pkg)))
                    {
                        app.IsSelected = true;
                        restoredCount++;
                    }
                }

                if (restoredCount > 0)
                {
                    AddLog($"✓ Restored {restoredCount} saved app selections");
                    OnPropertyChanged(nameof(SelectedAppsCountText));
                }
            }
            catch (Exception ex)
            {
                AddLog($"⚠ Failed to restore app selections: {ex.Message}");
            }
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
                MessageBox.Show(
                    "This operation requires administrator privileges.\n\n" +
                    "To create a bootable USB:\n" +
                    "1. Close this application\n" +
                    "2. Right-click on WinImagePrep and select 'Run as administrator'\n" +
                    "3. Your settings and selections will be preserved\n\n" +
                    "Note: Running as administrator will allow all features to work, " +
                    "but may affect drag-and-drop from File Explorer. You can use the " +
                    "Browse buttons or Load Config instead.",
                    "Administrator Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
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

                // Get unattended config if enabled
                UnattendedConfig? unattendedConfig = null;
                if (EnableUnattendedInstall && _settingsService.CurrentSettings.UnattendedInstallConfig != null)
                {
                    unattendedConfig = _settingsService.CurrentSettings.UnattendedInstallConfig;
                    AddLog("ℹ Unattended installation is enabled - autounattend.xml will be created");
                }

                var success = await _usbService.CreateBootableUsbAsync(
                    SelectedUsbDrive.DiskNumber,
                    _config.Windows11Directory,
                    IsoVolumeLabel,
                    unattendedConfig,
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

                // Check for pending update after operation completes
                await CheckPendingUpdateAsync();
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
                MessageBox.Show(
                    "This operation requires administrator privileges.\n\n" +
                    "To create a bootable USB:\n" +
                    "1. Close this application\n" +
                    "2. Right-click on WinImagePrep and select 'Run as administrator'\n" +
                    "3. Your settings and selections will be preserved\n\n" +
                    "Note: Running as administrator will allow all features to work, " +
                    "but may affect drag-and-drop from File Explorer. You can use the " +
                    "Browse buttons or Load Config instead.",
                    "Administrator Rights Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
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

                // Get unattended config if enabled
                UnattendedConfig? unattendedConfig = null;
                if (EnableUnattendedInstall && _settingsService.CurrentSettings.UnattendedInstallConfig != null)
                {
                    unattendedConfig = _settingsService.CurrentSettings.UnattendedInstallConfig;
                    AddLog("ℹ Unattended installation is enabled - autounattend.xml will be created");
                }

                var success = await _usbService.CreateBootableUsbAsync(
                    SelectedUsbDrive.DiskNumber,
                    _config.Windows11Directory,
                    IsoVolumeLabel,
                    unattendedConfig,
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

                // Check for pending update after operation completes
                await CheckPendingUpdateAsync();
            }
        }

        private void OpenSavedImageDialog()
        {
            var savedImageWindow = new Dialogs.SavedImageWindow(_config.SavedImagesDirectory, _settingsService.CurrentSettings.AdkSetupUrl);
            savedImageWindow.ShowDialog();
        }

        private bool CanCreateCustomIso()
        {
            if (IsProcessing || !Directory.Exists(_config.Windows11Directory))
                return false;

            // Check if boot files exist
            var bootDir = Path.Combine(_config.Windows11Directory, "boot");
            var efisysPath = Path.Combine(bootDir, "efisys.bin");
            var efisysNopromptPath = Path.Combine(bootDir, "efisys_noprompt.bin");

            return File.Exists(efisysPath) || File.Exists(efisysNopromptPath);
        }

        /// <summary>
        /// Return the path to oscdimg.exe, offering to install the Windows ADK Deployment Tools if
        /// they're missing. Returns null if they aren't installed (declined, cancelled or failed).
        /// </summary>
        private async Task<string?> EnsureIsoToolsAsync()
        {
            var wasProcessing = IsProcessing;
            try
            {
                return await IsoToolsPrompt.EnsureAsync(
                    _isoService,
                    _settingsService.CurrentSettings.AdkSetupUrl,
                    msg => { AddLog(msg); CurrentOperationText = msg; },
                    busy => IsProcessing = busy || wasProcessing);
            }
            finally
            {
                IsProcessing = wasProcessing;
            }
        }

        private async Task<string?> InstallIsoToolsAsync()
        {
            var wasProcessing = IsProcessing;
            try
            {
                return await IsoToolsPrompt.InstallAsync(
                    _isoService,
                    _settingsService.CurrentSettings.AdkSetupUrl,
                    msg => { AddLog(msg); CurrentOperationText = msg; },
                    busy => IsProcessing = busy || wasProcessing);
            }
            finally
            {
                IsProcessing = wasProcessing;
                CurrentOperationText = string.Empty;
            }
        }

        /// <summary>
        /// Tools > Check prerequisites: show what's installed and offer to install what's missing.
        /// </summary>
        private async Task CheckPrerequisitesAsync()
        {
            var results = new PrerequisiteService(_isoService).Check();
            AddLog("=== Prerequisites ===");
            foreach (var r in results)
            {
                AddLog($"  {(r.IsMet ? "✓" : "✗")} {r.Name}: {r.Detail}");
            }

            var text = string.Join("\n\n", results.Select(r => $"{(r.IsMet ? "✓" : "✗")}  {r.Name}\n      {r.Detail}"));
            var missing = results.Where(r => !r.IsMet && r.CanInstall).ToList();
            if (missing.Count == 0)
            {
                MessageBox.Show(text, "Prerequisites", MessageBoxButton.OK,
                    results.All(r => r.IsMet) ? MessageBoxImage.Information : MessageBoxImage.Warning);
                return;
            }

            var answer = MessageBox.Show(
                text + "\n\nInstall the Windows ADK Deployment Tools now? Win11 Image Prep downloads the setup " +
                "from Microsoft, checks its signature and installs only the Deployment Tools.",
                "Prerequisites", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Yes)
            {
                var path = await InstallIsoToolsAsync();
                if (path != null)
                {
                    MessageBox.Show("The Windows ADK Deployment Tools are installed. You can now create ISO files.",
                        "Prerequisites", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void OpenUrl(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AddLog($"Failed to open browser: {ex.Message}");
            }
        }

        private async Task CreateCustomIsoAsync()
        {
            try
            {
                // Validate that we have prepared content
                if (!Directory.Exists(_config.Windows11Directory) || 
                    !Directory.EnumerateFileSystemEntries(_config.Windows11Directory).Any())
                {
                    MessageBox.Show(
                        "No prepared Windows image found.\n\n" +
                        "Please first:\n" +
                        "1. Select a Windows ISO\n" +
                        "2. Run '🛡️ Prepare Image with Drivers'\n\n" +
                        "Then you can create a custom ISO from the prepared image.",
                        "No Prepared Image",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                // Check for boot files before proceeding
                var bootDir = Path.Combine(_config.Windows11Directory, "boot");
                var efisysPath = Path.Combine(bootDir, "efisys.bin");
                var efisysNopromptPath = Path.Combine(bootDir, "efisys_noprompt.bin");

                if (!File.Exists(efisysPath) && !File.Exists(efisysNopromptPath))
                {
                    MessageBox.Show(
                        "The prepared image is missing required boot files.\n\n" +
                        "This usually means the image preparation didn't complete successfully.\n\n" +
                        "Please:\n" +
                        "1. Select a valid Windows 11 ISO file\n" +
                        "2. Run '🛡️ Prepare Image with Drivers' again\n" +
                        "3. Wait for it to complete successfully\n\n" +
                        $"Missing files:\n" +
                        $"• {efisysPath}\n" +
                        $"• {efisysNopromptPath}\n\n" +
                        "Check the Operation Log for errors during preparation.",
                        "Missing Boot Files",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // ISO creation needs oscdimg from the Windows ADK Deployment Tools: offer to install it
                var oscdimgPath = await EnsureIsoToolsAsync();
                if (string.IsNullOrEmpty(oscdimgPath))
                {
                    return;
                }


                // Prompt for output ISO name and location
                var dialog = new SaveFileDialog
                {
                    Filter = "ISO Files (*.iso)|*.iso|All Files (*.*)|*.*",
                    Title = "Save Custom ISO",
                    FileName = $"Win11_Custom_{DateTime.Now:yyyyMMdd_HHmmss}.iso",
                    DefaultExt = ".iso"
                };

                if (dialog.ShowDialog() != true)
                {
                    AddLog("Custom ISO creation cancelled by user");
                    return;
                }

                var outputIsoPath = dialog.FileName;

                // Get volume label
                var volumeLabel = IsoVolumeLabel;
                if (string.IsNullOrEmpty(volumeLabel))
                {
                    volumeLabel = "WIN11_CUSTOM";
                }

                // Confirm operation
                var result = MessageBox.Show(
                    $"Create custom bootable ISO?\n\n" +
                    $"Source: {_config.Windows11Directory}\n" +
                    $"Output: {Path.GetFileName(outputIsoPath)}\n" +
                    $"Volume Label: {volumeLabel}\n\n" +
                    $"This operation may take several minutes.\n\n" +
                    $"The ISO can be used with:\n" +
                    $"• Ventoy multi-boot USB\n" +
                    $"• Rufus for USB creation\n" +
                    $"• Virtual machines\n" +
                    $"• Direct burning to DVD\n\n" +
                    $"Continue?",
                    "Create Custom ISO",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    AddLog("Custom ISO creation cancelled by user");
                    return;
                }

                IsProcessing = true;
                OverallProgress = 0;
                CurrentOperationProgress = 0;
                OverallProgressText = "Creating custom ISO...";
                CurrentOperationText = "Preparing...";
                AddLog("=== Creating Custom Bootable ISO ===");
                AddLog($"Output: {outputIsoPath}");
                AddLog($"Volume Label: {volumeLabel}");

                var progress = new Progress<string>(msg =>
                {
                    AddLog(msg);
                    // Simple progress indication
                    if (msg.Contains("oscdimg"))
                    {
                        OverallProgress = 20;
                        CurrentOperationText = "Running oscdimg...";
                    }
                    else if (msg.Contains("created successfully"))
                    {
                        OverallProgress = 100;
                        CurrentOperationText = "Complete";
                    }
                });

                var success = await _isoService.CreateBootableIsoAsync(
                    _config.Windows11Directory,
                    outputIsoPath,
                    volumeLabel,
                    progress);

                if (success)
                {
                    OverallProgress = 100;
                    CurrentOperationProgress = 100;
                    OverallProgressText = "Custom ISO created successfully!";
                    CurrentOperationText = "Complete";
                    AddLog("=== Custom ISO Creation Complete ===");

                    var openResult = MessageBox.Show(
                        $"Custom bootable ISO created successfully!\n\n" +
                        $"Location: {outputIsoPath}\n\n" +
                        $"You can now:\n" +
                        $"• Copy to Ventoy USB for multi-boot\n" +
                        $"• Use with Rufus to create bootable USB\n" +
                        $"• Mount in virtual machine\n\n" +
                        $"Open containing folder?",
                        "Success",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (openResult == MessageBoxResult.Yes)
                    {
                        // Open folder and select the ISO
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{outputIsoPath}\"");
                    }
                }
                else
                {
                    OverallProgressText = "ISO creation failed";
                    AddLog("✗ Custom ISO creation failed");

                    // Check if oscdimg is available to provide specific guidance
                    var oscdimgCheck = _isoService.FindOscdimgPath();
                    string errorMessage;

                    if (string.IsNullOrEmpty(oscdimgCheck))
                    {
errorMessage = "Failed to create custom ISO.\n\n" +
                            "The Windows ADK Deployment Tools aren't installed. Use Tools > Check prerequisites " +
                            "to install them, then try again.\n\n" +
                            "Check the log for details.";
                    }
                    else
                    {
                        errorMessage = "Failed to create custom ISO.\n\n" +
                            "Common issues:\n" +
                            "• Insufficient disk space\n" +
                            "• Missing boot files in prepared image\n" +
                            "• Output path is not writable\n\n" +
                            "Check the log for details.";
                    }

                    MessageBox.Show(
                        errorMessage,
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                OverallProgressText = "Error occurred";
                CurrentOperationText = ex.Message;
                AddLog($"✗ Error creating custom ISO: {ex.Message}");
                MessageBox.Show(
                    $"Error creating custom ISO:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsProcessing = false;

                // Check for pending update after operation completes
                await CheckPendingUpdateAsync();
            }
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

        #region Menu Commands

        private void SaveConfiguration()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Configuration Files (*.wip)|*.wip|All Files (*.*)|*.*",
                Title = "Save Configuration",
                FileName = "WinImagePrep_Config.wip"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var config = new
                    {
                        IsoPath = SelectedIsoPath,
                        DriverSources = DriverSources.Select(ds => new
                        {
                            Path = ds.Path,
                            Type = ds.Type.ToString()
                        }).ToList(),
                        RemoveWindowsApps = RemoveWindowsApps,
                        SelectedApps = WindowsApps.Where(a => a.IsSelected).Select(a => a.PackageName).ToList(),
                        SelectedEditionIndices = SelectedEditions,
                        EnableUnattendedInstall = EnableUnattendedInstall,
                        UnattendedConfig = EnableUnattendedInstall ? _settingsService.CurrentSettings.UnattendedInstallConfig : null
                    };

                    var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(dialog.FileName, json);

                    AddLog($"✓ Configuration saved: {Path.GetFileName(dialog.FileName)}");
                    MessageBox.Show("Configuration saved successfully!", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    AddLog($"✗ Error saving configuration: {ex.Message}");
                    MessageBox.Show($"Error saving configuration:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OpenConfiguration()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Configuration Files (*.wip)|*.wip|All Files (*.*)|*.*",
                Title = "Open Configuration"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    LoadConfigFromFile(dialog.FileName);
                }
                catch (Exception ex)
                {
                    AddLog($"✗ Error loading configuration: {ex.Message}");
                    MessageBox.Show($"Error loading configuration:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void LoadConfigFromFile(string filePath)
        {
            try
            {
                AddLog($"Loading configuration from: {Path.GetFileName(filePath)}");
                var json = File.ReadAllText(filePath);
                var config = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);

                // Load ISO path
                try
                {
                    if (config.TryGetProperty("IsoPath", out var isoPath))
                    {
                        var path = isoPath.GetString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(path))
                        {
                            SelectedIsoPath = path;
                            AddLog($"  Loaded ISO: {Path.GetFileName(path)}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"⚠ Warning: Failed to load ISO path - {ex.Message}");
                }

                // Load driver sources (new format)
                try
                {
                    if (config.TryGetProperty("DriverSources", out var driverSources))
                    {
                        DriverSources.Clear();
                        foreach (var ds in driverSources.EnumerateArray())
                        {
                            if (ds.TryGetProperty("Path", out var dsPath) && ds.TryGetProperty("Type", out var dsType))
                            {
                                var path = dsPath.GetString();
                                var typeStr = dsType.GetString();

                                if (!string.IsNullOrEmpty(path) && Enum.TryParse<DriverSourceType>(typeStr, out var type))
                                {
                                    DriverSources.Add(new DriverSourceInfo
                                    {
                                        Path = path,
                                        Type = type
                                    });
                                    AddLog($"  Loaded driver: {Path.GetFileName(path)}");
                                }
                            }
                        }
                    }
                    // Backward compatibility: Load old single driver source format
                    else if (config.TryGetProperty("DriverSourcePath", out var driverPath))
                    {
                        var path = driverPath.GetString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(path))
                        {
                            DriverSourceType type = DriverSourceType.Folder; // Default
                            if (config.TryGetProperty("DriverSourceType", out var sourceType))
                            {
                                if (Enum.TryParse<DriverSourceType>(sourceType.GetString(), out var parsedType))
                                    type = parsedType;
                            }

                            DriverSources.Clear();
                            DriverSources.Add(new DriverSourceInfo
                            {
                                Path = path,
                                Type = type
                            });
                            AddLog($"  Loaded driver (legacy format): {Path.GetFileName(path)}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"⚠ Warning: Failed to load driver sources - {ex.Message}");
                }

                // Load app removal settings
                try
                {
                    if (config.TryGetProperty("RemoveWindowsApps", out var removeApps))
                    {
                        RemoveWindowsApps = removeApps.GetBoolean();
                        AddLog($"  Windows app removal: {RemoveWindowsApps}");
                    }

                    if (config.TryGetProperty("SelectedApps", out var selectedApps) && 
                        selectedApps.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        var appList = selectedApps.EnumerateArray()
                            .Where(e => e.ValueKind == System.Text.Json.JsonValueKind.String)
                            .Select(e => e.GetString())
                            .Where(s => !string.IsNullOrEmpty(s))
                            .ToList();

                        if (WindowsApps != null)
                        {
                            foreach (var app in WindowsApps)
                            {
                                app.IsSelected = appList.Contains(app.PackageName);
                            }
                            OnPropertyChanged(nameof(SelectedAppsCountText));
                            AddLog($"  Loaded {appList.Count} selected apps for removal");
                        }
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"⚠ Warning: Failed to load app removal settings - {ex.Message}");
                }

                // Load edition selection
                try
                {
                    if (config.TryGetProperty("SelectedEditionIndices", out var editions) && 
                        editions.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        SelectedEditions = editions.EnumerateArray()
                            .Where(e => e.ValueKind == System.Text.Json.JsonValueKind.Number)
                            .Select(e => e.GetInt32())
                            .ToList();
                        AddLog($"  Loaded {SelectedEditions.Count} selected edition(s)");
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"⚠ Warning: Failed to load edition selection - {ex.Message}");
                }

                // Load unattended install settings
                try
                {
                    if (config.TryGetProperty("EnableUnattendedInstall", out var enableUnattended))
                    {
                        EnableUnattendedInstall = enableUnattended.GetBoolean();
                        AddLog($"  Unattended install: {EnableUnattendedInstall}");
                    }

                    if (config.TryGetProperty("UnattendedConfig", out var unattendedConfig) && 
                        unattendedConfig.ValueKind != System.Text.Json.JsonValueKind.Null)
                    {
                        var unattendedConfigObj = System.Text.Json.JsonSerializer.Deserialize<UnattendedConfig>(unattendedConfig.GetRawText());
                        if (unattendedConfigObj != null)
                        {
                            var settings = _settingsService.CurrentSettings.Clone();
                            settings.UnattendedInstallConfig = unattendedConfigObj;
                            _ = _settingsService.SaveSettingsAsync(settings);
                            AddLog($"  Loaded unattended config");
                        }
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"⚠ Warning: Failed to load unattended install settings - {ex.Message}");
                }

                AddLog($"✓ Configuration loaded: {Path.GetFileName(filePath)}");
                MessageBox.Show("Configuration loaded successfully!", "Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AddLog($"✗ Error loading configuration: {ex.Message}");
                MessageBox.Show(
                    $"Error loading configuration:\n\n{ex.Message}\n\n" +
                    "The configuration file may be from an older version or corrupted.\n" +
                    "Check the Operation Log for details.",
                    "Error Loading Configuration",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ExitApplication()
        {
            Application.Current.Shutdown();
        }

        private void OpenUserGuide()
        {
            // The docs site is the user guide; the old bundled HTML guide described v4
            OpenUrl("https://docs.andykemp.com/win11-image-prep/");
            AddLog("✓ Opened the user guide (docs.andykemp.com)");
        }

        private void OpenOnlineDocumentation()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://docs.andykemp.com/win11-image-prep/",
                    UseShellExecute = true
                });
                AddLog("✓ Opened online documentation");
            }
            catch (Exception ex)
            {
                AddLog($"✗ Error opening documentation: {ex.Message}");
                MessageBox.Show(
                    "Could not open documentation. Please visit:\nhttps://docs.andykemp.com/win11-image-prep/",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void OpenGitHubReadme()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://downloads.andykemp.com/win11-image-prep/",
                    UseShellExecute = true
                });
                AddLog("✓ Opened the downloads page");
            }
            catch (Exception ex)
            {
                AddLog($"✗ Error opening the downloads page: {ex.Message}");
                MessageBox.Show(
                    "Could not open the downloads page. Please visit:\nhttps://downloads.andykemp.com/win11-image-prep/",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }



        private void OpenReportIssue()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://www.andykemp.com/contact/",
                    UseShellExecute = true
                });
                AddLog("✓ Opened the contact page");
            }
            catch (Exception ex)
            {
                AddLog($"✗ Error opening issues page: {ex.Message}");
                MessageBox.Show(
                    "Could not open the contact page. Please visit:\nhttps://www.andykemp.com/contact/",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void OpenReleaseNotes()
        {
            OpenUrl("https://docs.andykemp.com/win11-image-prep/whats-new/");
            AddLog("✓ Opened release notes (What's new)");
        }

        private void ConfigureUnattendedInstall()
        {
            try
            {
                var currentConfig = _settingsService.CurrentSettings.UnattendedInstallConfig;
                var dialog = new UnattendedConfigDialog(currentConfig);

                if (dialog.ShowDialog() == true)
                {
                    // Save the configuration
                    var settings = _settingsService.CurrentSettings.Clone();
                    settings.UnattendedInstallConfig = dialog.Config;
                    _ = _settingsService.SaveSettingsAsync(settings);

                    AddLog("✓ Unattended install configuration saved");
                }
            }
            catch (Exception ex)
            {
                AddLog($"✗ Error configuring unattended install: {ex.Message}");
                MessageBox.Show(
                    $"Error opening configuration dialog: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ShowAbout()
        {
            try
            {
                var settingsService = new SettingsService();
                var aboutDialog = new AboutDialog(settingsService);
                aboutDialog.ShowDialog();
                AddLog("✓ Displayed About dialog");
            }
            catch (Exception ex)
            {
                AddLog($"✗ Error showing About dialog: {ex.Message}");
            }
        }

        private void OpenOptions()
        {
            try
            {
                // Create settings service
                var settingsService = new SettingsService();

                // Create OptionsViewModel
                var optionsViewModel = new OptionsViewModel(settingsService);

                // Create and show Options window
                var optionsWindow = new OptionsWindow(optionsViewModel);
                var result = optionsWindow.ShowDialog();

                if (result == true)
                {
                    // Settings were saved, reload configuration
                    AddLog("✓ Settings updated successfully");
                    AddLog($"ℹ New working folder: {settingsService.CurrentSettings.WorkingRoot}");
                    AddLog("⚠ Some settings will take effect after restarting the application");

                    MessageBox.Show(
                        "Settings have been saved.\n\n" +
                        "Some changes (like working folder location) will take effect after restarting the application.\n\n" +
                        "Consider restarting the application now if you changed the working folder.",
                        "Settings Saved",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    AddLog("ℹ Settings dialog cancelled");
                }
            }
            catch (Exception ex)
            {
                AddLog($"Error opening options: {ex.Message}");
                MessageBox.Show($"Error opening options: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                AddLog("Checking for updates...");

                var (updateAvailable, latestVersion) = await _updateService.CheckForUpdateAsync();

                if (updateAvailable && latestVersion != null)
                {
                    var currentVersionStr = _updateService.GetCurrentVersionString();
                    var latestVersionStr = $"{latestVersion.Major}.{latestVersion.Minor}.{latestVersion.Build}";
                    AddLog($"✓ Update available: v{latestVersionStr} (current: v{currentVersionStr})");

                    // Check if an operation is currently running
                    if (IsProcessing)
                    {
                        AddLog("⏳ Operation in progress. Update will be deferred until completion.");

                        var deferResult = MessageBox.Show(
                            $"A new version of WinImagePrep is available!\n\n" +
                            $"Current version: {currentVersionStr}\n" +
                            $"Latest version: {latestVersionStr}\n\n" +
                            $"An operation is currently in progress.\n\n" +
                            $"The update will be available after your current operation completes.\n\n" +
                            $"Would you like to be prompted to update when the operation finishes?",
                            "Update Available - Operation In Progress",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);

                        if (deferResult == MessageBoxResult.Yes)
                        {
                            PendingUpdate = true;
                            PendingUpdateVersion = latestVersion;
                            AddLog($"✓ Update to v{latestVersionStr} will be prompted after operation completes");
                        }
                        else
                        {
                            AddLog("Update deferred. You can check for updates later from Tools menu.");
                        }
                        return;
                    }

                    var result = MessageBox.Show(
                        $"A new version of WinImagePrep is available!\n\n" +
                        $"Current version: {currentVersionStr}\n" +
                        $"Latest version: {latestVersionStr}\n\n" +
                        $"Would you like to download and install the update now?\n\n" +
                        $"The application will close, download the update, and restart automatically.",
                        "Update Available",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        AddLog("Starting update download...");

                        var success = await _updateService.DownloadAndApplyUpdateAsync(
                            new Progress<string>(msg => AddLog(msg)));

                        if (success)
                        {
                            AddLog("Update downloaded. Application will now close and update...");
                            // The updater script will close the app
                            Application.Current.Shutdown();
                        }
                        else
                        {
                            AddLog("⚠ Update cancelled or failed.");
                            MessageBox.Show(
                                "Update was cancelled or failed.\n\n" +
                                "Administrator privileges are required to update the application.\n\n" +
                                "You can manually download the latest version from:\n" +
                                "https://downloads.andykemp.com/win11-image-prep/",
                                "Update Failed",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                    }
                    else
                    {
                        AddLog("Update cancelled by user");
                    }
                }
                else
                {
                    var currentVersionStr = _updateService.GetCurrentVersionString();
                    AddLog($"✓ You are running the latest version (v{currentVersionStr})");
                    MessageBox.Show(
                        $"You are running the latest version!\n\nCurrent version: {currentVersionStr}",
                        "No Updates Available",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                AddLog($"✗ Error opening options: {ex.Message}");
                MessageBox.Show($"Error opening options: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Performs a first-run update check if the app has just been installed
        /// or checks on every startup if CheckForUpdates is enabled
        /// </summary>
        public async Task PerformFirstRunUpdateCheckAsync()
        {
            AddLog("📥 PerformFirstRunUpdateCheckAsync() called");

            try
            {
                var settings = _settingsService.CurrentSettings;

                AddLog($"🔍 Update check: FirstRunComplete={settings.FirstRunUpdateCheckComplete}, CheckForUpdates={settings.CheckForUpdates}");

                // Check if user has enabled automatic update checks
                bool shouldCheck = !settings.FirstRunUpdateCheckComplete || settings.CheckForUpdates;

                AddLog($"🔍 shouldCheck = {shouldCheck} (!{settings.FirstRunUpdateCheckComplete} || {settings.CheckForUpdates})");

                if (!shouldCheck)
                {
                    AddLog("⚙ Automatic update checks disabled in settings");
                    return;
                }

                bool isFirstRun = !settings.FirstRunUpdateCheckComplete;

                if (isFirstRun)
                {
                    AddLog($"🆕 First-run update check initiated");
                }
                else
                {
                    AddLog("🔄 Startup update check initiated (CheckForUpdates=true)");
                }

                // Wait a moment for the window to fully load
                await Task.Delay(500);

                if (isFirstRun)
                {
                    AddLog("Checking for updates (first run)...");
                }
                else
                {
                    AddLog("Checking for updates...");
                }

                var (updateAvailable, latestVersion) = await _updateService.CheckForUpdateAsync();

                // Mark first-run check as complete if this was the first run
                if (isFirstRun)
                {
                    // Reload settings to ensure we have the latest (especially FirstRunComplete from wizard)
                    await _settingsService.ReloadSettingsAsync();
                    var currentSettings = _settingsService.CurrentSettings;

                    // Clone and only update FirstRunUpdateCheckComplete
                    var updatedSettings = currentSettings.Clone();
                    updatedSettings.FirstRunUpdateCheckComplete = true;

                    AddLog($"Marking first-run update check complete. Current FirstRunComplete={currentSettings.FirstRunComplete}");
                    await _settingsService.SaveSettingsAsync(updatedSettings);
                }

                if (updateAvailable && latestVersion != null)
                {
                    var currentVersionStr = _updateService.GetCurrentVersionString();
                    var latestVersionStr = $"{latestVersion.Major}.{latestVersion.Minor}.{latestVersion.Build}";
                    AddLog($"✓ Update available: v{latestVersionStr} (current: v{currentVersionStr})");

                    // Check if an operation is currently running
                    if (IsProcessing)
                    {
                        AddLog("⏳ Operation in progress. Update will be deferred until completion.");

                        // Silently defer the update
                        PendingUpdate = true;
                        PendingUpdateVersion = latestVersion;
                        AddLog($"✓ Update to v{latestVersionStr} will be prompted after operation completes");
                        return;
                    }

                    // Ensure we're on the UI thread and window is fully loaded
                    bool updateNow = false;
                    bool dontAskAgain = false;

                    try
                    {
                        AddLog("📋 Showing update dialog...");
                        AddLog($"   Current: v{currentVersionStr}, Latest: v{latestVersionStr}");
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                string message = $"A new version of WinImagePrep is available!\n\n" +
                                    $"Current version: v{currentVersionStr}\n" +
                                    $"Latest version: v{latestVersionStr}\n\n" +
                                    $"Would you like to download and install the update now?\n\n" +
                                    $"The application will close, download the update, and restart automatically.\n" +
                                    $"Estimated time: ~1 minute (depending on download speed)";

                                AddLog($"📝 Message length: {message.Length} characters");
                                var dialog = new Dialogs.UpdatePromptDialog(message);
                                dialog.Owner = Application.Current.MainWindow;
                                AddLog("🔄 Waiting for user response...");
                                var dialogResult = dialog.ShowDialog();
                                AddLog($"✓ Dialog result: {dialogResult}, UpdateNow: {dialog.UpdateNow}");

                                updateNow = dialog.UpdateNow && dialogResult == true;
                                dontAskAgain = dialog.DontAskAgain;
                            }
                            catch (Exception ex)
                            {
                                AddLog($"⚠ Error showing update dialog: {ex.Message}");
                                Logger.Error($"Update dialog error: {ex.Message}");
                            }
                        });
                        AddLog($"📋 Dialog complete. updateNow={updateNow}, dontAskAgain={dontAskAgain}");
                    }
                    catch (Exception ex)
                    {
                        AddLog($"✗ Update check failed: {ex.Message}");
                        Logger.Error($"Update check failed: {ex}");
                        return;
                    }

                    // Handle "Don't ask again" checkbox
                    if (dontAskAgain)
                    {
                        settings.CheckForUpdates = false;
                        await _settingsService.SaveSettingsAsync(settings);
                        AddLog("⚙ Automatic update checks disabled");
                    }

                    if (updateNow)
                    {
                        AddLog("Starting update download...");

                        var success = await _updateService.DownloadAndApplyUpdateAsync(
                            new Progress<string>(msg => AddLog(msg)));

                        if (success)
                        {
                            AddLog("Update downloaded. Application will now close and update...");
                            // The updater script will close the app
                            Application.Current.Shutdown();
                        }
                        else
                        {
                            AddLog("⚠ Update failed. Please download manually from GitHub.");
                            MessageBox.Show(
                                "Update failed. You can check for updates later from the Tools menu.",
                                "Update Failed",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                    }
                    else
                    {
                        AddLog("Update postponed. You can update later from Tools > Check for Updates");
                    }
                }
                                                                else
                                                                {
                                                                    var currentVersionStr = _updateService.GetCurrentVersionString();
                                                                    AddLog($"✓ You are running the latest version (v{currentVersionStr})");
                                                                }
                                                            }
                                                            catch (Exception ex)
                                                            {
                                                                AddLog($"✗ Update check failed: {ex.Message}");
                                                                Logger.Warning($"Update check failed: {ex.Message}");
                                                            }
                                                        }

        /// <summary>
        /// Checks if there's a pending update and prompts user after operation completes
        /// </summary>
        public async Task CheckPendingUpdateAsync()
        {
            try
            {
                if (PendingUpdate && PendingUpdateVersion != null)
                {
                    var currentVersionStr = _updateService.GetCurrentVersionString();
                    var latestVersionStr = $"{PendingUpdateVersion.Major}.{PendingUpdateVersion.Minor}.{PendingUpdateVersion.Build}";

                    AddLog($"Operation complete. Update to v{latestVersionStr} is ready.");

                    var result = MessageBox.Show(
                        $"Your operation has completed!\n\n" +
                        $"An update is ready to install:\n\n" +
                        $"Current version: {currentVersionStr}\n" +
                        $"Latest version: {latestVersionStr}\n\n" +
                        $"Would you like to update now?\n\n" +
                        $"The application will close, download the update, and restart automatically.\n" +
                        $"Estimated time: ~1 minute",
                        "Update Ready",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        AddLog("Starting update download...");

                        var success = await _updateService.DownloadAndApplyUpdateAsync(
                            new Progress<string>(msg => AddLog(msg)));

                        if (success)
                        {
                            AddLog("Update downloaded. Application will now close and update...");
                            // The updater script will close the app
                            Application.Current.Shutdown();
                        }
                        else
                        {
                            AddLog("⚠ Update failed. Please download manually from GitHub.");
                            MessageBox.Show(
                                "Update failed. You can check for updates later from the Tools menu.",
                                "Update Failed",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                    }
                    else
                    {
                        AddLog("Update postponed. You can update later from Tools > Check for Updates");
                    }

                    // Clear pending update flag regardless of user choice
                    PendingUpdate = false;
                    PendingUpdateVersion = null;
                }
            }
            catch (Exception ex)
            {
                AddLog($"✗ Pending update check failed: {ex.Message}");
                Logger.Warning($"Pending update check failed: {ex.Message}");
                PendingUpdate = false;
                PendingUpdateVersion = null;
            }
        }

        #endregion

        #region Helpers

        // "Start fresh" on old work that can't all be deleted yet: remember it so the question isn't asked again
        private static string DiscardedWorkMarker => Path.Combine(Models.AppSettings.SettingsDirectory, "discarded-work.txt");

        private static bool IsDiscardedWork(string directory)
        {
            try
            {
                return File.Exists(DiscardedWorkMarker) &&
                       string.Equals(File.ReadAllText(DiscardedWorkMarker).Trim(), directory.Trim(), StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static void MarkDiscardedWork(string directory)
        {
            try
            {
                Directory.CreateDirectory(Models.AppSettings.SettingsDirectory);
                File.WriteAllText(DiscardedWorkMarker, directory);
            }
            catch (Exception ex) { Logger.Warning($"Could not record discarded work: {ex.Message}"); }
        }

        private static void ClearDiscardedWork()
        {
            try { if (File.Exists(DiscardedWorkMarker)) File.Delete(DiscardedWorkMarker); } catch { }
        }

        /// <summary>Delete a folder with administrator rights (one UAC prompt). False if declined or it failed.</summary>
        private static bool DeleteDirectoryAsAdministrator(string directory)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c attrib -r -s -h \"{directory}\\*\" /s /d >nul 2>&1 & rd /s /q \"{directory}\"",
                    Verb = "runas",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                p?.WaitForExit(120_000);
            }
            catch (System.ComponentModel.Win32Exception) { return false; } // UAC declined
            catch (Exception ex) { Logger.Warning($"Elevated delete failed: {ex.Message}"); }
            return !Directory.Exists(directory);
        }

        public async Task CheckForExistingWorkAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    // Check if Windows11 directory exists and has content
                    if (!Directory.Exists(_config.Windows11Directory))
                    {
                        ClearDiscardedWork();
                        return;
                    }

                    var files = Directory.GetFiles(_config.Windows11Directory, "*", SearchOption.AllDirectories);
                    if (files.Length == 0)
                    {
                        // Directory exists but is empty, just delete it
                        try
                        {
                            Directory.Delete(_config.Windows11Directory, false);
                        }
                        catch { /* Ignore cleanup failures */ }
                        ClearDiscardedWork();
                        return;
                    }

                    // Already discarded ("No" last time) but not all of it could be deleted then: try again
                    // quietly and never ask about it again. The next build clears the folder anyway.
                    if (IsDiscardedWork(_config.Windows11Directory))
                    {
                        if (FileSystemHelper.ForceDeleteDirectory(_config.Windows11Directory, true)) ClearDiscardedWork();
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
                            // User wants to start fresh - delete the directory. Files from an elevated build
                            // may need administrator rights; if they still can't all go, the folder is marked as
                            // discarded so this isn't asked again, and the next build clears it.
                            var dir = _config.Windows11Directory;
                            AddLog("Deleting existing temporary files...");
                            bool deleted = false;
                            try { deleted = FileSystemHelper.ForceDeleteDirectory(dir, true); }
                            catch (Exception ex) { AddLog($"⚠ {ex.Message}"); }

                            if (!deleted && !AdminHelper.IsRunningAsAdministrator())
                            {
                                AddLog("Some files were written by an administrator build; asking Windows for permission to delete them...");
                                deleted = DeleteDirectoryAsAdministrator(dir);
                            }

                            if (deleted)
                            {
                                AddLog($"✓ Deleted temporary directory: {dir}");
                                ClearDiscardedWork();
                            }
                            else
                            {
                                MarkDiscardedWork(dir);
                                AddLog("⚠ Some files couldn't be deleted now. You won't be asked about them again, and they're cleared when you next build.");
                            }
                            UpdateImageReadyState();
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

        /// <summary>
        /// Checks if there's a pending update from first-run and applies it
        /// </summary>
        public async Task CheckPendingFirstRunUpdateAsync()
        {
            try
            {
                var settings = _settingsService.CurrentSettings;

                if (!string.IsNullOrEmpty(settings.PendingUpdateVersion))
                {
                    AddLog($"Pending update detected: v{settings.PendingUpdateVersion}");
                    AddLog("Starting update download...");

                    var success = await _updateService.DownloadAndApplyUpdateAsync(
                        new Progress<string>(msg => AddLog(msg)));

                    if (success)
                    {
                        AddLog("Update downloaded. Application will now close and update...");
                        // The updater script will close the app
                        await Task.Delay(1000); // Give user time to see the message
                        Application.Current.Shutdown();
                    }
                    else
                    {
                        AddLog("⚠ Update failed. You can try again from Tools > Check for Updates.");

                        // Clear the pending update flag
                        var updatedSettings = settings.Clone();
                        updatedSettings.PendingUpdateVersion = null;
                        await _settingsService.SaveSettingsAsync(updatedSettings);
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"✗ Failed to apply pending update: {ex.Message}");
                Logger.Error($"Pending update failed: {ex.Message}");

                // Clear the pending update flag
                try
                {
                    var settings = _settingsService.CurrentSettings.Clone();
                    settings.PendingUpdateVersion = null;
                    await _settingsService.SaveSettingsAsync(settings);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        public void AddLog(string message)
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

        #region Cancellation and Disposal

        public void CancelOperation()
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                AddLog("⚠ Cancelling operation...");
                _cancellationTokenSource.Cancel();

                // Give processes a moment to respond to cancellation
                Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    try
                    {
                        CleanupHelper.CleanupMountedImages();
                        AddLog("✓ Cleanup completed after cancellation");
                    }
                    catch (Exception ex)
                    {
                        AddLog($"✗ Cleanup warning: {ex.Message}");
                    }
                });
            }
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

    // Generic RelayCommand implementation
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter)
        {
            if (_canExecute == null) return true;
            if (parameter is T typedParam)
                return _canExecute(typedParam);
            if (parameter == null && default(T) == null)
                return _canExecute(default(T));
            return false;
        }

        public void Execute(object? parameter)
        {
            if (parameter is T typedParam)
                _execute(typedParam);
            else if (parameter == null && default(T) == null)
                _execute(default(T));
        }
    }
}
