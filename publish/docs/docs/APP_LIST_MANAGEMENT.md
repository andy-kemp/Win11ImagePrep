# App List Management

## Overview
Win11ImagePrep now includes a **GitHub-based app list system** that eliminates the need to scan ISOs every time you want to remove apps.

## How It Works

### Three Loading Methods

1. **Load Apps** (Recommended - Fast! <1 second)
   - Downloads curated app list from GitHub
   - Always up-to-date with latest Windows builds
   - Falls back to cache if offline
   - No ISO scanning required

2. **Local Cache** (Automatic Fallback)
   - Saved to `%AppData%\Win11ImagePrep\Cache\app-list.json`
   - Used when GitHub is unavailable
   - Updated automatically when online

3. **Scan from ISO** (Manual - Slow, 5-10 minutes)
   - For custom or modified ISOs
   - Automatically saves to cache for future use
   - Extracts full ISO and mounts WIM

## For Users

### Quick Start
1. Check **"Remove Windows apps from image"**
2. Click **"Load Apps"** (green button)
3. Wait ~1 second for download
4. Click **"Select Apps to Remove..."**
5. Choose apps and proceed with image preparation

### When to Scan from ISO
- Custom Windows builds
- Modified ISOs
- Specific enterprise images
- When app list doesn't match your ISO

## For Maintainers

### Updating the App List

The app list is stored in `app-list.json` in the repository root:

```json
[
  {
	"packageName": "Microsoft.BingWeather",
	"displayName": "Weather",
	"description": "Weather application"
  }
]
```

### Adding New Apps
1. Edit `app-list.json`
2. Add entry with:
   - `packageName`: Full DISM package name
   - `displayName`: User-friendly name
   - `description`: Brief description
3. Commit and push
4. Users get update automatically on next "Load Apps"

### Finding Package Names
Run on a Windows 11 system:
```powershell
Get-AppxProvisionedPackage -Online | Select DisplayName, PackageName
```

Or mount your ISO and run:
```cmd
dism /Mount-Image /ImageFile:"D:\sources\install.wim" /Index:1 /MountDir:"C:\Mount"
dism /Image:"C:\Mount" /Get-ProvisionedAppxPackages
dism /Unmount-Image /MountDir:"C:\Mount" /Discard
```

### Cache Location
- **User**: `%AppData%\Win11ImagePrep\Cache\app-list.json`
- **Downloaded**: From `https://raw.githubusercontent.com/andy-kemp/Win11ImagePrep/main/app-list.json`

## Technical Details

### AppListService
Located in `WinImagePrep/Services/AppListService.cs`

**Load Order:**
1. Try GitHub (10-second timeout)
2. Try local cache
3. Return empty list (prompt user to scan)

**Methods:**
- `LoadAppListAsync()` - Main load with fallback chain
- `SaveScannedAppsAsync()` - Cache scan results
- `ClearCache()` - Reset cache (troubleshooting)

### Performance
| Method | Time | Network | Admin Required |
|--------|------|---------|----------------|
| Load Apps (GitHub) | <1 sec | Yes | No |
| Load Apps (Cache) | <0.1 sec | No | No |
| Scan from ISO | 5-10 min | No | Yes |

## Benefits

✅ **Speed**: 300x faster than ISO scanning  
✅ **Convenience**: No ISO mount/extract needed  
✅ **Up-to-date**: Centrally maintained list  
✅ **Offline**: Works from cache when no internet  
✅ **Flexible**: Manual scan still available  
✅ **User-friendly**: Clear UI with two buttons  

## Troubleshooting

### "No app list available"
1. Check internet connection
2. Verify GitHub is accessible
3. Try "Scan from ISO" to build local cache

### Apps don't match my ISO
- Use **"Scan from ISO"** for custom builds
- Scanned list saves to cache automatically

### Clear cache
Currently manual - delete:
```
%AppData%\Win11ImagePrep\Cache\app-list.json
```

Future: Add "Clear Cache" option in Settings
