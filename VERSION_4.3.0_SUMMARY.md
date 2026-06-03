# WinImagePrep Version 4.3.0 - Release Summary

**Release Date:** January 2025  
**Publisher:** Andy Kemp Consulting Ltd  
**Build Status:** ✅ Successful (0 errors, 0 warnings)

---

## 🎯 What's New in 4.3.0

### Dynamic App Discovery from ISO

The biggest feature in this release is the ability to **automatically discover all provisioned apps** from your Windows 11 ISO, rather than relying on a hardcoded list.

#### Why This Matters:
- ✅ **Accurate:** Shows exactly what's in YOUR ISO (~47 apps)
- ✅ **Handles variants:** Correctly identifies MSTeams vs MicrosoftTeams
- ✅ **Future-proof:** Works with any Windows 11 build/edition
- ✅ **Reliable removal:** Uses full package names for guaranteed matches

### Fully Automated Workflow

**Old workflow** (manual):
1. Select ISO
2. Click "Load Apps from ISO"
3. Click "Select Apps to Remove..."
4. Check "Remove Windows apps"
5. Click "Prepare Image with Drivers"

**New workflow** (automated):
1. Select ISO
2. Check "Remove Windows apps"
3. Click "Prepare Image with Drivers"
4. **→ App automatically prompts to scan ISO and select apps!**

The tool now intelligently detects when you haven't loaded apps and offers to do it for you.

---

## 🔧 Technical Implementation

### New Components

1. **ProvisionedApp.cs** - New model class
   - `DisplayName` - Friendly name (e.g., "Microsoft.WindowsCalculator")
   - `PackageName` - Full package with version (e.g., "Microsoft.WindowsCalculator_11.2210.0.0_neutral_~_8wekyb3d8bbwe")
   - `Version` - Package version
   - `PublisherId` - Publisher identifier

2. **GetProvisionedAppsDetailedAsync()** - New DismService method
   - Parses DISM `/Get-ProvisionedAppxPackages` output
   - Extracts all metadata for each app
   - Returns rich `ProvisionedApp` objects

3. **LoadAppsFromIsoAsync()** - New MainViewModel method
   - Mounts ISO
   - Locates install.wim/install.esd
   - Mounts first Windows edition
   - Scans for provisioned apps
   - Populates WindowsApps collection
   - Cleanly unmounts everything
   - Full progress reporting

### Enhanced Logic

**RemoveProvisionedAppsAsync()** - Smart dual-mode removal:
- **Full package names** (from ISO scan) → Used directly, no re-scanning needed
- **Partial names** (legacy list) → Pattern matching against image
- Fully backward compatible

**InjectDriversAsync()** - Auto-prompt flow:
- Detects if "Remove Windows apps" is checked
- Checks if apps have been loaded
- Offers to scan ISO and open selection dialog
- Handles multiple scenarios gracefully
- Allows user to continue with default list or cancel

---

## 📝 Files Changed

### Code Files
- ✅ `WinImagePrep/Models/ProvisionedApp.cs` - NEW file
- ✅ `WinImagePrep/Services/DismService.cs` - Added GetProvisionedAppsDetailedAsync, updated RemoveProvisionedAppsAsync
- ✅ `WinImagePrep/ViewModels/MainViewModel.cs` - Added LoadAppsFromIsoAsync, auto-prompt logic in InjectDriversAsync
- ✅ `WinImagePrep/MainWindow.xaml` - Added green "Load Apps from ISO" button

### Version & Documentation
- ✅ `WinImagePrep/WinImagePrep.csproj` - Version 4.3.0
- ✅ `WinImagePrep/MainWindow.xaml` - Title "WinImagePrep v4.3.0"
- ✅ `WinImagePrep/AboutDialog.xaml` - Version 4.3.0
- ✅ `docs/ReleaseNotes.txt` - v4.3.0 section added
- ✅ `docs/UserGuide.html` - Updated app removal section
- ✅ `VERSION_4.3.0_SUMMARY.md` - This file

---

## 🎨 User Interface Changes

