# ISO Extraction Fix - January 2025

## Problem
When attempting to prepare an image with drivers, the app would fail during ISO extraction with the following errors:
1. **Robocopy Exit Code 16**: "Serious error - Robocopy did not copy any files"
2. **Binding Error**: "A TwoWay or OneWayToSource binding cannot work on the read-only property 'ImageSizeDisplay'"

## Root Cause Analysis

### Issue 1: Robocopy Exit Code 16
The ISO was being mounted successfully using PowerShell's `Mount-DiskImage`, but robocopy was failing immediately after with exit code 16. This indicates one of two problems:
- **Timing Issue**: The drive letter was returned but the filesystem wasn't fully ready for access
- **Permission Issue**: Robocopy couldn't access the mounted ISO or destination directory

**Original code:**
```csharp
// Wait a moment for the drive to be ready
await Task.Delay(2000, cancellationToken);
return driveLetter;
```

The 2-second delay was insufficient. Windows needs more time to fully mount and index the ISO filesystem before it's accessible.

### Issue 2: ImageSizeDisplay Binding
The error message about `ImageSizeDisplay` was a red herring - the property is correctly defined as a read-only computed property in `WimEdition.cs`:
```csharp
public string ImageSizeDisplay => FormatBytes(ImageSize);
```

And the XAML binding in `EditionSelectorWindow.xaml` is correct:
```xaml
<Run Text="{Binding ImageSizeDisplay}"/>
```

This error likely appeared due to the workflow failing partway through, causing WPF to report binding issues during cleanup.

## Solutions Implemented

### Fix 1: Increased Mount Wait Time and Added Drive Verification
Modified `IsoService.MountIsoAsync()` to:
1. Increase initial wait from 2 seconds to 5 seconds
2. Verify the drive is actually accessible using `Directory.Exists()`
3. Add fallback wait if drive isn't immediately accessible
4. Report detailed status to operation log

**File**: `WinImagePrep/Services/IsoService.cs`

```csharp
progress?.Report($"ISO mounted to drive {driveLetter}:");
// Wait for the drive to be fully ready (increased from 2s to 5s)
await Task.Delay(5000, cancellationToken);

// Verify the drive is accessible
var drivePath = $"{driveLetter}:\\";
if (Directory.Exists(drivePath))
{
	progress?.Report($"Drive {driveLetter}: is accessible and ready");
	return driveLetter;
}
else
{
	progress?.Report($"Warning: Drive {driveLetter}: was mounted but is not accessible");
	await Task.Delay(3000, cancellationToken); // Wait additional time
	if (Directory.Exists(drivePath))
	{
		return driveLetter;
	}
}
```

### Fix 2: Enhanced Error Reporting for Robocopy
Added detailed error output reporting to help diagnose future issues:

**File**: `WinImagePrep/Services/IsoService.cs`

```csharp
if (success)
{
	progress?.Report("ISO extracted successfully");
	return true;
}
else
{
	progress?.Report($"Failed to extract ISO (exit code: {result.ExitCode})");
	if (!string.IsNullOrEmpty(result.Error))
	{
		progress?.Report($"Error details: {result.Error}");
	}
	if (!string.IsNullOrEmpty(result.Output))
	{
		progress?.Report($"Output: {result.Output}");
	}
	return false;
}
```

## Testing Required

After these changes, please test:

1. **Prepare Image with Drivers workflow**:
   - Select a Windows 11 ISO
   - Select a driver MSI file
   - Click "Prepare Image with Drivers"
   - Monitor the operation log for:
	 - Successful ISO mounting
	 - "Drive X: is accessible and ready" message
	 - Successful ISO extraction
	 - Driver injection completing

2. **Edition Selection**:
   - The edition selector should open without binding errors
   - All editions should display with their sizes correctly

3. **Error Scenarios**:
   - If extraction still fails, the operation log should now show detailed error messages
   - Check for permission issues or disk space problems

## Admin Requirements

**Important**: This tool MUST be run as Administrator because:
- Robocopy requires elevated permissions to copy system files
- DISM operations require administrator privileges
- USB drive formatting requires administrator privileges
- ISO mounting via PowerShell works better with elevation

The app displays a warning if not running as admin, but it should ideally enforce this requirement.

## Robocopy Exit Codes Reference

For future debugging:
- **0-7**: Success (various levels of files copied/skipped)
- **8**: Some files or directories could not be copied (copy errors occurred)
- **16**: Serious error. Robocopy did not copy any files (usually permissions or path issues)

## Next Steps

If the issue persists after this fix:
1. Check the detailed error output now being logged
2. Verify the app is running as Administrator
3. Check available disk space on C: drive (needs ~25GB)
4. Verify the ISO file is not corrupted
5. Check Windows Event Viewer for mount/dismount errors
