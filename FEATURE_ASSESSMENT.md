# WinImagePrep - Feature Assessment & Improvement Opportunities

**Date:** January 25, 2026  
**Current Version:** 5.4.5

---

## ✅ Recently Fixed (Working Well)

### Update System
- [x] Automatic update checks on startup
- [x] Manual update checks (Tools menu)
- [x] Update Now button actually works
- [x] Version numbers displayed in update dialog
- [x] Download from reliable GitHub raw URLs
- [x] Updater applies update and restarts app
- [x] UAC elevation for updater

---

## ⚠️ Known Issues (User Reported)

### Unattended Installation
- [ ] **EULA still showing?** - Code looks correct but needs testing
  - `AcceptEula` set in windowsPE pass
  - `HideEULAPage` set in oobeSystem pass
  - May need additional registry tweaks or ISO-specific workarounds

- [ ] **Partitioning not working?** - Code looks correct but needs testing
  - Disk clean commands (diskpart)
  - DiskConfiguration with EFI+MSR+Windows partitions
  - WillWipeDisk=true
  - May need pre-cleaning if BitLocker is enabled

---

## 💡 Potential Improvements

### 1. User Experience

#### UI/UX Enhancements
- [ ] **Dark mode support** - Modern theme option
- [ ] **Drag & drop ISO selection** - Easier file selection
- [ ] **Progress indicators during operations** - Better visual feedback
- [ ] **Operation log filtering** - Filter by severity (info/warning/error)
- [ ] **Operation log export** - Save log to file for troubleshooting
- [ ] **Recent ISOs list** - Quick access to previously used ISOs
- [ ] **Preset configurations** - Save/load unattended config templates
- [ ] **Tooltips on all options** - Hover help text for every setting

#### Wizard Improvements
- [ ] **First-run tutorial** - Guided tour of features
- [ ] **Quick start presets** - "Surface Laptop", "Enterprise Deployment", etc.
- [ ] **Configuration validation** - Warn about common mistakes before USB creation

### 2. Driver Integration

#### Current Limitations
- Requires MSI packages
- Manual selection process
- No driver verification

#### Possible Enhancements
- [ ] **Support non-MSI drivers** - CAB, INF, ZIP extraction
- [ ] **Driver auto-detection** - Scan hardware and suggest drivers
- [ ] **Driver version checking** - Warn if outdated drivers
- [ ] **Driver signing validation** - Verify driver authenticity
- [ ] **Batch driver injection** - Multiple MSI files at once
- [ ] **Driver cache** - Remember driver selections per ISO
- [ ] **Online driver database** - Download Surface/Dell/HP drivers automatically

### 3. App Removal

#### Current Implementation
- Dynamic discovery from ISO
- Manual checkbox selection
- Runtime GitHub app list

#### Possible Enhancements
- [ ] **App removal presets** - "Minimal", "Gaming", "Enterprise", "Developer"
- [ ] **App dependency checking** - Warn if removing required apps
- [ ] **Custom app lists** - User-defined removal lists
- [ ] **App size calculation** - Show space saved
- [ ] **Removal impact preview** - Show what might break
- [ ] **Post-install app removal** - Script to remove apps from running Windows

### 4. Unattended Installation

#### Current Issues to Debug
- [ ] **Test EULA acceptance on real hardware**
  - Try different Windows 11 builds (22H2, 23H2)
  - Test with/without network connection
  - Add debugging output to autounattend.xml

- [ ] **Test auto-partitioning on real hardware**
  - Test with BitLocker-encrypted disks
  - Test with existing partitions
  - Test with different disk sizes
  - Verify EFI/MSR/Windows partition layout

#### Potential Enhancements
- [ ] **Domain join support** - Auto-join Active Directory domain
- [ ] **Pre-installed software** - Include apps in unattended setup
- [ ] **Post-install scripts** - Run PowerShell after setup
- [ ] **Network configuration** - Static IP, DNS, proxy settings
- [ ] **Firewall rules** - Pre-configure Windows Firewall
- [ ] **Group Policy templates** - Apply GPO settings
- [ ] **Multiple user accounts** - Create standard users
- [ ] **BitLocker encryption** - Enable BitLocker automatically
- [ ] **Windows Update settings** - Configure update behavior
- [ ] **Privacy settings templates** - Pre-configured privacy options

