# Directory Structure Refactoring - Complete

## Changes Summary

Successfully refactored the application to use a hybrid directory structure:

### Temporary Working Files (Auto-cleanup)
**Location**: `%LOCALAPPDATA%\WinImagePrep\Temp\`
- `Windows11\` - Extracted ISO contents
- `Drivers\` - Extracted driver files from MSI
- `Mount\` - DISM mount points
  - `PE\` - WinPE mount
  - `Setup\` - Setup mount

**Example path**: `C:\Users\andrew\AppData\Local\WinImagePrep\Temp\`

### Persistent Files
**Location**: `C:\WinImagePrep\`
- `SavedImages\` - Completed, reusable Windows images
- `Logs\` - Operation logs and error logs
- `Config\` - User settings (future use)

## Files Modified

### 1. AppConfiguration.cs
- Added `PersistentBaseDirectory` for saved outputs (`C:\WinImagePrep`)
- Added `TempBaseDirectory` for temporary work files (`%LOCALAPPDATA%\WinImagePrep\Temp\`)
- Added `LogsDirectory` property
- Moved temp directories to use `TempBaseDirectory`
- Moved persistent directories to use `PersistentBaseDirectory`
- Deprecated old `BaseDirectory` property for backward compatibility

### 2. App.xaml.cs
- `InitializeWorkingDirectory()` now creates both temp and persistent directory structures
- Updated `LogException()` to use `config.LogsDirectory`

### 3. CleanupHelper.cs
- Added `CleanupTempWorkingDirectories()` method
- Cleanup now only touches temporary directories, never saved images or logs
- Integrated into `CleanupMountedImages()` workflow

### 4. MainViewModel.cs
- Updated disk space check to use `TempBaseDirectory`
- Updated `RepairCleanup()` to use `TempBaseDirectory`

## Benefits

✅ **Better Permission Model**: Temp files in AppData don't require admin rights for read/write
✅ **Automatic Cleanup**: Windows periodically cleans AppData temp files
✅ **Protected Outputs**: Saved images and logs stay in predictable, protected location
✅ **Clear Separation**: Work-in-progress vs. finished products clearly separated
✅ **Safer Cleanup**: App can clean temp directories without risking user's saved work
✅ **Better UX**: Less clutter in C:\ root

## Testing Checklist

### On First Launch
- [ ] App creates `%LOCALAPPDATA%\WinImagePrep\Temp\` directory structure
- [ ] App creates `C:\WinImagePrep\` directory structure
- [ ] Logs are written to `C:\WinImagePrep\Logs\`

### During Image Preparation
- [ ] ISO extracts to `%LOCALAPPDATA%\WinImagePrep\Temp\Windows11\`
- [ ] Drivers extract to `%LOCALAPPDATA%\WinImagePrep\Temp\Drivers\`
- [ ] Mount points use `%LOCALAPPDATA%\WinImagePrep\Temp\Mount\`
- [ ] Disk space check shows correct drive (where AppData is located)

### After Cleanup
- [ ] "Repair/Cleanup" button clears temp directories
- [ ] Saved images in `C:\WinImagePrep\SavedImages\` are NOT deleted
- [ ] Logs in `C:\WinImagePrep\Logs\` are NOT deleted
- [ ] App restart re-creates temp directory structure

### Saved Image Workflow
- [ ] "From Saved Image" uses `C:\WinImagePrep\SavedImages\`
- [ ] Saved images persist across cleanups
- [ ] Saved images persist across app restarts

## Path Reference

```csharp
// Temporary (AppData Local)
config.TempBaseDirectory      // %LOCALAPPDATA%\WinImagePrep\Temp
config.Windows11Directory     // %LOCALAPPDATA%\WinImagePrep\Temp\Windows11
config.DriversDirectory       // %LOCALAPPDATA%\WinImagePrep\Temp\Drivers
config.MountDirectory         // %LOCALAPPDATA%\WinImagePrep\Temp\Mount
config.MountPEDirectory       // %LOCALAPPDATA%\WinImagePrep\Temp\Mount\PE
config.MountSetupDirectory    // %LOCALAPPDATA%\WinImagePrep\Temp\Mount\Setup

// Persistent (C:\WinImagePrep)
config.PersistentBaseDirectory // C:\WinImagePrep
config.SavedImagesDirectory    // C:\WinImagePrep\SavedImages
config.LogsDirectory           // C:\WinImagePrep\Logs
config.ConfigDirectory         // C:\WinImagePrep\Config
```

## Notes

- The old `BaseDirectory` property is marked `[Obsolete]` but still works for backward compatibility (returns `TempBaseDirectory`)
- DISM operations may still require admin privileges regardless of directory location
- Robocopy should have fewer permission issues with AppData temp directories
- Windows will eventually auto-cleanup old temp files if user doesn't manually clean

## Next Steps

After testing, consider:
1. Add UI indicator showing where temp files are located
2. Add UI button to open temp directory in Explorer
3. Add UI button to open saved images directory in Explorer
4. Add setting to customize persistent directory location
5. Add automatic temp cleanup on successful USB creation
