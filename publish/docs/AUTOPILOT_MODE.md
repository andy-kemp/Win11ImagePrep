# Autopilot Mode Feature

## Overview
WinImagePrep now supports **Autopilot Mode** for creating Windows installation media that's optimized for Autopilot-enrolled devices. This mode ensures the unattended installation preserves the Autopilot OOBE experience while automating the basic setup tasks.

## What is Autopilot Mode?

When you check **"This device is enrolled in Autopilot"** in the unattended configuration dialog, the tool will:

### ✅ What It DOES
- **Auto-accepts license agreement** - Skips the EULA screen
- **Auto-wipes and partitions disk** - Automatically removes old partitions and creates fresh ones
- **Preserves OOBE for Autopilot enrollment** - Shows the "Let's set things up..." screen with your company branding
- **Allows Azure AD join** - Keeps wireless setup enabled so the device can connect and join Azure AD
- **Skips local admin creation** - No local accounts; users will sign in with Azure AD credentials

### ❌ What It DOESN'T Do
- Does NOT skip the Out-of-Box Experience (OOBE)
- Does NOT hide your company logo and branding
- Does NOT create local administrator accounts
- Does NOT skip network setup
- Does NOT set a computer name (Autopilot will manage this)

## When to Use Autopilot Mode

**Use Autopilot Mode when:**
- Your device is enrolled in Windows Autopilot
- You want to reimage/refresh the device while keeping Autopilot enrollment
- You want the device to show company branding during setup
- Users will sign in with Azure AD (Entra ID) accounts

**Use Regular Unattended Install when:**
- This is NOT an Autopilot device
- You want a completely silent installation
- You need a local administrator account
- You want to skip all OOBE screens
- You want to set a specific computer name

## How to Configure

1. **Enable Unattended Installation**
   - Check "Enable Unattended Installation" in the main window

2. **Configure Settings**
   - Click "Configure Unattended Settings..."
   - Check **"This device is enrolled in Autopilot"**

3. **Basic Settings Still Apply**
   - Windows Edition (leave as auto-detect if multiple editions in ISO)
   - Language & Region preferences
   - Time Zone

4. **Hidden Options in Autopilot Mode**
   - Local Administrator Account section (hidden - Azure AD accounts only)
   - Computer Name (hidden - Autopilot will set this)
   - Setup Experience options (hidden - forced to Autopilot-friendly defaults)

## Technical Details

### Answer File Behavior

When Autopilot Mode is enabled, the generated `autounattend.xml` will:

```xml
<!-- Auto-accept license -->
<AcceptEula>true</AcceptEula>

<!-- Auto-partition disk -->
<DiskConfiguration>
	<WillWipeDisk>true</WillWipeDisk>
	<!-- Creates UEFI partitions automatically -->
</DiskConfiguration>

<!-- Preserve OOBE for Autopilot -->
<OOBE>
	<HideEULAPage>true</HideEULAPage>
	<!-- HideWirelessSetupInOOBE NOT included - wireless setup runs -->
	<!-- SkipMachineOOBE and SkipUserOOBE NOT set - OOBE runs normally -->
</OOBE>

<!-- NO LocalAccounts section - Azure AD accounts only -->
```

### Expected Installation Flow

1. **Boot from USB** - Device boots from the prepared USB drive
2. **Automatic Partition** - Disk is wiped and partitioned automatically
3. **Windows Installation** - Windows files are copied (no prompts)
4. **OOBE with Company Branding** - Device shows "Let's set things up..." with your company logo
5. **Network Connection** - User connects to Wi-Fi
6. **Autopilot Enrollment** - Device contacts Azure AD and applies Autopilot profile
7. **User Sign-In** - User signs in with Azure AD credentials
8. **Configuration Applied** - Apps, policies, and settings are deployed via Intune

## Disk Partitioning Warning

⚠️ **WARNING**: When auto-partitioning is enabled (default in Autopilot mode), the installation will **automatically WIPE ALL DATA** on the target disk (usually Disk 0).

- All existing partitions will be deleted
- All data will be lost
- This happens without any confirmation during installation
- Make sure you have backups before using this feature

## Comparison: Autopilot Mode vs. Standard Unattended

| Feature | Autopilot Mode | Standard Unattended |
|---------|---------------|---------------------|
| Auto-accept EULA | ✅ Yes | Configurable |
| Auto-partition disk | ✅ Yes | Configurable |
| Show OOBE | ✅ Yes (for Autopilot) | No (skipped) |
| Wireless setup | ✅ Yes (required) | Configurable |
| Local admin account | ❌ No (Azure AD only) | ✅ Yes |
| Computer name | ❌ No (Autopilot sets) | Configurable |
| Company branding | ✅ Yes (preserved) | No (OOBE skipped) |

## Troubleshooting

### Issue: Device not showing company branding
- **Cause**: SkipOOBE was enabled in standard mode
- **Solution**: Use Autopilot Mode instead

### Issue: Installation asks for disk partition
- **Cause**: Auto-partition not enabled
- **Solution**: Ensure "Automatically partition and format Disk 0" is checked

### Issue: Can't create local admin account
- **Cause**: Autopilot Mode is enabled
- **Solution**: This is intentional - Autopilot devices use Azure AD accounts only. Uncheck Autopilot Mode if you need a local account.

### Issue: Installation prompts for license agreement
- **Cause**: HideEULA not working properly
- **Solution**: Verify the autounattend.xml file in the USB root contains `<AcceptEula>true</AcceptEula>`

## Version History

- **v5.0.4** (Current) - Added dedicated Autopilot Mode with UI optimization
- **v5.0.0** - Initial Autopilot-friendly unattended install support
- **v4.5.0** - First unattended installation feature
