# Changelog

All notable changes to **WinImagePrep** by Andy Kemp Consulting Ltd will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [5.0.10] - 2026-06-04

### Fixed
- **Reset to Default Configuration** - First-run update check now properly triggers after resetting configuration
  - `GetDefaultSettings()` now explicitly sets `FirstRunUpdateCheckComplete = false`
  - Ensures users see update prompt after config reset, just like on true first run
  - Complements the deferred update safety improvements from v5.0.9

---

## [5.0.9] - 2026-06-04

### Added
- **Deferred Updates During Operations** - Safety improvement to prevent mid-operation shutdowns
  - Updates now check if an operation is currently in progress (`IsProcessing`)
  - If processing (driver injection, image prep, USB creation), update prompt is deferred
  - User is asked if they want to be notified when operation completes
  - After operation finishes, pending update prompt appears automatically
  - Prevents data loss and ensures safe completion of long-running operations
  - Works for both manual update checks and first-run update checks

### Technical
- Added `PendingUpdate` bool and `PendingUpdateVersion` Version? properties to `MainViewModel`
- Added `CheckPendingUpdateAsync()` method to prompt for deferred updates after operations
- Modified `CheckForUpdatesAsync()` to check `IsProcessing` and defer if needed
- Modified `PerformFirstRunUpdateCheckAsync()` to check `IsProcessing` and defer silently
- Wired `CheckPendingUpdateAsync()` into all operation completion points:
  - `InjectDriversAsync` (2 completion points)
  - `CreateUsbFromIsoAsync`
  - `CreateUsbAsync`

---

## [5.0.8] - 2026-06-04

### Added
- **First-Run Update Check** - Automatic version check on initial startup
  - Alerts users if a newer version is available immediately after installation
  - Shows current version vs. latest version
  - User-friendly dialog with "Update Now" or "Later" options
  - Displays estimated update time (~1 minute depending on download speed)
  - Only checks once per installation (uses `FirstRunUpdateCheckComplete` flag)
  - Ensures users who download older published versions can upgrade immediately
  - Non-intrusive: silently skips check if already running latest version

### Technical
- Added `FirstRunUpdateCheckComplete` property to `AppSettings` model
- Added `PerformFirstRunUpdateCheckAsync()` method to `MainViewModel`
- Integrated first-run check into `MainWindow_Loaded` event
- Check runs after cleanup and existing work validation
- Settings automatically persist the check-complete flag

---

## [5.0.7] - 2026-06-04

