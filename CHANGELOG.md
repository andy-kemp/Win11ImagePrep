# Changelog

All notable changes to the Windows Image Preparation Tool will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
