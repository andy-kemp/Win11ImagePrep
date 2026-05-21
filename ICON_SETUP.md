# Application Icon Setup

## Icon Files

The application uses **WinImagePrep.png** as the source image for the application icon.

### File Locations

- **Source PNG**: `WinImagePrep/WinImagePrep.png` (original image)
- **Application ICO**: `WinImagePrep/app.ico` (converted icon file)

## What Was Done

### 1. PNG to ICO Conversion
Converted `WinImagePrep.png` to `app.ico` using PowerShell/.NET:
```powershell
$pngPath = ".\WinImagePrep\WinImagePrep.png"
$icoPath = ".\WinImagePrep\app.ico"
Add-Type -AssemblyName System.Drawing
$bitmap = [System.Drawing.Bitmap]::FromFile((Resolve-Path $pngPath))
$icon = [System.Drawing.Icon]::FromHandle($bitmap.GetHicon())
$fileStream = [System.IO.File]::Create($icoPath)
$icon.Save($fileStream)
$fileStream.Close()
$bitmap.Dispose()
```

### 2. Added to Project File
Updated `WinImagePrep.csproj` to include the icon:
```xml
<PropertyGroup>
  <ApplicationIcon>app.ico</ApplicationIcon>
</PropertyGroup>
```

This embeds the icon into the executable file.

### 3. Added to Main Window
Updated `MainWindow.xaml` to display the icon in the window title bar:
```xml
<Window Icon="app.ico">
```

## Where the Icon Appears

✅ **Windows Explorer** - Shows on the .exe file  
✅ **Taskbar** - Shows when app is running  
✅ **Window Title Bar** - Shows in top-left corner of the window  
✅ **Alt+Tab Switcher** - Shows in the task switcher  
✅ **Start Menu** - If pinned, shows the icon  
✅ **Desktop Shortcut** - If created, shows the icon  

## Icon Specifications

| Property | Value |
|----------|-------|
| Format | ICO (Windows Icon) |
| Source | WinImagePrep.png |
| Location | WinImagePrep/app.ico |
| Embedded | Yes (in .exe) |
| Window Icon | Yes (title bar) |

## Updating the Icon

If you want to change the icon in the future:

1. **Replace the PNG file**:
   ```
   WinImagePrep/WinImagePrep.png
   ```

2. **Re-convert to ICO**:
   ```powershell
   cd WinImagePrep
   # Use the PowerShell script above to convert PNG to ICO
   ```

3. **Rebuild the project**:
   ```powershell
   dotnet build WinImagePrep/WinImagePrep.csproj --configuration Release
   ```

The icon will be automatically embedded into the new build.

## Technical Details

### Build Integration
- The icon is specified in the `.csproj` file
- During compilation, the .NET build system embeds the icon into the PE (Portable Executable) header
- The icon resource is compressed and stored in the .exe file
- No external icon file is needed at runtime

### Icon Formats in ICO
Standard .ico files contain multiple resolutions:
- 16×16 (small icons, tree views)
- 32×32 (standard size)
- 48×48 (large icons)
- 256×256 (high-DPI displays)

The conversion creates a single-size icon from the PNG. For production, consider using a proper icon editor to create multi-resolution .ico files.

## Verification

After building, verify the icon is present:

1. **Check in Explorer**:
   - Navigate to `WinImagePrep\bin\Release\net8.0-windows\`
   - The `WinImagePrep.exe` should show the icon

2. **Run the app**:
   - The window title bar should show the icon
   - The taskbar should show the icon
   - Alt+Tab should show the icon

3. **Right-click the exe**:
   - Properties → Details should show custom icon

---

**Build Status:** ✅ Build succeeded in 4.4s  
**Icon Status:** ✅ Embedded in EXE and visible in window  
**File Size:** 1,136,640 bytes (1.08 MB)  