### Enhanced
- **Auto-Update with Documentation** - Update system now downloads and installs all documentation files
  - Downloads `UserGuide.html`, `README.md`, `CHANGELOG.md`, `ReleaseNotes.txt`, and `AUTOPILOT_MODE.md`
  - Creates/updates `docs\` subfolder next to application EXE
  - Copies README and CHANGELOG to application root directory
  - Provides download progress and success/failure count
  - Continues update even if some documentation downloads fail
  - Users always have up-to-date documentation matching their installed version

### Technical
- Added `DocumentationUrls` dictionary in `UpdateService.cs` with GitHub raw URLs
- Enhanced PowerShell updater script with `Invoke-WebRequest` download logic
- Updater now creates documentation directory structure automatically
- Improved update progress reporting for multi-file downloads

---

## [5.0.6] - 2026-06-04

### Fixed
- **Operation Log Scrollbar** - Scrollbar now visible and working properly
- **Log Collapse Behavior** - Log section properly collapses without leaving empty space
- **Window Resize** - Window now resizes correctly when log is expanded/collapsed
- **Grid Row Definition** - Operation log row now uses `Height="*"` to fill available space
- **ScrollViewer Sizing** - Fixed height (200px) instead of min/max for consistent behavior

---

## [5.0.5] - 2026-06-04

### Added
- **Autopilot Mode** - Dedicated checkbox for Autopilot-enrolled devices
  - Preserves Windows Autopilot OOBE experience and company branding
  - Auto-accepts license agreement
  - Auto-partitions disk (recommended for Autopilot refresh scenarios)
  - Skips local admin account creation (Azure AD accounts only)
  - Keeps wireless setup enabled for Azure AD join
  - Smart UI that hides irrelevant options when Autopilot is enabled
- **Enhanced Privacy Screen Control**
  - Added registry-based privacy experience disabling
  - Properly skips telemetry/diagnostic/speech/inking/location screens
  - Uses both OOBE XML settings AND FirstLogonCommands for reliability
  - Conditional behavior based on Autopilot vs. standard unattended mode
- **Comprehensive Documentation**
  - New `AUTOPILOT_MODE.md` guide with detailed Autopilot scenarios
  - Updated `README.md` with complete Autopilot and unattended installation guide
  - Comparison tables: Autopilot Mode vs. Standard Unattended Installation
  - Troubleshooting section for common deployment issues

### Changed
- Unattended installation dialog now conditionally validates admin username (not required in Autopilot mode)
- OOBE settings generation now respects Autopilot mode separately from SkipOOBE flag
- `ProtectYourPC` setting only applied in non-Autopilot mode

### Fixed
- **CRITICAL**: Privacy/telemetry screens (reporting, location, diagnostics, speech, inking) now properly skipped via registry keys
- Autopilot devices no longer lose company branding and OOBE experience when using unattended installation

---

## [5.0.3] - 2026-06-04

### Fixed
- Operation log UI now properly resizes with window collapse/expand
- No empty space when log section is hidden
- Better experience on smaller screens (Surface devices)

---

## [5.0.2] - 2026-06-04

### Fixed
- Operation log scrollbar auto-adjusts to window height
- Works properly on smaller Surface screens
- Improved layout with auto-sizing behavior

---

## [5.0.1] - 2026-06-04

### Fixed
- Corrected overlapping sections in main window
- Better spacing and margins throughout UI
- Improved visual consistency

---

## [5.0.0] - 2026-06-03

### Changed
- **MAJOR RELEASE**: Redesigned unattended installation for Windows Autopilot compatibility
- Edition selection now only prompts when multiple editions are present in ISO
- OOBE behavior now conditional based on deployment scenario
- Enhanced support for enterprise deployment workflows

---

## [4.0.4] - 2024-01-XX

### Added
- **Close Operation Validation** - Application now warns when closing during active operations
  - Displays confirmation dialog if user tries to close while processing
  - Shows impact of closing (cancel operation, terminate DISM, cleanup mounted images)
  - User can choose to continue working or force close
  - Automatically cancels operations and cleans up DISM processes on forced close
- **CancelOperation method** - Proper cancellation workflow for long-running operations

### Fixed
- **Splash Screen Layout** - Fixed subtitle text being cut off
  - Increased splash screen width to 650px
  - Added MaxWidth constraint to subtitle
  - Reduced font size slightly for better fit

---

## [4.0.3] - 2024-01-XX

### Fixed
- **Operation Log Auto-Scroll** - Fixed operation log to always auto-scroll to the latest entry
  - Previously locked at the top until manually scrolled
  - Now automatically shows the most recent log messages as they appear

### Changed
- Executable renamed to `WinImagePrep4-3.exe` to allow running alongside previous versions

---

## [4.0.2] - 2024-01-XX

### Changed
- Improved UI layout by removing ScrollViewer for cleaner appearance
- Restored operation log to standard height (200px) for better visibility
- Enhanced "Select Apps to Remove" section with vertical layout and better spacing
- Improved button alignment and visual consistency

### Added
- Created CHANGELOG.md for version tracking
- Added menu bar with File, Help, and About sections (coming soon)

---

## [4.0.1] - 2024-01-XX

### Added
- App removal UI moved to dedicated popup dialog for cleaner main window layout
- Live counter showing number of selected apps for removal
- Select All/None functionality in app removal dialog
- Better app selection experience with larger, more informative dialog

### Changed
- Replaced embedded app removal expander with popup button
- Improved main window layout to be less cluttered

---

## [4.0.0] - 2024-01-XX

### Added
- **Windows App Removal Feature** - Remove built-in Windows apps before image creation
  - Predefined list of common apps (Xbox, OneDrive, Cortana, Teams, etc.)
  - Selectable checkbox list for choosing which apps to remove
  - Integration with DISM for provisioned app package removal
- **Windows Edition Selection** - Choose which editions to keep in install.wim
  - Multi-select edition picker dialog
  - Automatic deletion of unselected editions to reduce image size
  - Reduces Windows installation prompts by limiting edition choices
- **Microsoft Teams (Consumer) removal** - Added to predefined app list
- Rebranded as V4 with updated splash screen and window titles
- Single-file standalone executable (WinImagePrepV4.exe)

### Changed
- Updated product name to "Windows Image Preparation Tool V4"
- Enhanced DISM service with app removal and edition deletion capabilities
- Improved main workflow to integrate app removal into install.wim processing

---

## [3.0.0] - 2023-XX-XX

### Added
- **Native WPF Desktop Application** - Complete rewrite from PowerShell to .NET 8 WPF
- MVVM architecture with clean separation of concerns
- Startup splash screen with branded initialization
- Administrator privilege checking with automatic elevation
- Modern professional UI with Material Design inspired controls
- Real-time progress tracking with detailed operation log
- Smart directory management:
  - `C:\WinImagePrep\SavedImages\` - Persistent saved images
  - `C:\WinImagePrep\Logs\` - Application logs
  - `C:\WinImagePrep\Temp\` - Temporary working files (auto-cleanup)
- Enhanced DISM logging with detailed command execution
- Robust ISO mounting with drive verification
- Process management with clean shutdown and resource cleanup
- USB drive detection and validation
- Edition selection dialog for multi-edition Windows ISOs
- Comprehensive error recovery and cleanup

### Changed
- Migrated from PowerShell script to full .NET application
- Improved UI/UX with graphical interface
- Enhanced error handling and logging
- Better resource management and cleanup

---

## [2.0.0] - 2023-XX-XX

### Added
- PowerShell-based automation script
- Basic driver injection into Windows ISO
- ISO to USB creation functionality
- Support for Surface device drivers
- MSI driver package extraction

### Changed
- Improved from manual process to semi-automated PowerShell workflow

---

## [1.0.0] - 2023-XX-XX

### Added
- Initial concept and manual driver injection process
- Basic Windows ISO modification support
- Manual DISM command execution

---

## Version Definitions

### Major Version (X.0.0)
- Complete rewrites or major architectural changes
- Breaking changes to workflow or functionality
- New major features that significantly change the tool's purpose

### Minor Version (x.X.0)
- New features or significant enhancements
- New capabilities that extend functionality
- Non-breaking changes that add value

### Patch Version (x.x.X)
- Bug fixes
- UI/UX improvements
- Documentation updates
- Performance optimizations
- Minor tweaks and adjustments
