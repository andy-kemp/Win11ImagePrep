# WinImagePrep Publishing Guide

## Version 4.1.1 - Standalone EXE Publishing

This document describes how to build and publish WinImagePrep as a standalone single-file executable for distribution.

---

## Prerequisites

- Visual Studio 2022 or later
- .NET 8.0 SDK
- Windows 10/11 development environment
- Administrator rights (for testing DISM operations)

---

## Standalone EXE Publishing

WinImagePrep is **always** published as a standalone single-file EXE. The project file is pre-configured with these settings:

```xml
<PublishSingleFile>true</PublishSingleFile>
<SelfContained>true</SelfContained>
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
<PublishTrimmed>false</PublishTrimmed>
<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
<DebugType>embedded</DebugType>
<EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
```

### Command-Line Publish

From the solution directory, run:

```powershell
dotnet publish WinImagePrep\WinImagePrep.csproj -c Release -r win-x64 -o .\publish
```

This will create:
- `publish\WinImagePrep.exe` - Standalone single-file executable (~100-150 MB)
- `publish\docs\UserGuide.html` - Local user guide
- `publish\docs\ReleaseNotes.txt` - Release notes
- `publish\docs\LICENSE.txt` - Software license

### Visual Studio Publish

1. Open the solution in Visual Studio
2. Right-click the `WinImagePrep` project
3. Select **Publish...**
4. Choose **Folder** as the target
5. Set the target location to `.\publish`
6. Click **Publish**

The standalone EXE and documentation files will be created in the `publish` folder.

---

## Output Files

After publishing, the `publish` folder will contain:

```
publish\
  ├── WinImagePrep.exe          (Standalone single-file EXE, ~100-150 MB)
  └── docs\
	  ├── UserGuide.html        (Comprehensive local user guide)
	  ├── ReleaseNotes.txt      (Version history and release notes)
	  └── LICENSE.txt           (Software license and terms)
```

The EXE is fully self-contained and includes:
- .NET 8.0 runtime
- All application assemblies
- WPF framework components
- Native libraries

**No additional runtime installation is required on target systems.**

---

## Installer Preparation (Inno Setup)

The published output is ready for packaging with Inno Setup or similar installers.

### Recommended Installer Structure

```
[Files]
Source: "publish\WinImagePrep.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\docs\*"; DestDir: "{app}\docs"; Flags: ignoreversion recursesubdirs

[Dirs]
Name: "{commonappdata}\Win11ImagePrep"; Permissions: users-modify

[Icons]
Name: "{autoprograms}\WinImagePrep"; Filename: "{app}\WinImagePrep.exe"
Name: "{autodesktop}\WinImagePrep"; Filename: "{app}\WinImagePrep.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\WinImagePrep.exe"; Description: "Launch WinImagePrep"; Flags: nowait postinstall skipifsilent
```

### Installer Settings

- **Install Location:** `C:\Program Files\WinImagePrep`
- **Settings Folder:** `C:\ProgramData\Win11ImagePrep` (created automatically on first run)
- **Default Working Folder:** `C:\Win11ImagePrep` (created on first run or configurable via Options)
- **Privileges:** Request administrator elevation in installer
- **Shortcuts:** Start Menu (required), Desktop (optional)
- **Uninstall:** Standard Windows uninstall entry
- **Upgrade Behavior:** Preserve `C:\ProgramData\Win11ImagePrep\settings.json` during upgrades

### First-Run Behavior

On first launch, WinImagePrep will:
1. Create `C:\ProgramData\Win11ImagePrep` if missing
2. Create default `settings.json` if not present
3. Show the first-run welcome wizard
4. Create working directories: `C:\Win11ImagePrep\{ExtractedISO, Drivers, Mount, Temp, SavedImages, Logs}`
5. Set `FirstRunComplete` flag in `settings.json`

Subsequent launches will skip the first-run wizard.

---

## Testing the Published Build

### Pre-Release Checklist

