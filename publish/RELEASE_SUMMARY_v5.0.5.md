# WinImagePrep v5.0.5 - Release Summary

**Product**: WinImagePrep  
**Version**: 5.0.5  
**Release Date**: June 4, 2026  
**Publisher**: Andy Kemp Consulting Ltd  
**Size**: 68.75 MB (single-file, self-contained)  

---

## 🚀 What's New in v5.0.5

### Major Feature: Autopilot Mode

This release introduces **Autopilot Mode**, a game-changing feature for enterprise deployments using Windows Autopilot.

#### The Problem We Solved
In v5.0.4 and earlier, using unattended installation with Autopilot-enrolled devices would:
- ❌ Skip the OOBE completely
- ❌ Lose company branding and "Let's set things up..." screen
- ❌ Still show privacy/telemetry screens (location, diagnostics, speech, inking)
- ❌ Create local admin accounts that weren't needed for Azure AD-joined devices

#### The Solution: Autopilot Mode
Now you can check **"This device is enrolled in Autopilot"** and get the best of both worlds:
- ✅ Auto-accepts license agreement (no EULA screen)
- ✅ Auto-wipes and partitions disk (perfect for refresh scenarios)
- ✅ **Preserves OOBE** so company branding appears
- ✅ **Skips privacy screens** (via XML + registry methods)
- ✅ Keeps wireless setup enabled (needed for Azure AD join)
- ✅ No local admin creation (Azure AD accounts only)
- ✅ Smart UI that hides irrelevant options

---

## 🎯 Who Should Use v5.0.4?

### Ideal For:
- **IT Departments** reimaging Autopilot-enrolled devices
- **System Administrators** creating standardized deployment media
- **Enterprise Environments** using Azure AD / Intune
- **MSPs** managing multiple client deployments
- **Anyone** tired of clicking through privacy screens during Windows setup

### Use Cases:
1. **Autopilot Device Refresh** - Wipe and reimage enrolled devices while keeping Autopilot enrollment
2. **Clean Autopilot Deployments** - Create media that preserves company branding
3. **Standard Unattended Installs** - Fully silent installs with local admin accounts
4. **Driver-Integrated Media** - Surface or other hardware-specific Windows installations
5. **App-Free Windows** - Remove bloatware before deployment

---

## 📊 Feature Comparison

| Feature | Autopilot Mode | Standard Unattended | Manual Install |
|---------|---------------|---------------------|----------------|
| License acceptance | Automatic | Automatic | Manual |
| Disk partitioning | Automatic | Automatic | Manual |
| Show OOBE | ✅ Yes (for Autopilot) | ❌ No | ✅ Yes |
| Company branding | ✅ Preserved | ❌ Skipped | ✅ Yes |
| Privacy screens | ✅ Skipped (registry) | ✅ Skipped | ❌ Manual clicks |
| Local admin | ❌ No (Azure AD) | ✅ Yes | Optional |
| Azure AD join | ✅ Automatic | Manual | Manual |
| Intune enrollment | ✅ Automatic | Manual | Manual |

---

## 🔧 Technical Improvements

### Privacy Screen Elimination
Previous versions (v5.0.4 and earlier) would sometimes still show privacy screens even with `SkipOOBE` enabled. v5.0.5 fixes this with a **dual-method approach**:

1. **XML-based** (in `autounattend.xml`):
   ```xml
   <HideOEMRegistrationScreen>true</HideOEMRegistrationScreen>
   <HideOnlineAccountScreens>true</HideOnlineAccountScreens>
   <HideLocalAccountScreen>true</HideLocalAccountScreen>
   ```

2. **Registry-based** (via FirstLogonCommands):
   ```cmd
   reg add HKLM\SOFTWARE\Policies\Microsoft\Windows\OOBE /v DisablePrivacyExperience /t REG_DWORD /d 1 /f
   ```

This **bulletproof approach** ensures privacy screens are suppressed regardless of Windows build variations.

### Conditional Behavior
The unattended installer now intelligently adapts:
- **Autopilot Mode ON**: Preserves OOBE, skips local accounts, keeps wireless setup
- **Autopilot Mode OFF**: Fully silent, creates local admin, configurable setup experience

---

## 📖 Documentation

