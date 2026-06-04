# Windows 11 Image & USB Creator

A professional WPF application for preparing Windows 11 installation images with injected drivers, dynamic app discovery and removal, and creating bootable USB drives (UEFI-compatible, FAT32, 14GB+). This tool is especially useful for creating custom Windows 11 USB installers with integrated drivers from MSI packages and streamlined Windows app configurations, specifically designed for Microsoft Surface devices and other hardware requiring driver slipstreaming.

![Version](https://img.shields.io/badge/version-4.4.7-blue.svg)
![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)
![License](https://img.shields.io/badge/license-Proprietary-orange.svg)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue.svg)

---

## 🎯 Overview

Similar in concept to Rufus, but specialized for driver integration before deployment. This native Windows application streamlines the process of injecting hardware drivers (primarily Surface drivers from MSI packages) into Windows 11 ISO images, creating bootable USB drives with fully integrated drivers.

### Key Benefits
- **Native Windows Application**: Full .NET 8 WPF application with modern UI
- **Dynamic App Discovery**: Scan ISO to discover all ~47 provisioned apps actually present in your image
- **Automated Workflow**: Intelligent prompts guide you through app removal process
- **One-time setup**: Create a single USB with all drivers pre-installed and unwanted apps removed
- **Zero-touch installation**: No need to install drivers or remove apps post-Windows setup
- **Time-saving**: Eliminates manual driver installation and app removal on multiple devices
- **UEFI compatible**: Creates FAT32 bootable USB drives with proper partitioning
- **Edition control**: Choose which Windows editions to include and suppress installation prompts
- **Graphical interface**: Professional WPF UI with real-time progress tracking
- **Splash screen**: Branded startup with initialization status

---

## ✨ Features

### Version 4.4.7 (Latest) - June 2026
- 🐛 **Fixed: First-Run Loop Bug**
  - Corrected settings persistence pattern to properly save FirstRunComplete flag
  - Fixed settings save/clone behavior to ensure all preferences persist across restarts
  - App no longer re-enters first-run wizard after completing it
  - Settings now properly saved when modifying RemoveWindowsApps and app selections

### Version 4.4.6 - June 2026
- ✨ **NEW: Persistent App Selection Settings**
  - App removal checkbox state remembered across sessions
  - Selected apps for removal automatically restored on startup
  - Settings saved to `C:\ProgramData\Win11ImagePrep\settings.json`
  - No need to re-select your apps every time you open the application
  - Works seamlessly with the auto-load feature

### Version 4.4.5 - June 2026
- 🐛 **Fixed: Version Display Format**
  - All version displays now show clean 3-part format (e.g., "4.4.5" instead of "4.4.5.0")
  - Consistent version formatting in About dialog, update checker, and logs
  - Improved readability and professional appearance

### Version 4.4.4 - June 2026
- ✨ **Streamlined App Removal UX**
  - Apps auto-load from GitHub when "Remove Windows Apps" checkbox is checked
  - Removed manual "Load Apps" and "Scan from ISO" buttons for cleaner interface
  - Selection panel greys out when checkbox is unchecked for clear visual feedback
  - Status text shows loading progress: "Loading apps..." → "✓ 79 apps loaded"
  - "Select Apps to Remove" button only enabled when apps are successfully loaded
  - Simpler, more intuitive workflow with fewer manual steps

### Version 4.4.3 - June 2026
- ✨ **Quick Select Presets for App Removal**
  - "Remove Bloatware Only" - Removes social/gaming apps, keeps Calculator, Notepad, Paint, Photos, Snipping Tool, etc.
  - "Remove All Optional Apps" - Removes everything including useful utilities
  - "Clear Selection" - Quick deselect all
  - Smart categorization preserves Windows essentials while removing fluff

### Version 4.4.2 - June 2026
- ✨ **NEW: Auto-Update System**
  - Check for updates via Tools menu
  - Automatic download and installation from GitHub
  - Elevated updater with backup and rollback
  - Seamless version tracking with version.json

- ✨ **NEW: Grouped Multi-Architecture App Support**
  - Apps grouped by display name across x64 and ARM64
  - One Teams entry removes correct architecture-specific packages
  - Smart removal: only removes packages present in current image
  - Merged app list with 79 packages (47 x64 + 45 ARM64)

- ✨ **GitHub-Hosted App List**
  - Runtime app list loaded from GitHub
  - Cache fallback for offline scenarios
  - Easy maintainer updates via ISO scan scripts
  - Supports both x64 and ARM64 Windows images

### Version 4.3.0 - January 2025
- ✨ **Dynamic App Discovery from ISO**
  - Scan install.wim to discover all ~47 provisioned apps actually in your image
  - Handles package name variants correctly (MSTeams vs MicrosoftTeams)
  - Works with all Windows 11 editions and builds
  - Full package name resolution for reliable removal

- ✨ **Automated App Loading Workflow**
  - Intelligent auto-prompt when "Remove Windows apps" is enabled
  - Automatically offers to scan ISO and select apps
  - No more manual steps - just check the box and go!
  - Graceful fallback to default app list if needed

- ✨ **Enhanced App Removal**
  - Dual-mode removal: Full package names (fast) or pattern matching (legacy)
  - Created ProvisionedApp model with DisplayName, PackageName, Version, PublisherId
  - Green "Load Apps from ISO" button for manual discovery
  - Backward compatible with existing configurations

### Version 4.2.1 - Startup & Settings Improvements
- ✅ **Startup & First-Run Improvements**
  - Fixed first-run window display and activation
  - Improved splash screen timing and lifecycle
  - Enhanced wizard visibility

- ✅ **Storage & Path Updates**
  - Changed default working folder to `C:\ProgramData\Win11ImagePrep`
  - Centralized application data storage
  - Updated documentation

- ✅ **Reset & Troubleshooting**
  - "Reset to Defaults" functionality
  - Settings reset for troubleshooting
  - Better error handling during startup

### Core Features
- ✅ **Windows App Removal** - Remove built-in Windows apps before image creation
  - Dynamic app discovery from ISO (NEW in 4.3.0)
  - Popup dialog with selectable app list (Xbox, OneDrive, Cortana, Teams, etc.)
  - Live counter showing selected apps
  - Select All/None functionality
  - Integration with DISM for provisioned app package removal

- ✅ **Windows Edition Control** - Choose which editions to keep in install.wim
  - Multi-select edition picker dialog
  - Automatic deletion of unselected editions
  - Reduces Windows installation prompts by limiting edition choices

- ✅ **Native Windows Application** - Full .NET 8 WPF desktop application
- ✅ **MVVM Architecture** - Clean separation of concerns with ViewModels and Services
- ✅ **Startup Splash Screen** - Branded splash screen during application initialization
- ✅ **Administrator Privilege Checking** - Automatic detection and restart with elevation
- ✅ **Modern UI** - Professional WPF interface with Material Design inspired controls
- ✅ **Real-time Progress Tracking** - Live operation log with detailed status updates
- ✅ **Smart Directory Management** - Persistent and temporary file locations:
  - `C:\ProgramData\Win11ImagePrep\` - Application settings and configuration
  - `C:\ProgramData\Win11ImagePrep\Temp\` - Temporary working files (auto-cleanup)
- ✅ **Enhanced DISM Logging** - Detailed command execution and error reporting
- ✅ **Robust ISO Mounting** - Persistent ISO mount with drive verification
- ✅ **Process Management** - Clean shutdown with proper resource cleanup
- ✅ **USB Drive Detection** - Automatic USB drive enumeration and validation
- ✅ **Error Recovery** - Comprehensive error handling and cleanup
- ✅ **Single-file Executable** - Standalone WinImagePrep.exe with no dependencies

### Core Functionality
- **Multiple injection points**: Drivers injected into:
  - WinPE (Windows Preinstallation Environment)
  - Windows Setup Environment
  - Windows Recovery Environment (WinRE)
  - All Windows 11 editions in install.wim
- **Windows customization**:
  - Dynamically discover and remove provisioned Windows apps
  - Select which Windows editions to include (Pro, Enterprise, etc.)
  - Streamline installation by removing unused editions
- **Four creation modes**:
  1. **Prepare Image with Drivers** - Full driver injection + app removal workflow
  2. **Create USB from ISO** - Direct USB creation from original ISO
  3. **Create USB** - Create bootable USB from prepared image
  4. **From Saved Image** - Load and use previously prepared images
- **Repair/Cleanup** - Utility to clean mounted images and temporary files

---

## 📋 System Requirements

### Operating System
- Windows 10 (20H2 or later)
- Windows 11 (any version)
- **Administrator privileges required** (application will prompt for elevation)

### Software Dependencies
- .NET 8.0 Runtime (Desktop) - bundled with application
- DISM (Deployment Image Servicing and Management) - built into Windows
- PowerShell (for ISO mounting operations)

### Hardware Requirements
- **Disk Space**: Minimum 25GB free space on C: drive
  - ~5GB for extracted ISO files
  - ~2GB for extracted drivers
  - ~15GB for mounted WIM images during processing
  - ~3GB buffer for temporary operations
- **RAM**: 8GB+ recommended (DISM operations are memory-intensive)
- **USB Drive**: 14GB+ for bootable USB creation (FAT32 formatted)
- **Processor**: Modern multi-core CPU recommended (driver injection is CPU-intensive)

### Input Files
- **Windows 11 ISO image** - Official Microsoft Windows 11 ISO (Business or Consumer editions)
- **Driver MSI package** - Surface drivers or other MSI-packaged drivers (e.g., Surface_Laptop_WiFi_drivers.msi)

---

## 📥 Download & Installation

### Option 1: Download Pre-built Release (Recommended)

Download the latest **self-contained executable** from the [Releases](https://github.com/andy-kemp/Win11ImagePrep/releases) page:

- **`WinImagePrep_full.exe`** (72 MB) - Single file, includes .NET 8 runtime
  - ✅ No installation required
  - ✅ No dependencies needed
  - ✅ Just download and run
  - ✅ Works on any Windows 10/11 x64 machine

### Option 2: Build from Source

1. **Prerequisites**:
   - Visual Studio 2022+ or .NET 8 SDK
   - Windows 10/11 with DISM and PowerShell

2. **Clone the repository**:
   ```bash
   git clone https://github.com/andy-kemp/Win11ImagePrep.git
   cd Win11ImagePrep
   ```

3. **Build the application**:

   **Standard build** (requires .NET 8 Runtime on target):
   ```powershell
   cd WinImagePrep
   dotnet build --configuration Release
   # Output: bin\Release\net8.0-windows\WinImagePrep.exe
   ```

   **Self-contained single-file build** (no runtime needed):
   ```powershell
   dotnet publish WinImagePrep/WinImagePrep.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=embedded -o "WinImagePrep/bin/Publish"
   # Output: WinImagePrep\bin\Publish\WinImagePrep.exe (rename to WinImagePrep_full.exe)
   ```

---

## 🚀 Usage

### Quick Start

1. **Run as Administrator**
   - Right-click `WinImagePrep_full.exe` → **Run as Administrator**
   - Or double-click and the application will prompt you to restart with elevation

2. **Select Windows 11 ISO**
   - Click "Browse..." next to "1. Select Windows ISO"
   - Select your Windows 11 ISO file
   - Optional: Click "Verify" to validate ISO integrity

3. **Select Driver MSI**
   - Click "Browse..." next to "2. Select Driver MSI"
   - Select your Surface drivers MSI file

4. **Configure Options** (Optional)
   - Click "Select Specific Windows Editions" to choose which editions to process
   - Default: All editions will be processed

5. **Start Processing**
   - Click "Prepare Image with Drivers"
   - Application will check for admin privileges and prompt if needed
   - Monitor real-time progress in the Operation Log
   - Process takes 30-60 minutes depending on hardware

6. **Create Bootable USB**
   - After image preparation completes
   - Insert USB drive (14GB+ required)
   - Click "Refresh" to detect USB drive
   - Select USB drive from "3. Select USB Drive" dropdown
   - Click "Create USB"
   - Confirm data loss warning
   - Wait for USB creation to complete

### Alternative Workflows

#### Create USB from Saved Image
1. Previously prepared images are saved in `C:\WinImagePrep\SavedImages\`
2. Click "From Saved Image" button
3. Select saved image from dialog
4. Select USB drive
5. Click "Create USB" - much faster than full preparation!

#### Repair/Cleanup
- Click "Repair/Cleanup" button to:
  - Dismount any stuck WIM images
  - Clean up temporary files in `C:\WinImagePrep\Temp\`
  - Resolve DISM mount errors

---

## 📂 Directory Structure

```
C:\WinImagePrep\
│
├── WinImagePrep.exe          # Main application executable
├── app.ico                   # Application icon
├── README.md                 # This file
│
├── SavedImages\              # Persistent: Saved prepared images
│   └── [ImageName].wim files
│
├── Logs\                     # Persistent: Application logs
│   └── WinImagePrep_[date].log
│
├── Config\                   # Persistent: Configuration files
│
└── Temp\                     # Temporary: Auto-cleanup working directory
    ├── Windows11\            # Extracted ISO contents
    ├── Drivers\              # Extracted MSI drivers
    └── Mount\                # WIM mount points
        ├── PE\               # WinPE mount
        ├── Setup\            # Setup mount
        └── Edition_[N]\      # Edition-specific mounts
```

**Note**: The `Temp\` folder is automatically cleaned during operations and on startup. SavedImages, Logs, and Config are preserved.
├── Drivers\                  # Extracted drivers from MSI (temporary)
├── Mount\                    # Temporary mount root for WIM files (WinPE, WinSetup, Edition_*, WinRE_*)
├── Config\                   # Configuration storage (iso-label.txt)
├── SavedImages\              # User-saved prepared images (persistent)
│   ├── Surface_Laptop_Win11_20260519\
│   │   ├── boot\
│   │   ├── efi\
│   │   ├── sources\
│   │   └── iso-label.txt
│   └── ...
└── ISO_Temp\                 # Temporary ISO extraction (for USB-from-ISO feature)
```

### Automatic Cleanup
- `Windows11\`, `Drivers\`, `Mount\`, and `ISO_Temp\` are automatically cleaned after operations
- `SavedImages\` persists for future use
- `Config\` is cleaned after saved image creation

---

## 🚀 Sample Workflows

### Workflow 1: Prepare USB with Drivers (Full Process)
1. Select Windows 11 ISO file
2. Select driver MSI file
3. Click "Prepare Image with Drivers"
4. Read time warning (45-60 minutes typical) and click "Yes"
5. Insert USB drive when prompted
6. Confirm USB selection and data erasure warning
7. Optionally save prepared image for future use

### Workflow 2: Quick USB from Saved Image
1. Click "Create from Saved Image"
2. Choose previously saved image from dropdown
3. Select USB drive
4. Click "Create USB" - **much faster than full preparation!**

### Workflow 3: Direct USB from ISO (No Drivers)
1. Click "Create USB from ISO"
2. Select Windows 11 ISO
3. Select USB drive
4. Click "Create USB from ISO" - creates standard bootable USB without driver injection

---

## ⚙️ How It Works

### Driver Injection Process

```mermaid
graph TD
    A[Select ISO & MSI] --> B[Validate Inputs]
    B --> C[Mount ISO]
    C --> D[Copy ISO Contents]
    D --> E[Extract MSI Drivers]
    E --> F[Inject into WinPE]
    F --> G[Inject into WinSetup]
    G --> H[Inject into Windows Editions]
    H --> I[Inject into WinRE]
    I --> J[Split WIM if >4GB]
    J --> K[Create Bootable USB]
    K --> L[Optional: Save Image]
```

### Technical Details

#### 1. **ISO Mounting & Extraction**
   - Uses Windows `Mount-DiskImage` to mount ISO
   - Copies all files with `robocopy` for speed
   - Clears read-only attributes for modification

#### 2. **Driver Extraction**
   - Uses `msiexec /a` to extract MSI contents
   - Validates presence of `.inf` driver files
   - Checks for digital signatures (`.cat` files)

#### 3. **WinPE Injection** (boot.wim Index 1)
   - Mounts WinPE image
   - Runs `DISM /Add-Driver` recursively
   - Ensures drivers available during initial boot

#### 4. **WinSetup Injection** (boot.wim Index 2)
   - Mounts Windows Setup environment
   - Injects drivers for setup process
   - Critical for detecting hardware during installation

#### 5. **Edition Processing** (install.wim)
   - Enumerates all Windows editions (Pro, Enterprise, Home, etc.)
   - User can select specific editions or process all
   - Each edition mounted and injected separately
   - **Time intensive**: 10-15 minutes per edition

#### 6. **WinRE Injection** (Recovery Environment)
   - Locates `Winre.wim` inside each edition
   - Injects drivers for recovery scenarios
   - Ensures hardware detection in recovery mode

#### 7. **FAT32 Optimization**
   - Checks if `install.wim` exceeds 4GB (FAT32 file size limit)
   - Automatically splits into `install.swm`, `install2.swm`, etc.
   - Uses DISM split with 3.8GB chunks for compatibility

#### 8. **USB Creation**
   - Removes all existing partitions
   - Initializes disk as MBR (UEFI/Legacy compatible)
   - Creates 14GB partition (Windows 11 minimum recommendation)
   - Formats as FAT32 for UEFI compatibility
   - Copies all modified files to USB
   - Preserves ISO volume label

---

## 🛠️ Troubleshooting

### Common Issues

#### "Access Denied" or "Administrator required"
**Solution**: Always run as Administrator
```powershell
# Check if running as admin:
([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
```

#### "Execution Policy" error
**Solution**: Bypass execution policy for the session
```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\WinImagePrep_V3.ps1
```

#### Stuck at 50% progress / Application appears frozen
**Expected Behavior**: DISM driver injection operations take 10-15 minutes **per operation**
- Total expected time: 45-60 minutes for full injection
- Progress updates between major steps (not during DISM operations)
- Do NOT close the application - it's working!

#### "Error 0x800F0823" - Access denied during DISM
**Cause**: WIM image is already mounted or locked
**Solution**: Use "Repair/Cleanup" button to force unmount all images

#### "Error: Not enough disk space"
**Solution**: Free up space on C: drive
- Need 25GB minimum
- Delete temporary files: `C:\Windows\Temp\`
- Clean up old Windows updates: `Disk Cleanup → Clean up system files`

#### "No USB drives found"
**Solution**: 
- Ensure USB drive is physically connected
- Try different USB port
- Check if USB appears in Disk Management
- Click "Refresh" button in application

#### USB drive shows as "Not formatted" after creation
**Cause**: Some systems show this briefly while finalizing
**Solution**: 
- Safely eject and reinsert USB
- Verify files exist on USB manually
- Boot test in UEFI mode

#### "The file install.wim was not found"
**Cause**: Some ISOs use install.esd instead of install.wim
**Solution**: Convert ESD to WIM first:
```powershell
dism /Get-WimInfo /WimFile:install.esd
dism /Export-Image /SourceImageFile:install.esd /SourceIndex:1 /DestinationImageFile:install.wim /Compress:max /CheckIntegrity
```

### Manual Cleanup

If the tool crashes or is forcibly closed:

```powershell
# Check mounted images
Get-WindowsImage -Mounted

# Force unmount all
Get-WindowsImage -Mounted | ForEach-Object {
    Dismount-WindowsImage -Path $_.Path -Discard
}

# Clean directories
Remove-Item -Path C:\WinImagePrep\Windows11\* -Recurse -Force
Remove-Item -Path C:\WinImagePrep\Drivers\* -Recurse -Force
Remove-Item -Path C:\WinImagePrep\Mount\* -Recurse -Force
```

Or simply click **"Repair/Cleanup"** button in the application.

---

## 📊 Performance & Timing

### Typical Processing Times

| Operation | Duration | Notes |
|-----------|----------|-------|
| ISO Validation | 30-60 seconds | Depends on ISO size |
| ISO Extraction | 2-5 minutes | ~5GB of files |
| MSI Extraction | 1-2 minutes | Varies by driver package size |
| WinPE Injection | 5-10 minutes | First DISM operation |
| WinSetup Injection | 5-10 minutes | Second DISM operation |
| Edition Injection | 10-15 min **each** | Multiply by number of editions (typically 5-10) |
| WinRE Injection | 3-5 min each | Per edition with WinRE |
| WIM Splitting | 3-5 minutes | Only if install.wim >4GB |
| USB Creation | 5-10 minutes | ~6GB file copy |
| **Total Time** | **45-90 minutes** | Full workflow with all editions |

### Optimization Tips
- **Select specific editions** instead of processing all (saves 30-60 minutes)
- Use an **SSD** for C:\WinImagePrep working directory
- **Close other applications** to free up RAM for DISM
- **Disable antivirus temporarily** (real-time scanning slows DISM)
- Use **USB 3.0+ drives** for faster USB creation

---

## 🔒 Security Considerations

### Administrator Privileges
- **Required**: Full disk access, WIM mounting, partition management
- **Best Practice**: Run only trusted scripts with admin rights
- **Audit**: Review script source code before execution

### Driver Validation
- Tool checks for `.cat` signature files
- **Recommendation**: Only use official driver packages from hardware manufacturers
- **Surface drivers**: Download from Microsoft Surface support website

### Antivirus
- Some AV software may flag PowerShell scripts as suspicious
- **False Positive**: Common with scripts requiring admin privileges
- **Mitigation**: Review script, add exception, or use digital signature

---

## 🔮 Roadmap / Future Development

### Next Major Version: C# WPF Native Application

**Planned Features:**
- ✨ **True async operations** - No UI freezing, real-time progress during DISM
- ✨ **Faster performance** - Native C# is 5-10x faster than PowerShell
- ✨ **Digital signing** - No antivirus false positives
- ✨ **Professional installer** - MSI/MSIX package with Start Menu integration
- ✨ **Smaller footprint** - ~5MB exe vs current PowerShell overhead
- ✨ **Enhanced UI** - Modern Fluent Design with animations
- ✨ **Multi-threading** - Process multiple editions in parallel
- ✨ **Update checker** - Automatic version checking via GitHub
- ✨ **Telemetry** (optional) - Anonymous usage statistics for improvements
- ✨ **Log export** - Save operation logs for troubleshooting
- ✨ **Dark mode** - Theme options for user preference

**Timeline**: Under development, targeting Q3 2026

### Feature Requests (Current Version)
- [ ] Support for .esd images (in addition to .wim)
- [ ] Network share support (UNC paths)
- [ ] ISO creation (export modified files back to ISO)
- [ ] Multiple MSI support (inject drivers from multiple sources)
- [ ] Configurable partition sizes (currently fixed at 14GB)
- [ ] exFAT support for drives >32GB (for future-proofing)
- [ ] Command-line interface for automation/scripting

---

## 📝 Changelog

### Version 3.0 (May 19, 2026)
- **NEW**: Silent background processing (no popup windows)
- **NEW**: Hidden PowerShell console for cleaner appearance
- **NEW**: Real-time USB drive information panel
- **NEW**: Time warning dialog (45-60 minute expected duration)
- **NEW**: Professional blue-themed UI
- **NEW**: About dialog with version information
- **IMPROVED**: Visual warnings with color-coded alerts
- **IMPROVED**: Better progress tracking with detailed messages
- **IMPROVED**: Enhanced error messages with context

### Version 2.0 (May 18, 2026)
- **NEW**: Edition selection dialog (choose specific Windows editions)
- **NEW**: Saved image management (save and reuse prepared images)
- **NEW**: ISO integrity validation
- **NEW**: Disk space checking (25GB requirement)
- **NEW**: Driver validation with signature checking
- **NEW**: Progress dialogs with cancellation support
- **NEW**: Error recovery with automatic cleanup
- **NEW**: Repair/Cleanup utility for stuck mounts
- **NEW**: USB-from-ISO feature (direct USB creation without drivers)
- **NEW**: Create-from-saved-image workflow
- **IMPROVED**: WIM splitting for FAT32 compatibility
- **IMPROVED**: Volume label preservation
- **IMPROVED**: Detailed operation logging
- **FIXED**: Unicode character encoding issues
- **FIXED**: String interpolation parser errors

### Version 1.0 (Initial Release)
- Basic driver injection into Windows 11 ISO
- Simple USB creation workflow
- Manual mount/unmount operations

---

## 🤝 Contributing

Contributions are welcome! If you'd like to improve this tool:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

### Development Guidelines
- Test on both Windows 10 and Windows 11
- Verify with multiple ISO sources (Consumer/Business editions)
- Include error handling for all external commands
- Add comments for complex logic
- Update README with new features

---

## ⚠️ Important Notes

- **ALL DATA ON THE SELECTED USB DRIVE WILL BE ERASED** - Triple-check drive selection before proceeding
- For ISOs with `install.wim` larger than 4GB, the script automatically splits to `install.swm` files for FAT32 compatibility
- Some operations (mounting, driver injection, formatting) can take **45-60 minutes or more** - be patient!
- This tool is designed for clean, UEFI-compatible USB creation (not for legacy BIOS)
- All operations require Administrator privileges
- All destructive actions prompt for explicit user confirmation

---

## 🚨 Known Limitations

- **Architecture**: Only supports x64 and ARM64 Windows 11 ISOs with standard install.wim layout
- **USB Size**: Requires minimum 14GB USB drive (32GB+ recommended for future updates)
- **BIOS Support**: Designed for UEFI only (not compatible with legacy BIOS boot)
- **Driver Format**: Only MSI-packaged drivers supported (must contain .inf files)
- **Security Software**: Some antivirus or endpoint protection may interfere with file operations
- **Network Paths**: UNC paths not currently supported - use local files only
- **ESD Images**: Install.esd format requires manual conversion to install.wim first

---

## 🔒 Security & Safety

### Data Collection
- The script does **NOT** collect or transmit any data
- All file operations are performed locally on your machine
- No telemetry, analytics, or external network calls

### Permissions
- **Administrator privileges required** for:
  - Mounting/unmounting disk images
  - Partition management and formatting
  - DISM operations on WIM files
- **Best Practice**: Review script source code before granting admin access

### Confirmations
- All destructive actions require explicit user confirmation
- USB drive selection shows full drive details before formatting
- Multiple warnings for data loss scenarios

### Driver Safety
- Tool validates presence of .inf driver files
- Checks for digital signatures (.cat files)
- **Recommendation**: Only use official drivers from hardware manufacturer websites
- **Surface Drivers**: Download from [Microsoft Surface Support](https://support.microsoft.com/en-us/surface/download-drivers-and-firmware-for-surface)

---

## 📜 License

This project is licensed under the MIT License:

```
MIT License

Copyright (c) 2026 andrew-kemp

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## 👤 Author

**andrew-kemp**

Contributions, issues, and feature requests are welcome!

---

## � Acknowledgments

- **Microsoft DISM** - Core driver injection technology
- **Rufus** - Inspiration for UI/UX design
- **Surface Driver Team** - MSI-packaged drivers
- **PowerShell Community** - WPF XAML examples and best practices

---

## 📞 Support

### Issues & Bug Reports
- GitHub Issues: [Create an issue](https://github.com/andrew-kemp/WinImagePrep/issues)
- Include: Windows version, PowerShell version, full error message, operation log

### Questions & Discussion
- GitHub Discussions: [Ask a question](https://github.com/andrew-kemp/WinImagePrep/discussions)

### Documentation
- [Microsoft DISM Reference](https://docs.microsoft.com/en-us/windows-hardware/manufacture/desktop/dism-driver-servicing-command-line-options)
- [Surface Drivers Download](https://support.microsoft.com/en-us/surface/download-drivers-and-firmware-for-surface)
- [Windows 11 ISO Download](https://www.microsoft.com/software-download/windows11)

---

## ⚠️ Disclaimer

This tool is provided "as-is" without warranty of any kind. Always:
- **Backup important data** before operations
- **Test USB drives** in non-production environments first
- **Verify driver sources** are official and trusted
- **Use official Windows ISOs** from Microsoft

The author is not responsible for data loss, hardware issues, or failed Windows installations resulting from use of this tool.

---

## 🌟 Star History

If you find this tool useful, please consider giving it a star on GitHub! ⭐

---

**Made with ❤️ for the Windows deployment community**

---

*Last Updated: May 19, 2026*  
*Current Version: 3.0*  
*Status: Active Development*
