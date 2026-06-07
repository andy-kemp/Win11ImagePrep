# Changelog

All notable changes to **WinImagePrep** by Andy Kemp Consulting Ltd will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [5.4.4] - 2026-01-25

### Fixed
- 🔧 **Update dialog now shows full message**: Increased dialog height from 280px to 350px
  - Version numbers (current and latest) now fully visible
  - Complete update instructions displayed
  - Better user experience with clear information

### Technical
- Dialog height increased to accommodate full multi-line update message
- ScrollViewer properly displays all content without truncation

## [5.4.3] - 2026-01-25

### Added
- 🔍 **Debug logging for update flow**: Added detailed operation log messages
  - "📋 Showing update dialog..." message
  - "🔄 Waiting for user response..." message
  - "✓ Dialog result" message with UpdateNow/DialogResult values
  - "📋 Dialog complete" message with final updateNow/dontAskAgain values
  - Helps diagnose update dialog behavior issues

### Technical
- Enhanced logging throughout update prompt workflow
- Better visibility into update dialog state transitions

## [5.4.2] - 2026-01-25

### Fixed
- 🔧 **CRITICAL: Update Now button now actually updates**: Fixed async dispatcher bug
  - Changed from `InvokeAsync(async () =>)` to `Invoke(() =>)`
  - Removed unnecessary `await Task.Delay(500)` inside dialog invocation
  - Code now properly waits for user to click button before continuing
  - Previously, code continued immediately and `updateNow` remained false
  - "Update postponed" message no longer appears when clicking "Update Now"

### Technical
- Synchronous `Dispatcher.Invoke` ensures dialog completion before proceeding
- Lambda no longer async, preventing race condition
- User choice properly captured before update logic executes

## [5.4.1] - 2026-01-25

### Changed
- 🧪 Test release to verify automatic updates work correctly from both old and new installations
  - Validates update URL changes from v5.4.0
  - Confirms GitHub raw content downloads work
  - Tests backward compatibility with older versions

## [5.4.0] - 2026-01-25

### Fixed
- 🔧 **CRITICAL: Update download URLs fixed**: Changed from GitHub Releases to raw GitHub content
  - Old URL: `https://github.com/andy-kemp/Win11ImagePrep/releases/latest/download/WinImagePrep.exe`
  - New URL: `https://raw.githubusercontent.com/andy-kemp/Win11ImagePrep/main/publish/WinImagePrep.exe`
  - Old URL: `https://github.com/andy-kemp/Win11ImagePrep/releases/latest/download/WinImagePrep.Updater.exe`
  - New URL: `https://raw.githubusercontent.com/andy-kemp/Win11ImagePrep/main/publish/WinImagePrep.Updater.exe`
  - Eliminates 404 errors from missing GitHub releases
  - All versions can now download updates successfully

### Added
- 📦 **GitHub Release v5.4.0 created**: Provides backward compatibility for older installations
  - Contains `WinImagePrep.exe` and `WinImagePrep.Updater.exe`
  - Allows versions still using old URLs to find update files
  - Bridge for smooth transition to new download mechanism

### Changed
- 📝 **Startup update message improved**: Removed confusing "Welcome to WinImagePrep!" text
  - Now clearly shows: "A new version of WinImagePrep is available!"
  - Displays current version: "Current version: v5.X.X"
  - Displays latest version: "Latest version: v5.X.X"
  - More professional and informative update notification

### Technical
- UpdateService URLs point to raw GitHub content instead of release artifacts
- Version check URL unchanged: `https://raw.githubusercontent.com/andy-kemp/Win11ImagePrep/main/version.json`
- Hybrid approach: new versions use raw files, old versions can still use release

## [5.0.44] - 2025-01-29

### Fixed
- 🔧 **App restart after update now works**: Fixed batch script execution
  - Changed to `UseShellExecute = true` for proper batch script execution
  - Added `cd /d` command to ensure correct working directory
  - Added echo statements for debugging
  - Changed window style to Minimized (was Hidden) for troubleshooting
  - Batch script now properly launches the main app after updater replacement

### Technical
- Batch script process launch now uses shell execute instead of direct cmd.exe invocation
- Working directory is explicitly set with `cd /d` before starting application

## [5.0.43] - 2025-01-29

