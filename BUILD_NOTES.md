# Build Notes - AppData Directory Structure Update

**Build Date**: May 21, 2026
**Build Type**: Release
**EXE Location**: `WinImagePrep\bin\Release\net8.0-windows\WinImagePrep.exe`

## What's New in This Build

### 🎯 Major Change: Smart Directory Structure
The app now uses a hybrid directory approach that separates temporary work files from permanent outputs.

#### Before:
- Everything went to `C:\WinImagePrep\` (required admin, lots of clutter)

#### After:
**Temporary files** → `%LOCALAPPDATA%\WinImagePrep\Temp\`
- ISO extractions
- Driver extractions
- DISM mount points
- Auto-cleaned by Windows
- Fewer permission issues

**Permanent files** → `C:\WinImagePrep\`
- Saved images (protected!)
- Logs
- Configuration

### 🔧 Technical Improvements

1. **Better Permissions**
   - Temp operations use AppData (less admin friction)
   - Robocopy should have fewer permission issues
   - Still requires admin for DISM and USB formatting

2. **Safer Cleanup**
   - "Repair/Cleanup" only touches temp directories
   - Your saved images are never deleted
   - Logs are preserved

3. **Better Disk Space Management**
   - Checks free space where temp files will actually be written
   - Windows can auto-cleanup old temp files

4. **Faster ISO Mounting**
   - Increased wait time from 2s to 5s after mount
   - Added drive accessibility verification
   - Better error reporting for robocopy failures

### 📋 Files Modified
- `AppConfiguration.cs` - New path structure
- `App.xaml.cs` - Initialize both directory types
- `CleanupHelper.cs` - Smart cleanup (temp only)
- `MainViewModel.cs` - Updated disk space checks
- `IsoService.cs` - Better mount timing and error handling

### ✅ Build Status
- Build: **SUCCESS**
- Warnings: **0**
- Errors: **0**
- Icon: **Embedded (32x32)**
- Size: **0.17 MB**

## Testing Checklist

### First Run
- [ ] Creates `C:\Users\[username]\AppData\Local\WinImagePrep\Temp\`
- [ ] Creates `C:\WinImagePrep\SavedImages\`, `Logs\`, `Config\`
- [ ] Logs appear in `C:\WinImagePrep\Logs\`

### Image Preparation Workflow
- [ ] ISO extracts to AppData temp location
- [ ] Drivers extract to AppData temp location
- [ ] Operation completes successfully
- [ ] No permission errors on robocopy

### Cleanup Testing
- [ ] Click "Repair/Cleanup" button
- [ ] Temp directories are cleaned
- [ ] Saved images remain untouched
- [ ] Logs remain untouched
- [ ] App restarts cleanly

### Saved Images
- [ ] "From Saved Image" still works
- [ ] Saved images persist across cleanups
- [ ] Saved images in `C:\WinImagePrep\SavedImages\`

## Known Considerations

⚠️ **Still requires admin for**:
- DISM operations (mount/unmount WIM files)
- USB drive formatting
- Some driver injection operations

✅ **No longer requires admin for**:
- Reading/writing temp files
- Most file operations during ISO extraction
- Log writing

## Rollback Plan

If issues occur, previous behavior can be restored by:
1. Open `AppConfiguration.cs`
2. Change `TempBaseDirectory` to return `PersistentBaseDirectory`
3. Rebuild

## Next Steps

Consider adding:
- UI indicator showing temp directory location
- Button to open temp folder in Explorer
- Button to open saved images folder
- Setting to customize persistent directory location
- Auto-cleanup temp on successful USB creation
- Disk space monitoring for both locations

---

**For detailed information**, see:
- `DIRECTORY_STRUCTURE_REFACTOR.md` - Complete technical details
- `ISO_EXTRACTION_FIX.md` - Robocopy timing improvements
