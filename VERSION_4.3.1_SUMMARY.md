# Version 4.3.1 - GitHub App List System

**Release Date**: January 6, 2026  
**Build**: 4.3.1.0

## Overview

This release introduces a **GitHub-based app list system** that dramatically improves the app-removal workflow by eliminating the need to scan ISOs every time.

## What's New

### ⚡ Instant App Loading (300x Faster!)

**Before v4.3.1:**
- Had to extract entire ISO (5-10 minutes)
- Mount WIM with DISM (2-3 minutes)
- Scan apps every time
- **Total: ~10 minutes**

**After v4.3.1:**
- Download from GitHub (<1 second)
- Automatic caching for offline use
- **Total: <1 second** ✨

### 🎯 Two Loading Methods

#### 1. Load Apps (Recommended - Green Button)
- Downloads curated list from GitHub
- Always up-to-date with latest Windows builds
- Falls back to cache if offline
- No admin rights required
- No ISO scanning needed

#### 2. Scan from ISO (Manual - Orange Button)
- For custom or modified ISOs
- Extracts and mounts WIM (slow)
- Auto-saves to cache for future use
- Requires administrator privileges

### 📋 Pre-Loaded App List

Includes 40+ common Windows 11 apps:
- Microsoft Teams (both versions)
- Clipchamp
- Cortana
- Xbox apps
- Bing News/Weather
- Office Hub
- Solitaire Collection
- And many more...

## New Files

### For End Users (in release package)
- Updated `WinImagePrep.exe` with new loading system
- `docs/APP_LIST_MANAGEMENT.md` - User documentation

### For Maintainers (in repository only)
- `app-list.json` - Curated Windows app list
- `tools/Update-AppList.ps1` - PowerShell script to update app list
- `tools/README.md` - Maintainer documentation
- `WinImagePrep/Services/AppListService.cs` - GitHub download service

## Technical Changes

### AppListService
New service with fallback chain:
1. Try GitHub (10-second timeout)
2. Try local cache (`%AppData%\Win11ImagePrep\Cache\app-list.json`)
3. Prompt user to scan from ISO

### UI Changes
- **Renamed**: "Load Apps from ISO" → "Load Apps" (green)
- **Added**: "Scan from ISO" button (orange)
- **Tooltip updates** to clarify fast vs. slow methods

### Auto-Load Integration
When "Remove Windows apps" is checked and user clicks "Prepare Image":
- App auto-loads from GitHub/cache (fast method)
- Opens selection dialog automatically
- Falls back to manual scan if needed

## Benefits

✅ **300x faster** - 10 minutes → <1 second  
✅ **Always up-to-date** - Centrally maintained by you  
✅ **Offline support** - Works from cache when no internet  
✅ **Flexible** - Manual ISO scan still available  
✅ **User-friendly** - Clear two-button UI  
✅ **Maintainable** - Simple JSON file on GitHub  

## Maintainer Workflow

### Updating the App List

When new Windows builds are released:

```powershell
# 1. Download latest Windows 11 ISO

# 2. Run admin script
cd tools
.\Update-AppList.ps1 -IsoPath "C:\ISOs\Win11_24H2.iso" -MergeWithExisting

# 3. Review and edit descriptions
code ..\app-list.json

# 4. Commit and push
git add ..\app-list.json
git commit -m "Update app list from Win11 24H2"
git push origin main

# 5. Done! All users get updates automatically
```

### Maintenance Schedule

**Update after**:
- Major Windows 11 releases (24H2, 23H2, etc.)
- User reports of missing apps
- New provisioned apps discovered

**Don't update for**:
- Monthly cumulative updates
- Security patches only
- Minor builds

## Cache Location

**End User Cache**:
```
%AppData%\Win11ImagePrep\Cache\app-list.json
```

**GitHub Source**:
```
https://raw.githubusercontent.com/andy-kemp/Win11ImagePrep/main/app-list.json
```

## Migration Notes

### For Existing Users
- No action needed - works automatically
- First "Load Apps" downloads from GitHub
- Subsequent loads use cache if available

### For Maintainers
- Admin tool (`Update-AppList.ps1`) is in repository only
- Not included in release packages
- Use for your own maintenance workflow

## Performance Comparison

| Method | Time | Network | Admin | Use Case |
|--------|------|---------|-------|----------|
| **Load Apps (GitHub)** | <1 sec | ✓ | ✗ | Most users, every time |
| **Load Apps (Cache)** | <0.1 sec | ✗ | ✗ | Offline or repeat use |
| **Scan from ISO** | 5-10 min | ✗ | ✓ | Custom ISOs, initial cache |

## Breaking Changes

None - fully backward compatible.

## Known Issues

None at release.

## Future Enhancements

Possible additions:
- Settings option to clear cache
- Manual cache location selection
- Version check for app list updates
- Option to skip GitHub and use cache-only mode

## Files Changed

### New Files
- `WinImagePrep/Services/AppListService.cs`
- `app-list.json`
- `tools/Update-AppList.ps1`
- `tools/README.md`
- `docs/APP_LIST_MANAGEMENT.md`

### Modified Files
- `WinImagePrep/ViewModels/MainViewModel.cs`
  - Added `AppListService` integration
  - Split `LoadAppsAsync()` and `ScanAppsFromIsoAsync()`
  - Auto-save scanned apps to cache
- `WinImagePrep/MainWindow.xaml`
  - Updated button layout
  - Changed button colors/text
  - Updated tooltips
- `WinImagePrep/WinImagePrep.csproj`
  - Version bump to 4.3.1

## Testing Checklist

- [x] GitHub download works
- [x] Cache fallback works
- [x] Offline mode works (cache only)
- [x] ISO scan still works
- [x] Scanned apps save to cache
- [x] Auto-load in prepare workflow
- [x] Admin script generates valid JSON
- [x] Merge mode preserves descriptions
- [x] UI buttons display correctly
- [x] Tooltips are accurate

## Credits

- **Idea**: Centralized GitHub app list vs. repeated ISO scanning
- **Implementation**: Full PowerShell admin tool + C# service integration
- **Documentation**: Complete user and maintainer guides

---

**Version**: 4.3.1  
**Previous**: 4.3.0 (Dynamic ISO app discovery)  
**Next**: TBD