### Fixed
- ✅ **First-run update now works**: Fixed first-run window to download and apply updates immediately
  - Previously deferred update to main window which didn't trigger properly
  - Now downloads and applies update right from the welcome screen
  - Application properly shuts down after update initiated
- ✅ **App restarts after update**: Fixed batch script that restarts main application
  - Changed from direct batch execution to `cmd.exe /c` for reliable execution
  - Increased timeout to 3 seconds for safer file replacement
  - Added explicit `exit` command for proper cleanup
- ✅ **Smart updater downloads**: Updater only downloads when newer version available
  - Checks local updater version using `FileVersionInfo`
  - Compares against remote updater version from `version.json`
  - Skips 68MB download if updater is already current
  - Added `updaterVersion` field to `version.json` for version tracking

### Technical
- Improved batch script reliability using cmd.exe wrapper
- Enhanced version checking with local file version inspection
- Fixed first-run shutdown flow to work correctly with updater handoff

## [5.0.42] - 2025-01-29

### Fixed
- 🔧 **Critical updater fix**: Changed JSON handoff location from user TEMP to `C:\ProgramData`
  - Resolved issue where elevated updater couldn't access user's temp folder
  - Update process now works reliably with UAC elevation
  - Fixed updater logging to maintain consistent log file path
  - Removed broken `ContinueOnStartup` method from updater

### Changed
- 🧪 Test release for complete update validation v5.0.41 → v5.0.42

## [5.0.41] - 2025-01-29

### Changed
- 🧪 Test release for final update validation:
  - Testing v5.0.40 → v5.0.41 update path
  - Verifying JSON-based argument passing works correctly
  - Confirming UAC elevation and admin rights handling
  - Validating updater window displays and completes successfully

## [5.0.40] - 2025-01-29

### Changed
- 🧪 Test release to validate:
  - JSON-based updater argument passing
  - Admin elevation with UAC prompt
  - Complete update flow from v5.0.39 → v5.0.40
  - First-run and manual update paths

## [5.0.39] - 2025-01-29

### Fixed
- 🔧 **Major updater architecture change**:
  - Arguments now passed via JSON file instead of command-line
  - Fixes UAC elevation argument mangling/loss
  - Updater reads from `%TEMP%\WinImagePrep_UpdateInfo.json`
  - Simpler, more reliable argument passing through admin elevation
- 🔧 Updater version bumped to 1.6.0

## [5.0.38] - 2025-01-29

### Fixed
- 🔧 Added deep crash diagnostics to updater:
  - Static constructor logging (runs before anything)
  - Custom Main() method with full exception handling
  - Logs to WinImagePrep_Updater_MAIN.log and WinImagePrep_Updater_CRASH.log
  - Captures crashes at every initialization stage
- 🔧 Updater version bumped to 1.5.0

## [5.0.37] - 2025-01-29

### Changed
- 🧪 Test release to validate:
  - Updater error handling and UAC prompt detection
  - First-run update detection with aggressive no-cache headers
  - Complete update flow from v5.0.36 → v5.0.37

## [5.0.36] - 2025-01-29

### Fixed
- 🔧 Improved updater error handling:
  - Detects when user declines UAC prompt (error 1223)
  - Shows clear dialog explaining admin rights requirement
  - Updater now logs ALL command-line arguments for debugging
  - Updater keeps window open on error instead of silently exiting
- 🔧 Updater version bumped to 1.4.0

## [5.0.35] - 2025-01-29

### Fixed
- 🔧 Added aggressive HTTP cache-busting headers to update check:
  - Cache-Control: no-cache, no-store, max-age=0
  - Pragma: no-cache
- 🔧 Fixes issue where some versions couldn't detect new updates due to HTTP caching

## [5.0.34] - 2025-01-29

### Changed
- 🧪 TEST: Version bump to validate complete update flow with admin elevation

## [5.0.33] - 2025-01-29

### Fixed
- 🔧 Updater now explicitly requests admin elevation (Verb = "runas") to ensure it can replace EXE files
- 🔧 Fixes issue where updater would download but not run when main app was running as admin

## [5.0.32] - 2025-01-29

### Changed
- 🧪 TEST: Version bump to validate auto-download updater functionality

## [5.0.31] - 2025-01-29

### Added
- ✨ Main app now automatically downloads the latest updater from GitHub before launching it
- ✨ Shows progress while downloading updater (e.g., "Downloading updater: 12.5 MB / 64.7 MB (19%)")
- ✨ No more manual updater downloads required - always uses the latest version

