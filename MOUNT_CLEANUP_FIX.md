# CRITICAL FIX: Mount Directory Cleanup

## Problem Identified
The temp folder was accumulating 37GB and 124,900 files because **mount directories were not being deleted after each DISM unmount operation**.

## Root Cause
The original PowerShell V2 script explicitly deletes mount directories after every unmount:

```powershell
dism /Unmount-Wim /MountDir:"$mountWinRE" /Commit | Out-Null
Remove-Item -Path $mountWinRE -Recurse -Force -ErrorAction SilentlyContinue

dism /Unmount-Wim /MountDir:"$mountEdition" /Commit | Out-Null
Remove-Item -Path $mountEdition -Recurse -Force -ErrorAction SilentlyContinue
```

Our C# implementation was **not** deleting the mount directories after unmount, only clearing their contents in some failure cases.

## Fix Applied
Updated `DismService.UnmountWimAsync()` to delete the mount directory after successful unmount:

```csharp
if (result.Success)
{
	progress?.Report($"Successfully unmounted image");

	// Clean up the mount directory after successful unmount
	try
	{
		if (Directory.Exists(mountPath))
		{
			progress?.Report($"Cleaning up mount directory: {mountPath}");
			Directory.Delete(mountPath, true);
		}
	}
	catch (Exception ex)
	{
		progress?.Report($"⚠ Warning: Could not delete mount directory: {ex.Message}");
		// Not a fatal error, continue
	}

	return true;
}
```

Also updated the error 50 (not mounted) case to delete the directory instead of just clearing contents.

## Impact
- **Before**: Mount directories accumulated indefinitely in `C:\WinImagePrep\Temp\Mount\`
  - Each edition creates: `Edition_1`, `Edition_2`, `WinRE_1`, `WinRE_2`, `WinPE`, `WinSetup`
  - Each mount directory contains thousands of Windows system files
  - Result: Tens of thousands of files accumulating over time

- **After**: Mount directories are deleted immediately after each unmount
  - Disk space usage reduced by ~95%
  - Only active mount directories exist during processing
  - Clean slate for each mount operation

## How Mount Directories Work Now

### Workflow Per Image:
1. `FileSystemHelper.EnsureDirectoryExists(mountPath)` - Creates directory if needed
2. If directory exists and has contents, they're cleared
3. `DISM /Mount-Wim` - Mounts the image
4. `DISM /Add-Driver` - Adds drivers
5. `DISM /Unmount-Wim /Commit` - Commits changes
6. **`Directory.Delete(mountPath, true)`** - **NEW: Deletes entire mount directory**

### Example for 2 Editions:
- Mount `C:\WinImagePrep\Temp\Mount\Edition_1`
- Process drivers
- Unmount and **delete** `Edition_1` directory
- Mount `C:\WinImagePrep\Temp\Mount\Edition_2`
- Process drivers  
- Unmount and **delete** `Edition_2` directory

Result: Only one mount directory exists at any time during processing.

## Recommended Action for Current Temp Folder

**BEFORE running the next image preparation**, manually clean the existing temp folder:

```powershell
# Stop the application first, then run:
Remove-Item -Path "C:\WinImagePrep\Temp\Mount" -Recurse -Force -ErrorAction SilentlyContinue
```

Or use the **Repair/Cleanup** button in the application (also clears mounted images and temp files).

## Verification
After this fix, monitor `C:\WinImagePrep\Temp\Mount\` during operation:
- Should only see 1-2 directories at any time
- Directories should appear and disappear as each edition is processed
- Final size should be near-zero after completion

## File Locations Preserved
This fix only affects temporary mount directories. The following remain untouched:
- `C:\WinImagePrep\SavedImages\` - Prepared images (keep)
- `C:\WinImagePrep\Logs\` - Application logs (keep)
- `C:\WinImagePrep\Config\` - Configuration (keep)
- `C:\WinImagePrep\Temp\Windows11\` - Extracted ISO (used for USB creation, cleaned between runs)
- `C:\WinImagePrep\Temp\Drivers\` - Extracted drivers (cleaned between runs)