This release includes comprehensive documentation:

1. **README.md** - Complete usage guide with:
   - Feature overview
   - Installation instructions
   - Autopilot vs. Standard comparison
   - Step-by-step configuration guides
   - Troubleshooting section

2. **AUTOPILOT_MODE.md** - Deep dive on Autopilot Mode:
   - When to use Autopilot Mode
   - Technical details on answer file generation
   - Expected installation flow
   - Common troubleshooting scenarios

3. **CHANGELOG.md** - Full version history:
   - All changes from v3.0.0 to v5.0.4
   - Upgrade notes
   - Breaking changes documentation

4. **ReleaseNotes.txt** - Human-readable release notes:
   - Feature highlights
   - Version-by-version improvements

---

## 🚦 Getting Started

### Quick Start (Autopilot Mode)
1. Run `WinImagePrep.exe` as Administrator
2. Select Windows 11 ISO
3. (Optional) Select driver MSI for Surface/hardware integration
4. (Optional) Configure app removal
5. Check **"Enable Unattended Installation"**
6. Click **"Configure Unattended Settings..."**
7. Check **"This device is enrolled in Autopilot"** ✅
8. Configure language/timezone
9. Click **Save**
10. Click **"Prepare Image with Drivers"**
11. Create bootable USB
12. Deploy!

### Expected Result
- Device boots from USB
- Disk automatically wiped and partitioned
- Windows installs (no prompts)
- **OOBE appears with your company logo** 🏢
- User connects to Wi-Fi
- Device enrolls via Autopilot
- User signs in with Azure AD
- Apps/policies deployed via Intune
- Done! 🎉

---

## ⚠️ Important Warnings

### Data Loss
When using unattended installation (Autopilot or Standard mode):
- **ALL DATA on the target disk will be PERMANENTLY DELETED**
- Auto-partitioning is enabled by default in Autopilot Mode
- There is NO confirmation during installation
- Ensure you have backups before deploying

### Autopilot Requirements
For Autopilot Mode to work properly:
- Device must already be enrolled in Azure AD Autopilot
- Autopilot profile must be configured in Intune
- Company branding requires Entra ID (Azure AD) tenant configuration
- Users must have valid Azure AD accounts

### Answer File Security
- The `autounattend.xml` file is stored on the USB root in plain text
- In Standard Unattended mode, admin passwords are stored in plain text
- In Autopilot Mode, no passwords are stored (Azure AD only)
- Consider physical security of deployment media

---

## 🔄 Upgrade Path

### From v5.0.4 or Earlier
1. Download `WinImagePrep.exe` (v5.0.5)
2. Replace existing executable
3. Settings are forward-compatible
4. **Review unattended configuration** - New Autopilot Mode option available
5. No other changes required

### From v4.x
- All v4.x settings and workflows are compatible
- New Autopilot Mode is optional enhancement
- Existing unattended configs continue to work

### From v3.x
- v3.x PowerShell-based configs NOT compatible
- Start fresh configuration with v5.0.5

---

## 📞 Support

**WinImagePrep** is developed and maintained by **Andy Kemp Consulting Ltd**

For support:
- **GitHub Issues**: [Win11ImagePrep/issues](https://github.com/andy-kemp/Win11ImagePrep/issues)
- **Documentation**: See README.md, AUTOPILOT_MODE.md, and CHANGELOG.md
- **Community**: GitHub Discussions (if enabled)

---

## 📝 License

**Proprietary Software**  
© 2026 Andy Kemp Consulting Ltd  
All rights reserved.

This software is provided as-is for evaluation and commercial use by licensed customers of Andy Kemp Consulting Ltd.

---

## 🎯 Next Steps

1. **Download**: Get `WinImagePrep.exe` from the [Releases](https://github.com/andy-kemp/Win11ImagePrep/releases) page
2. **Read**: Review `README.md` and `AUTOPILOT_MODE.md`
3. **Test**: Try Autopilot Mode in a lab environment first
4. **Deploy**: Roll out to production Autopilot devices
5. **Feedback**: Report issues or suggest features via GitHub Issues

---

**Thank you for using WinImagePrep!**

Andy Kemp Consulting Ltd  
Professional Windows Deployment Solutions  
June 2026