1. **Build Verification:**
   - Run `dotnet build -c Release` to ensure no compilation errors
   - Verify version is `4.1.1` in project file and About dialog

2. **Publish Test:**
   - Publish the standalone EXE
   - Verify file size (~100-150 MB is normal for self-contained EXE)
   - Check that `docs` folder contains all three files

3. **Functional Testing:**
   - Copy `publish` folder to a clean test machine (or VM)
   - Run `WinImagePrep.exe` **without** .NET 8 runtime installed
   - Verify first-run wizard appears
   - Test ISO selection and validation
   - Test driver source selection
   - Test app removal selection
   - Test Help menu links (User Guide, Online Documentation, GitHub, Report Issue, Release Notes)
   - Test About dialog (version, working folder, links)
   - Test Tools > Options (storage validation, save/cancel)
   - Verify settings persistence after restart

4. **Administrator Privileges:**
   - Run as standard user (should show warning)
   - Run as administrator (should proceed normally)
   - Test DISM operations (require admin)
   - Test USB creation (requires admin)

5. **Settings & Storage:**
   - Verify `C:\ProgramData\Win11ImagePrep\settings.json` is created
   - Verify `C:\Win11ImagePrep` working folders are created
   - Change working folder via Options, verify derived paths update
   - Test validation for invalid paths (network drives, USB, OneDrive, insufficient space)

6. **Cleanup & Exit:**
   - Verify temporary files are cleaned on exit (if enabled)
   - Verify mounted images are unmounted on exit (if enabled)
   - Test Repair & Cleanup command

---

## Version Management

### Updating Version Numbers

Update the following locations when changing versions:

1. **WinImagePrep.csproj:**
   ```xml
   <Version>4.1.1</Version>
   <AssemblyVersion>4.1.1.0</AssemblyVersion>
   <FileVersion>4.1.1.0</FileVersion>
   ```

2. **AboutDialog.xaml:**
   ```xaml
   <TextBlock Text="Version 4.1.1" .../>
   ```

3. **MainWindow.xaml:**
   ```xaml
   Title="WinImagePrep v4.1.1"
   ```

4. **docs/ReleaseNotes.txt:**
   - Add new version section at the top
   - Document new features, bug fixes, and changes

5. **docs/UserGuide.html:**
   - Update version in footer
   - Update any version-specific instructions

---

## Installer Script Notes (Inno Setup)

Keep installer packaging **separate** from the WPF application codebase. Suggested structure:

```
Win11ImagePrep\
  ├── WinImagePrep\           (WPF application project)
  ├── docs\                   (Documentation files)
  ├── publish\                (Dotnet publish output - gitignored)
  └── installer\              (Inno Setup scripts and assets)
	  ├── WinImagePrep.iss    (Inno Setup script)
	  ├── license.rtf         (Installer license display)
	  └── output\             (Final installer EXE output)
```

Do **not** include Inno Setup scripts or installer logic inside the WPF project.

---

## Distribution Checklist

Before releasing:

- [ ] Version numbers updated in all locations
- [ ] Release notes updated with all changes
- [ ] User guide reflects current UI and workflow
- [ ] Build completes without errors or warnings
- [ ] Standalone EXE tested on clean Windows 11 system
- [ ] All Help menu links verified and working
- [ ] About dialog shows correct version and links
- [ ] First-run wizard tested
- [ ] Settings persistence tested
- [ ] Administrator elevation tested
- [ ] DISM operations tested (ISO extraction, driver injection, app removal)
- [ ] USB creation tested (if hardware available)
- [ ] Documentation files included in publish output
- [ ] Installer (if created) tested on clean system
- [ ] Upgrade path tested (settings preserved)

---

## Support & Contact

For questions about publishing or distribution:

- Website: https://tools.andykemp.com
- GitHub: https://github.com/andy-kemp/Win11ImagePrep
- Issues: https://github.com/andy-kemp/Win11ImagePrep/issues

---

© 2025 Andy Kemp Consulting Ltd. All rights reserved.