### Changed
- 🔄 Update process now: download updater → launch updater → updater downloads main app → install → relaunch

## [5.0.30] - 2025-01-29

### Fixed
- 🔧 Added global exception handlers to updater - will now ALWAYS create log files even if crashes
- 🔧 Moved logging to App.xaml.cs to catch constructor and InitializeComponent errors

## [5.0.29] - 2025-01-29

### Fixed
- 🔧 Added comprehensive logging to updater for debugging update issues
- 🔧 Updater now logs to %TEMP%\WinImagePrep_Updater_*.log for troubleshooting

## [5.0.28] - 2025-01-29

### Changed
- 🧪 TEST: Version bump to test v5.0.26 update detection without cache-busting

## [5.0.27] - 2025-01-29

### Changed
- 🧪 TEST: Version bump to validate cache-busting update detection fix

## [5.0.26] - 2025-01-29

### Fixed
- **Update Check Caching** - Added timestamp query parameter to version check URL
  - Prevents HttpClient from using cached version.json responses
  - Ensures update checks always retrieve the latest version information
  - Fixes issue where v5.0.24 could not detect v5.0.25

---

## [5.0.25] - 2025-01-29

### Changed
- **Test Release** - Version bump only to test updater self-update
- No functional changes from v5.0.24

---

## [5.0.24] - 2025-01-29

### Fixed
- **Updater Self-Update** - Updater now downloads and replaces itself during updates
  - Downloads both WinImagePrep.exe and WinImagePrep.Updater.exe from GitHub
  - Ensures users always get the latest updater improvements
  - Progress bar shows download progress for both files (20-50% main, 50-70% updater)
  - Updater version bumped to 1.1.0

### Changed
- Updater now accepts progress range parameters for better visual feedback
- All EXE files in the publish directory are now synchronized during updates

---

## [5.0.23] - 2025-01-29

### Changed
- **Test Release** - Version bump only to test new updater application
- No functional changes from v5.0.22

---

## [5.0.22] - 2025-01-29

### Added
- **New Dedicated Updater Application** - Replaced fragile PowerShell script with standalone WinImagePrep.Updater.exe
  - Modern WPF interface with visible progress bar and status messages
  - More reliable process waiting and file replacement
  - Better error handling and user feedback
  - Smaller and faster than PowerShell-based approach
  - No more "process still running" timing issues

### Changed
- UpdateService now launches WinImagePrep.Updater.exe instead of creating PowerShell scripts
- Build process now includes updater compilation via build-with-updater.ps1
- Updater automatically waits for app to close, downloads update, replaces EXE, and relaunches

### Removed
- PowerShell-based updater script generation (replaced by dedicated executable)

---

## [5.0.21] - 2025-01-29

### Fixed
- **First-Run Update Process** - Deferred update until main window loads
  - Update check still happens in first-run wizard
  - If user accepts, update is flagged and downloaded after main app loads
  - Prevents "process still running" errors from wizard window not closing cleanly
  - Update progress now visible in operation log
  - Added `PendingUpdateVersion` property to AppSettings

### Changed
- First-run wizard now only checks for updates and prompts user
- Actual download/apply happens after main window loads via `CheckPendingFirstRunUpdateAsync()`

---

## [5.0.20] - 2025-01-29

### Fixed
- **Updater Timing** - Increased initial delay from 3 to 5 seconds
  - Fixes "process still running" error when updating immediately after a previous update
  - Gives application more time to fully shut down before file replacement
  - Combined with 2-second post-launch delay = 7 seconds total safety window

---

## [5.0.19] - 2025-01-29

### Changed
- **Test Release** - No functional changes
- Version bump to test v5.0.18 first-run update check
- Validates simplified first-run update flow

---

## [5.0.18] - 2025-01-29

### Fixed
- **Simplified First-Run Update Check** - Complete redesign for reliability
  - Update check now happens immediately when clicking Continue in first-run wizard
  - Removed complex flag-based logic that was causing issues
  - No more `FirstRunUpdateCheckComplete` flag - just check every time
  - If update available, prompts immediately; if not, silently continues
  - Added detailed debug logging to update service

