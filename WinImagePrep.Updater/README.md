# WinImagePrep Updater

This is a standalone updater utility for WinImagePrep. It provides a better update experience with visible progress and reliable file replacement.

## How It Works

1. The main WinImagePrep application launches this updater when an update is available
2. The updater receives command-line arguments:
   - Target EXE path (the main app to update)
   - Download URL (where to get the new version)
   - Process name (to verify the app has closed)
   - Process ID (to wait for the specific instance)
3. The updater:
   - Waits for the main app to exit
   - Downloads the new version
   - Replaces the old EXE
   - Relaunches WinImagePrep
   - Shows progress throughout

## Building

```powershell
dotnet publish -c Release -r win-x64 --self-contained
```

The output will be in `bin\Release\net8.0-windows\win-x64\publish\WinImagePrep.Updater.exe`

## Deployment

The updater EXE must be placed in the same directory as WinImagePrep.exe when deploying the application.

For publishing, copy the updater to the main app's publish directory:

```powershell
# After building both projects
Copy-Item WinImagePrep.Updater\bin\Release\net8.0-windows\win-x64\publish\WinImagePrep.Updater.exe `
		  WinImagePrep\bin\Release\net8.0-windows\win-x64\publish\WinImagePrep.Updater.exe
```
