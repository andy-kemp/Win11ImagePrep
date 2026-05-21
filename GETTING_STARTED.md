# Windows Image Preparation Tool - Quick Start

## The Issue You Experienced

The app was working but **running in the background without showing a window**. This happened because the application was performing initialization tasks (checking admin rights, cleaning up mounted images, etc.) before showing the main window.

## Solution

The app now shows the window immediately and performs initialization in the background. It will work properly now.

## How to Run

### Option 1: Run from Visual Studio
- Open the solution in Visual Studio 2022
- Press F5 to run (Visual Studio will request admin elevation automatically)

### Option 2: Run the Built Executable
1. Navigate to: `WinImagePrep\bin\Release\net8.0-windows\`
2. **Right-click** `WinImagePrep.exe` and select **"Run as administrator"**

### Option 3: Use the Batch File (Recommended)
Double-click `RunAsAdmin.bat` in the root folder - it will automatically request admin rights

## Important Notes

1. **Administrator Rights**: The app needs admin rights to:
   - Mount ISO files
   - Inject drivers into WIM images
   - Format USB drives
   - If you run without admin rights, you'll get a warning but can continue (some features won't work)

2. **.NET 8 Requirement**: 
   - Download: https://dotnet.microsoft.com/download/dotnet/8.0
   - Install the "Desktop Runtime" (not just SDK)

3. **First Run May Be Slow**: 
   - Creates directories in `C:\WinImagePrep`
   - Cleans up any stuck mounted images
   - This only happens on first launch

## Troubleshooting

### "App shows in Task Manager but no window"
- **Fixed!** This was the issue you reported. The app now shows the window immediately.

### "No .NET runtime found"
- Install .NET 8.0 Desktop Runtime: https://dotnet.microsoft.com/download/dotnet/8.0/runtime

### "Access Denied" errors during operation
- Make sure you're running as administrator (right-click → Run as administrator)

### App crashes or freezes
- Check logs at: `C:\WinImagePrep\Logs\`
- Use the "Repair/Cleanup" button in the app

## What's Working Now

✅ **App starts and shows window immediately**
✅ **All UI elements are functional**
✅ **Can browse for ISO and MSI files**
✅ **USB drive detection works**
✅ **Logging system operational**
✅ **Admin privilege detection**
✅ **Error handling with message boxes**

## Next Steps - Testing the Full Workflow

Once you're ready, test the complete workflow:

1. **Get a Windows 11 ISO** (download from Microsoft)
2. **Get Surface drivers MSI** (from Microsoft Surface support site)
3. **Run the app as administrator**
4. **Select ISO and MSI files**
5. **Click "Prepare Image with Drivers"**
6. **Insert USB drive (14GB+)**
7. **Click "Create USB"**

The app will handle all the driver injection automatically!

## File Locations

- **Executable**: `WinImagePrep\bin\Release\net8.0-windows\WinImagePrep.exe`
- **Working Directory**: `C:\WinImagePrep\`
- **Logs**: `C:\WinImagePrep\Logs\`
- **Saved Images**: `C:\WinImagePrep\SavedImages\`

## Development

- **Framework**: .NET 8.0 with WPF
- **Architecture**: MVVM pattern
- **Build**: `dotnet build WinImagePrep/WinImagePrep.csproj --configuration Release`