### 5. Autopilot Mode

#### Current Implementation
- Basic Autopilot-friendly OOBE preservation
- Auto-partition and EULA acceptance
- Wireless setup enabled

#### Possible Enhancements
- [ ] **Autopilot profile validation** - Check if device is enrolled before imaging
- [ ] **Autopilot diagnostics** - Test Autopilot connectivity and configuration
- [ ] **Hybrid Azure AD join support** - On-prem AD + Azure AD
- [ ] **White Glove provisioning** - Pre-provision device at vendor
- [ ] **Self-deploying mode** - Kiosk/shared device scenarios
- [ ] **User-driven mode optimization** - User self-service deployment

### 6. ISO Management

#### Current Limitations
- Single ISO per operation
- No ISO validation beyond basic checks

#### Possible Enhancements
- [ ] **ISO integrity checking** - Verify ISO hash/signature
- [ ] **Multi-edition ISO support** - Better handling of combined ISOs
- [ ] **ISO download integration** - Download Windows 11 ISO from Microsoft
- [ ] **ISO customization preview** - Show what will be changed before USB creation
- [ ] **ISO version detection** - Display Windows build number
- [ ] **ISO comparison** - Compare two ISOs to see differences

### 7. USB Drive Management

#### Current Implementation
- Format to FAT32
- UEFI-compatible boot
- Split install.wim if >4GB

#### Possible Enhancements
- [ ] **Multi-boot USB** - Multiple Windows versions on one USB
- [ ] **Persistent storage partition** - Data partition on USB
- [ ] **USB label customization** - Custom volume label
- [ ] **USB icon customization** - Custom drive icon
- [ ] **Bootable recovery tools** - Include WinPE, diagnostics
- [ ] **USB write verification** - Verify files after copy
- [ ] **USB speed test** - Test USB write speed before starting

### 8. Logging & Diagnostics

#### Current Implementation
- Basic operation log
- File-based logging (`app.log`)
- Updater log (`WinImagePrep_Updater.log`)

#### Possible Enhancements
- [ ] **Log level configuration** - Info/Debug/Verbose modes
- [ ] **Log rotation** - Automatic log file management
- [ ] **Remote logging** - Send logs to central server (enterprise)
- [ ] **Crash reporting** - Anonymous crash analytics
- [ ] **Performance metrics** - Track operation durations
- [ ] **Export diagnostics package** - Bundle all logs/settings for support
- [ ] **Log viewer UI** - Dedicated log browser with search/filter

### 9. Settings & Configuration

#### Current Implementation
- Settings stored in `settings.json`
- Basic first-run wizard
- Update preferences

#### Possible Enhancements
- [ ] **Settings import/export** - Backup/restore settings
- [ ] **Settings sync** - Cloud sync via OneDrive/GitHub
- [ ] **Per-ISO settings** - Remember settings per ISO file
- [ ] **Enterprise deployment** - Deploy settings via GPO
- [ ] **Configuration schema validation** - Validate settings file
- [ ] **Settings migration** - Upgrade old settings to new format
- [ ] **Reset to defaults** - Quick factory reset

### 10. Automation & Scripting

#### Current Limitations
- Fully GUI-based
- No CLI interface

#### Possible Enhancements
- [ ] **Command-line interface** - Full CLI mode for scripting
- [ ] **PowerShell module** - Native PowerShell cmdlets
- [ ] **REST API** - Web service for remote operations
- [ ] **Batch processing** - Process multiple ISOs
- [ ] **Scheduled operations** - Automate recurring tasks
- [ ] **Configuration files** - JSON/YAML config for automation
- [ ] **Exit codes** - Proper error codes for scripts

### 11. Enterprise Features

#### Possible Enhancements
- [ ] **SCCM/MECM integration** - Task sequence integration
- [ ] **Intune integration** - Device enrollment automation
- [ ] **Active Directory integration** - Computer account pre-staging
- [ ] **Certificate deployment** - Include enterprise certificates
- [ ] **VPN configuration** - Pre-configure VPN settings
- [ ] **Printer deployment** - Install network printers
- [ ] **Software deployment** - Include corporate apps
- [ ] **Licensing** - Auto-activate with KMS/MAK keys
- [ ] **Compliance checking** - Verify regulatory requirements
- [ ] **Audit logging** - Track who created what images

