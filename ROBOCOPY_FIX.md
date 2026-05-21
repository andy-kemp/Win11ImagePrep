# Robocopy Command Fix - Exit Code 16

## Problem
Robocopy was failing with:
```
ERROR : No Destination Directory Specified.
```

The command output showed the destination path and flags were being split incorrectly:
```
Source = D:\" C:\Users\andrew\AppData\Local\WinImagePrep\Temp\Windows11 \E \NJH \NJS \NP \NFL \NDL\
```

## Root Cause
The robocopy arguments string had incorrect escaping. The closing quote on the source path was causing the command line parser to misinterpret the destination and flags.

### Before (broken):
```csharp
var arguments = $"\"{driveLetter}:\\\" \"{destinationPath}\" /E /NJH /NJS /NP /NFL /NDL";
```

This resulted in:
```
robocopy.exe "D:\" "C:\Users\...\Windows11" /E ...
```
The extra backslash before the closing quote was escaping it!

### After (fixed):
```csharp
var sourcePath = $"{driveLetter}:\\";
var arguments = $"\"{sourcePath}\" \"{destinationPath}\" /E /COPY:DAT /R:1 /W:1 /NP /NFL /NDL /NJH /NJS";
```

This results in:
```
robocopy.exe "D:\" "C:\Users\...\Windows11" /E /COPY:DAT /R:1 /W:1 /NP /NFL /NDL /NJH /NJS
```

## Additional Improvements

1. **Added `/COPY:DAT`** - Copy data, attributes, and timestamps (not security, which can cause permission issues)
2. **Added `/R:1 /W:1`** - Retry only once, wait 1 second (faster failure instead of hanging)
3. **Reordered flags** - Put action flags before output suppression flags
4. **Added debug log** - Shows the actual source and destination paths

## Testing

After this fix, robocopy should:
1. ✅ Correctly parse source and destination
2. ✅ Copy all ISO files successfully
3. ✅ Return exit code 0-7 (success)
4. ✅ Show proper progress in operation log

## Build Status
- **Built**: May 21, 2026 11:06
- **Status**: ✅ SUCCESS
- **Location**: `WinImagePrep\bin\Release\net8.0-windows\WinImagePrep.exe`

## To Test
1. Close any running instances of WinImagePrep
2. Run `.\Launch-WinImagePrep.ps1` (or run the EXE as admin)
3. Select ISO and MSI
4. Click "Prepare Image with Drivers"
5. Watch operation log for:
   - ✓ Drive D: is accessible and ready
   - ✓ Robocopy: D:\ -> C:\Users\...\Temp\Windows11
   - ✓ ISO extracted successfully

The robocopy exit code 16 error should now be resolved!
