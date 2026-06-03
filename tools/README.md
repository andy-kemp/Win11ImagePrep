# Admin Tools

This folder contains maintenance tools for repository maintainers.

## Update-AppList.ps1

**Purpose**: Scan a Windows ISO and generate/update `app-list.json` for the GitHub repository.

**Requirements**:
- Windows 10/11
- Administrator privileges
- PowerShell 5.1 or later

### Basic Usage

```powershell
# Run as Administrator
cd tools
.\Update-AppList.ps1 -IsoPath "C:\ISOs\Win11_24H2.iso"
```

### Options

**Generate New List**
```powershell
.\Update-AppList.ps1 -IsoPath "C:\path\to\windows.iso"
```
- Scans ISO from scratch
- Overwrites existing `app-list.json`
- Generates generic descriptions

**Merge with Existing (Recommended)**
```powershell
.\Update-AppList.ps1 -IsoPath "C:\path\to\windows.iso" -MergeWithExisting
```
- Keeps existing descriptions
- Adds new apps discovered in ISO
- Preserves your manual edits

**Custom Output Location**
```powershell
.\Update-AppList.ps1 -IsoPath "C:\path\to\windows.iso" -OutputPath "C:\temp\app-list.json"
```

### Workflow

1. **Download latest Windows 11 ISO**
   - From Microsoft, VLSC, or MSDN

2. **Run the script**
   ```powershell
   cd C:\Users\AndrewKemp\source\repos\andy-kemp\Win11ImagePrep\tools
   .\Update-AppList.ps1 -IsoPath "C:\Downloads\Win11_24H2.iso" -MergeWithExisting
   ```

3. **Review output**
   - Check `..\app-list.json`
   - Edit descriptions for new apps
   - Verify package names

4. **Commit and push**
   ```powershell
   git add ..\app-list.json
   git commit -m "Update app list from Win11 24H2"
   git push origin main
   ```

5. **Done!**
   - All users get updates automatically

### How It Works

1. Mounts the ISO
2. Extracts to temp folder (to avoid DISM read-only issues)
3. Dismounts ISO
4. Mounts `install.wim` Index 1
5. Runs `dism /Get-ProvisionedAppxPackages`
6. Parses output into JSON
7. Merges with existing if requested
8. Cleans up temp files

### Example Output

```
========================================
  Windows App List Generator
========================================

[1/7] Mounting ISO...
	  ✓ ISO mounted to D:\
[2/7] Extracting ISO contents to temp folder...
	  This may take 3-5 minutes...
	  ✓ ISO extracted
[3/7] Dismounting ISO...
	  ✓ ISO dismounted
[4/7] Locating install.wim...
	  ✓ Found: install.wim
[5/7] Reading Windows editions...
	  ✓ Using Index 1
[6/7] Mounting Windows image (this may take 2-3 minutes)...
	  ✓ Image mounted
[7/7] Scanning provisioned apps...
	  ✓ Found 47 provisioned apps

Cleaning up...
✓ Cleanup complete

Generating app list JSON...
  • Clipchamp.Clipchamp (kept existing description)
  • Microsoft.BingNews (kept existing description)
  + Microsoft.NewApp24H2 (NEW)
  ...

========================================
✓ SUCCESS!
========================================

App list saved to: ..\app-list.json
Total apps: 48

Next steps:
  1. Review the generated file
  2. Edit descriptions as needed
  3. Commit and push to GitHub:
	 git add app-list.json
	 git commit -m "Update app list from [ISO name]"
	 git push origin main
```

### Duration

- **ISO extraction**: 3-5 minutes
- **WIM mount**: 2-3 minutes
- **App scan**: 30 seconds
- **Total**: ~5-10 minutes

### Troubleshooting

**Error: "This script requires Administrator privileges"**
- Right-click PowerShell and select "Run as Administrator"

**Error: "Failed to mount ISO"**
- Verify ISO is not corrupt
- Ensure no other process has the ISO open
- Try a different ISO file

**Error: "install.wim or install.esd not found"**
- ISO may not be a valid Windows installation media
- Verify you're using a Windows 11 ISO (not a recovery or upgrade ISO)

**Script hangs during extraction**
- Be patient - extracting 5-6 GB can take time
- Check Task Manager for robocopy.exe activity

### Maintenance Schedule

**Recommended**: Update after each major Windows 11 release
- 24H2, 23H2, 22H2, etc.
- Or when users report missing apps

**Not needed for**:
- Monthly cumulative updates
- Security patches
- Minor builds

### Notes

- This tool is for maintainers only
- End users should use the main WinImagePrep app
- The script never modifies the source ISO
- All work is done in temp folders
- Cleanup is automatic even if script fails

---

**Created by**: Andy Kemp  
**Last Updated**: January 2026  
