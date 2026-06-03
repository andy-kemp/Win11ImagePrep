# WinImagePrep Version 4.2.1 - Release Summary

## Version Information
- **Version**: 4.2.1
- **Release Date**: January 2025
- **Build Type**: Standalone single-file EXE (win-x64)
- **Publisher**: Andy Kemp Consulting Ltd

## What's New in 4.2.1

### 1. Startup & First-Run Improvements
- **Fixed first-run window display issues**
  - Corrected `ShutdownMode` from `OnMainWindowClose` to `OnExplicitShutdown`
  - Fixed window ownership and activation problems
  - Changed from `ShowDialog()` to `Show()` with proper window lifecycle management
  - Added `UserAccepted` property to track first-run completion

- **Enhanced startup sequence**
  - Improved splash screen timing
  - Better error handling and logging throughout startup
  - Comprehensive diagnostic messages for troubleshooting

### 2. Default Storage Location Changes
- **New default working folder**: `C:\ProgramData\Win11ImagePrep` (was `C:\Win11ImagePrep`)
- **Rationale**:
  - Aligns with Windows best practices for application data
  - Settings and working files now in consistent parent directory
  - Settings file location: `C:\ProgramData\Win11ImagePrep\settings.json`
  - Working data location: `C:\ProgramData\Win11ImagePrep\*`

- **Updated documentation**:
  - `docs\UserGuide.html` reflects new default paths
  - All references updated throughout the application

### 3. Reset & Troubleshooting Features
- **Reset to Defaults** (existing, kept)
  - Resets settings to default values in the UI
  - Changes can be undone by clicking Cancel without saving
  - Does not delete settings file

- **Reset Everything** (NEW)
  - Deletes `C:\ProgramData\Win11ImagePrep\settings.json`
  - Restores first-run state
  - Application closes after reset
  - First-run wizard appears on next start
  - **Purpose**: Troubleshooting tool for users experiencing issues

### 4. Documentation Updates
- **Release notes updated** (`docs\ReleaseNotes.txt`)
  - Version 4.2.1 changes documented at the top
  - Version 4.1.1 moved to "Previous Release" section
  - Comprehensive change log maintained

- **User guide updated**
  - Default paths updated to `C:\ProgramData\Win11ImagePrep`
  - Requirements section reflects new location
  - Troubleshooting section updated

## Files Changed

### Version Metadata
- `WinImagePrep\WinImagePrep.csproj` - Version updated to 4.2.1
- `WinImagePrep\MainWindow.xaml` - Title updated to "WinImagePrep v4.2.1"
- `WinImagePrep\AboutDialog.xaml` - Version display updated to 4.2.1

### Core Application Changes
- `WinImagePrep\App.xaml` - ShutdownMode changed to `OnExplicitShutdown`
- `WinImagePrep\App.xaml.cs` - First-run flow rewritten with Show() and TaskCompletionSource
- `WinImagePrep\FirstRunWindow.xaml.cs` - Added UserAccepted property, removed DialogResult usage
- `WinImagePrep\Models\AppSettings.cs` - Default WorkingRoot changed to `C:\ProgramData\Win11ImagePrep`

### UI & Features
- `WinImagePrep\OptionsWindow.xaml` - Added "Reset Everything..." button
- `WinImagePrep\ViewModels\OptionsViewModel.cs` - Added ResetEverythingCommand implementation

### Documentation
- `docs\ReleaseNotes.txt` - Version 4.2.1 section added
- `docs\UserGuide.html` - Default paths updated (3 locations)

## Testing Checklist

### First-Run Experience
- [ ] Delete settings file: `Remove-Item "C:\ProgramData\Win11ImagePrep\settings.json"`
- [ ] Run `.\publish\WinImagePrep.exe`
- [ ] Verify splash screen appears
- [ ] Verify first-run window appears and stays visible
- [ ] Click "Continue" - should proceed to main window
- [ ] Verify settings file created at `C:\ProgramData\Win11ImagePrep\settings.json`
- [ ] Verify working folder created at `C:\ProgramData\Win11ImagePrep\`

### Reset Everything Feature
- [ ] Open WinImagePrep
- [ ] Go to Tools > Options
- [ ] Click "Reset Everything..."
- [ ] Confirm warning dialog
- [ ] Verify application closes
- [ ] Restart application
- [ ] Verify first-run wizard appears again

### Version Display
- [ ] Main window title shows "WinImagePrep v4.2.1"
- [ ] Help > About shows "Version 4.2.1"
- [ ] EXE properties show FileVersion 4.2.1.0

### Documentation
- [ ] Help > User Guide opens and shows `C:\ProgramData\Win11ImagePrep` as default
- [ ] Help > Release Notes shows version 4.2.1 at the top

## Build Commands

```powershell
# Build
dotnet build WinImagePrep\WinImagePrep.csproj -c Release

# Publish standalone EXE
dotnet publish WinImagePrep\WinImagePrep.csproj -c Release -r win-x64 -o .\publish

# Verify version
(Get-Item ".\publish\WinImagePrep.exe").VersionInfo | Select-Object FileVersion, ProductVersion, ProductName
```

## Deployment Notes

### For Inno Setup Installer
1. Update installer script version to 4.2.1
2. Ensure all docs are included:
   - `docs\UserGuide.html`
   - `docs\ReleaseNotes.txt`
   - `docs\LICENSE.txt`
3. Default installation directory can remain `C:\Program Files\WinImagePrep`
4. Application data will be stored in `C:\ProgramData\Win11ImagePrep`

### User Communication
- Users upgrading from 4.1.1 should be informed:
  - Settings location remains the same (`C:\ProgramData\Win11ImagePrep\settings.json`)
  - New installs will use `C:\ProgramData\Win11ImagePrep` as the default working folder
  - Existing working folders will be preserved (not automatically migrated)
  - Users can change working folder via Tools > Options if desired

## Known Issues & Future Improvements
- None identified in 4.2.1
- First-run experience now stable and reliable
- Reset functionality provides good troubleshooting path

## Support Resources
- Documentation: https://tools.andykemp.com/winimageprep
- GitHub: https://github.com/andy-kemp/Win11ImagePrep
- Issues: https://github.com/andy-kemp/Win11ImagePrep/issues