### New Button
**"Load Apps from ISO"** (green button)
- Location: Next to "Select Apps to Remove..." button
- Color: Green (#28A745) to indicate discovery/scan action
- Tooltip: "Scan the ISO and load all provisioned apps into the selection list"

### Automated Prompts
When "Remove Windows apps" is checked and user hasn't loaded apps:

**Prompt 1: Load Apps from ISO?**
- Yes → Scans ISO, opens app selector
- No → Uses default hardcoded list
- Cancel → Aborts, lets user do it manually

**Prompt 2: Select Apps?** (if apps loaded but none selected)
- Yes → Opens app selection dialog
- No → Disables app removal
- Cancel → Aborts workflow

**Prompt 3: No Apps Selected** (if dialog closed with 0 selections)
- Yes → Continue without app removal
- No → Aborts workflow

---

## 🧪 Testing Checklist

### Manual Tests

- [ ] **Load Apps Button**
  - [ ] Select a Windows 11 ISO
  - [ ] Click "Load Apps from ISO" (green button)
  - [ ] Verify ~47 apps appear in the list
  - [ ] Verify app names match ISO (check for MSTeams variants)
  - [ ] Check that mount/unmount happens cleanly

- [ ] **Automated Workflow - Fresh Start**
  - [ ] Select ISO
  - [ ] Check "Remove Windows apps"
  - [ ] Click "Prepare Image with Drivers"
  - [ ] Verify prompt: "Load apps from ISO now?"
  - [ ] Click Yes
  - [ ] Verify apps load and selection dialog opens
  - [ ] Select some apps
  - [ ] Verify workflow continues

- [ ] **Automated Workflow - Apps Already Loaded**
  - [ ] Load apps manually first
  - [ ] Don't select any
  - [ ] Click "Prepare Image with Drivers"
  - [ ] Verify prompt: "Select apps now?"
  - [ ] Select apps and continue

- [ ] **Backward Compatibility**
  - [ ] Don't load apps from ISO (use default list)
  - [ ] Select apps from default list
  - [ ] Run workflow
  - [ ] Verify apps still removed correctly

- [ ] **Edge Cases**
  - [ ] ISO scan fails → Offers to continue with default list
  - [ ] User cancels at each prompt → Workflow aborts cleanly
  - [ ] No apps selected → Offers to continue without removal

### Build & Publish

- [ ] `dotnet build` - Clean build with 0 errors
- [ ] `dotnet publish` - Standalone EXE created
- [ ] File properties show version 4.3.0.0
- [ ] App title shows "WinImagePrep v4.3.0"
- [ ] About dialog shows version 4.3.0

---

## 📊 Version Numbering Rationale

**4.3.0** (not 4.2.2 or 4.3.1) because:
- **4** = Major version (WinImagePrep v4 architecture)
- **3** = New minor feature (dynamic app discovery + automated workflow)
- **0** = First release of this feature set

This follows semantic versioning:
- Major.Minor.Patch
- New features increment Minor version
- Bug fixes increment Patch version

---

## 🚀 Deployment Notes

### Publishing Command
```powershell
dotnet publish WinImagePrep/WinImagePrep.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -o publish
```

### Output
- `publish/WinImagePrep.exe` (~90-100 MB standalone EXE)
- Includes all dependencies (no .NET runtime required)
- Administrator elevation required for DISM operations

### Installer (Inno Setup)
Use existing Inno Setup script with updated version:
```iss
#define MyAppVersion "4.3.0"
```

### Distribution
- Standalone EXE ready for direct distribution
- Optionally package with Inno Setup for installer
- Include `docs/` folder for local help files

---

## 📖 User-Facing Changes

### What Users Will Notice

1. **New green button** next to app removal controls
2. **Automatic prompts** guide them through app loading
3. **More apps available** (~47 instead of ~34 hardcoded)
4. **Accurate app names** matching their specific ISO
5. **Faster removal** (no re-scanning when using full package names)

### Breaking Changes
**None** - Fully backward compatible with existing configurations and workflows.

---

## 🐛 Known Issues / Limitations

1. **Mount time:** Loading apps from ISO takes 30-60 seconds (requires mount/unmount)
2. **Read-write mount:** Currently mounts in read-write mode (could be optimized to read-only in future)
3. **First edition only:** Scans only the first edition in install.wim (typically Pro/Enterprise)
   - All editions usually have the same provisioned apps, so this is acceptable
4. **No caching:** Apps are not cached between sessions (re-mount required each time)

### Future Enhancements (not in 4.3.0)
- Cache loaded apps to avoid re-mounting
- Read-only mount optimization
- Scan multiple editions and merge app lists
- App dependency warnings (e.g., removing Store)
- Pre-configured app removal profiles (Gaming, Business, Minimal)

---

## ✅ Testing Results

**Build:** ✅ Success (0 errors, 0 warnings)  
**Manual Testing:** ⏳ Ready for user acceptance testing  
**Performance:** ⏳ To be validated with real ISO

---

## 📞 Support Information

**Documentation:** https://docs.andykemp.com/win11-image-prep/  
**GitHub:** https://github.com/andy-kemp/Win11ImagePrep  
**Website:** https://www.andykemp.com  

---

**Next Steps:**
1. User acceptance testing with real Windows 11 ISO
2. Validate all ~47 apps load correctly
3. Test app removal workflow end-to-end
4. Package with Inno Setup as v4.3.0
5. Publish release
