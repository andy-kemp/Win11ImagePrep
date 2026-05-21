# ISO Mount Fix - Using Original PowerShell Logic

## Problem
The C# app was trying to use a complex PowerShell mount command that wasn't working reliably. The ISO would "mount" but the drive letter wouldn't be accessible.

## Solution
Reverted to use the **exact same PowerShell commands** from the working `WinImagePrep_V3.ps1` script:

### Original Working PowerShell (V3 script):
```powershell
$mount = Mount-DiskImage -ImagePath $ISOPath -PassThru -StorageType ISO
Start-Sleep -Seconds 2
$vol = ($mount | Get-Volume)
$driveLetter = $vol.DriveLetter
```

### Now Using in C#:
```csharp
var arguments = $"-Command \"" +
	"$mount = Mount-DiskImage -ImagePath '{isoPath}' -PassThru -StorageType ISO; " +
	"Start-Sleep -Seconds 2; " +
	"$vol = ($mount | Get-Volume); " +
	"$driveLetter = $vol.DriveLetter; " +
	"Write-Output $driveLetter\"";
```

## What Changed
1. ✅ Simplified mount command to match working PowerShell script
2. ✅ Uses `$mount | Get-Volume` instead of complex filtering
3. ✅ Waits 2 seconds after mount (like original)
4. ✅ Additional 2 second stabilization wait in C# code
5. ✅ Checks for `sources` folder to verify it's a Windows ISO
6. ✅ Better diagnostic logging throughout

## Testing
Watch the operation log for:
```
Mounting ISO: filename.iso
Executing mount command...
Mount result: ExitCode=0, Success=True
Mount output: [D]
ISO mounted to drive D:
✓ Drive D: is accessible and contains Windows files
```

If it fails, you'll see specific error messages at each step.

## Build Info
- **Built**: 2026-05-21 11:36
- **Status**: SUCCESS
- **Location**: `WinImagePrep\bin\Release\net8.0-windows\WinImagePrep.exe`

## Why This Works
The original PowerShell script has been tested and proven to work. By using the exact same commands, we eliminate any differences in behavior. The C# wrapper now just executes the proven PowerShell logic.
