# UI Improvements Summary

## Changes Implemented

### 1. Window Behavior
- **Fixed Size**: Changed `ResizeMode` from `CanResize` to `NoResize`
- **Dynamic Height**: Changed from fixed `Height="820"` to `SizeToContent="Height"`
- **Result**: Window height automatically adjusts when log is expanded/collapsed, no maximize button, no manual resizing

### 2. Operation Log Behavior
- Changed from `MinHeight/MaxHeight` to fixed `Height="200"` on ListBox
- Changed row definition from `Height="*"` to `Height="Auto"`
- Log now properly collapses and window shrinks accordingly
- Log remains expanded by default (`IsExpanded="True"`)

### 3. Current Operation Progress Updates
- **Problem**: Progress bar was not updating because services report via `IProgress<string>` but we need to update both log and progress properties
- **Solution**: Wrapped all progress callbacks to update both:
  - `CurrentOperationText` - shows descriptive text
  - `CurrentOperationProgress` - shows percentage (0-100)
  - Log entries (via `AddLog`)

#### Progress Updates Added To:
- **ISO Volume Label Reading**: 50% progress
- **ISO Extraction**: 0-50% progress with text updates
- **Driver MSI Extraction**: 0-50% progress with text updates
- **Boot WIM Injection**: 
  - WinPE mount (25%), add drivers (50-75%), unmount (100%)
  - Setup mount (25%), add drivers (50-75%), unmount (100%)
- **Install WIM Injection**: 
  - Per-edition progress tracking (1/2, 2/2, etc.)
  - Mount (20%), add drivers (40%), WinRE processing (60-80%), unmount (90-100%)

### 4. ISO Label Usage
- Added `IsoVolumeLabel` property to MainViewModel (defaults to "WIN11USB")
- Created `GetIsoVolumeLabelAsync()` in IsoService
- Label is captured before ISO extraction
- USB drive is now formatted with ISO's original label instead of hardcoded "WIN11USB"

### 5. Workflow Improvements
- After driver injection completes, user is prompted: "Would you like to create USB now?"
- If Yes → automatic transition to USB creation workflow
- If No → user can manually click "Create USB" later
- Existing USB warning dialog still appears (warns about data erasure)

## Known Issues to Monitor

### Potential Stall/Hang
The screenshot shows the process stalled at "Step 4/6: Injecting drivers into boot images..." at 40%.

**Possible causes**:
1. DISM operations can take a very long time (10-20+ minutes per image)
2. Current operation progress may update slowly during actual driver injection
3. No timeout configured for DISM operations
4. Cancellation token may not be properly handled during long operations

**Recommendations**:
1. Monitor DISM logs during operations: `C:\Windows\Logs\DISM\dism.log`
2. Consider adding operation timeout warnings (e.g., "This step may take 15-20 minutes")
3. Consider adding more granular progress reporting from DISM output parsing
4. Test with smaller driver packages first to verify workflow

### Next Steps
1. Test collapsible log behavior
2. Verify current operation progress updates during actual run
3. Monitor for hangs/stalls during DISM operations
4. Consider adding timeout handling or progress indicators for long DISM operations