### 12. Documentation

#### Current State
- README.md with features
- CHANGELOG.md with version history
- AUTOPILOT_MODE.md for Autopilot guidance
- Release notes for versions

#### Possible Improvements
- [ ] **Video tutorials** - YouTube walkthrough videos
- [ ] **Interactive help** - In-app help system
- [ ] **Troubleshooting guide** - Common problems and solutions
- [ ] **FAQ section** - Frequently asked questions
- [ ] **Best practices guide** - Recommended workflows
- [ ] **API documentation** - If CLI/API added
- [ ] **Contribution guide** - How to contribute (if open source)
- [ ] **Localization** - Multi-language documentation

---

## 🔧 Technical Debt

### Code Quality
- [ ] **Unit tests** - Test coverage for core services
- [ ] **Integration tests** - End-to-end testing
- [ ] **Code refactoring** - Simplify complex methods
- [ ] **Performance profiling** - Identify bottlenecks
- [ ] **Memory leak detection** - Check for memory issues
- [ ] **Thread safety review** - Async/await patterns
- [ ] **Error handling audit** - Consistent error handling
- [ ] **Dependency updates** - Keep NuGet packages current

### Architecture
- [ ] **MVVM cleanup** - Better separation of concerns
- [ ] **Dependency injection** - Use DI container
- [ ] **Service abstraction** - Interface-based services
- [ ] **Plugin architecture** - Extensibility support
- [ ] **Event system** - Decouple components
- [ ] **Configuration management** - Centralized config

---

## 📊 Priority Assessment

### High Priority (Critical Issues)
1. **Fix EULA acceptance** - Users report still seeing EULA
2. **Fix auto-partitioning** - Users report manual partition prompts
3. **Test on real hardware** - Validate unattended install works

### Medium Priority (Quality of Life)
1. **CLI interface** - Enable automation scenarios
2. **Preset configurations** - Save time for repeat operations
3. **Driver auto-detection** - Reduce manual work
4. **Dark mode** - Modern UI preference

### Low Priority (Nice to Have)
1. **Multi-boot USB** - Advanced feature
2. **Cloud sync** - Convenience feature
3. **Video tutorials** - Documentation enhancement

---

## 🧪 Testing Recommendations

### Unattended Installation Testing
1. **Create test USB with current code**
2. **Boot real hardware (Surface or other device)**
3. **Document exact screens that appear:**
   - Does EULA show?
   - Does partition selection show?
   - What prompts appear?
4. **Check generated `autounattend.xml`:**
   - Is it present on USB root?
   - Does it match expected XML structure?
   - Are all settings correctly formatted?
5. **Setup logs analysis:**
   - Check `C:\Windows\Panther\unattend.xml` (processed version)
   - Check `C:\Windows\Panther\setuperr.log` (errors)
   - Check `C:\Windows\Panther\setupact.log` (actions)

### Suggested Test Matrix
| Scenario | ISO Version | Hardware | Autopilot | Expected Result |
|----------|-------------|----------|-----------|-----------------|
| Standard unattended | 23H2 | Surface Laptop 5 | No | Fully automated, no prompts |
| Autopilot deployment | 23H2 | Surface Pro 9 | Yes | Auto-partition, OOBE preserved |
| BitLocker disk | 22H2 | Enterprise laptop | No | Diskpart clean succeeds |
| Multi-edition ISO | 23H2 | VM | No | Correct edition selected |

---

## 📞 Next Actions

### Immediate
1. **Test unattended install on real hardware** - Validate EULA/partition fixes
2. **Review setup logs** - Identify exact failure points
3. **Document test results** - Note what works and what doesn't

### Short Term
1. **Fix confirmed issues** - Address EULA/partition problems
2. **Add CLI interface** - Enable automation
3. **Create troubleshooting guide** - Help users self-diagnose

### Long Term
1. **Consider enterprise features** - SCCM, Intune integration
2. **Explore driver auto-detection** - Reduce manual work
3. **Build plugin architecture** - Community extensions

---

**Status:** Document created for planning purposes  
**Last Updated:** January 25, 2026  
**Author:** Andy Kemp Consulting Ltd