### Changed
- Removed `PerformFirstRunUpdateCheckAsync()` from MainViewModel (no longer needed)
- Update check now integrated directly into FirstRunWindow Continue button
- Cleaner, more predictable behavior

---

## [5.0.17] - 2025-01-29

### Changed
- **Test Release** - No functional changes
- Version bump to test v5.0.16 first-run update check fix
- Validates that update check triggers after settings reset

---

## [5.0.16] - 2025-01-29

### Fixed
- **First-Run Update Check After Reset** - Fixed update check not running after settings reset
  - Settings are now reloaded from disk after first-run wizard completes
  - Ensures `FirstRunUpdateCheckComplete` flag is properly read
  - Added debug logging to track first-run update check status
- **Updater Process Detection** - Improved "process still running" error handling
  - Now waits for specific process ID instead of searching by name
  - Added 3-second initial delay to allow app to begin shutdown
  - Better error handling if process lookup fails

### Technical
- Modified `App.xaml.cs` to call `ReloadSettingsAsync()` after first-run wizard closes
- Modified `CreateUpdaterScript()` to capture and wait for current process ID
- Added diagnostic logging in `PerformFirstRunUpdateCheckAsync()`

---

## [5.0.15] - 2025-01-28

### Changed
- **Test Release** - No functional changes
- Version bump to test v5.0.14 updater improvements
- Confirms updater works reliably from 5.0.14 → 5.0.15

---

## [5.0.14] - 2025-01-28

### Fixed
- **Critical Updater Fix** - Fixed PowerShell updater window flashing and cancelling
  - Now downloads EXE and documentation BEFORE launching PowerShell script
  - Increased delay to 2 seconds before app shutdown to give PowerShell time to start
  - Removed complex web download logic from PowerShell script
  - Script now only handles: wait for process, backup, copy files, launch
  - Much more reliable update process, especially when running as admin

### Technical
- Modified `UpdateService.DownloadAndApplyUpdateAsync()` to pre-download all files
- Modified `CreateUpdaterScript()` to accept pre-downloaded docs path
- Script now receives all file paths as parameters instead of downloading during execution
- Fixed timing issue where app closed before PowerShell script could initialize

---

## [5.0.13] - 2026-06-05

### Fixed
- **Critical Autopilot Enrollment Fix** - Fixed unattended XML interfering with proper Autopilot enrollment
  - Removed `HideEULAPage`, `NetworkLocation`, and `ProtectYourPC` settings in Autopilot mode
  - Removed ALL `FirstLogonCommands` registry tweaks in Autopilot mode (especially `DisablePrivacyExperience`)
  - Autopilot now properly displays company logo and enrollment screens without manual prompts
  - Non-Autopilot unattended installs still skip all privacy/setup screens as before

### Technical
- Modified `UnattendedInstallService.BuildAutounattendXml()` to skip OOBE suppression in Autopilot mode
- Autopilot enrollment requires clean OOBE - answer file now only handles disk partitioning and locale settings
- Standard unattended mode unchanged - still suppresses all privacy/setup prompts

---

## [5.0.12] - 2026-06-05

### Fixed
- **Unattended Install Locale Prompts** - Fixed keyboard and regional prompts appearing during Autopilot/unattended installations
  - Added `UILanguageFallback` setting to both windowsPE and oobeSystem passes
  - Ensures Windows doesn't prompt for keyboard layout or regional settings when booting from USB
  - Maintains proper locale configuration for en-GB and other regional ISOs

### Technical
- Modified `UnattendedInstallService.BuildAutounattendXml()` to include UILanguageFallback
- Added comment clarifying international settings suppress keyboard/regional prompts

---

## [5.0.11] - 2026-06-04

### Fixed
- **Updater Script Improvements** - Fixed silent update failures when running as administrator
  - Added comprehensive error handling and logging to PowerShell updater script
  - Added file existence validation before attempting update
  - Improved error messages showing exact failure reasons
  - Added automatic force-close if app doesn't exit gracefully within 30 seconds
  - Added GC calls to release file locks before overwriting EXE
  - Extended final message display so users can see completion status
  - Better UAC cancellation detection and error reporting

### Technical
- Modified `UpdateService.CreateUpdaterScript()` with enhanced error handling
- Added `$ErrorActionPreference = 'Continue'` to PowerShell script
- Added try-catch blocks around all file operations
- Added file size display and validation steps
- Improved process termination logic with force-close fallback

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
