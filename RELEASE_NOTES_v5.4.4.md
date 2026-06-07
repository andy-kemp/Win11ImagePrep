# Release Notes - WinImagePrep v5.4.4

**Release Date:** January 25, 2026  
**Publisher:** Andy Kemp Consulting Ltd

---

## 🎯 Critical Update - Automatic Update System Fixed

This release resolves critical issues with the automatic update system that were preventing updates from being applied correctly.

---

## 🔧 What's Fixed

### Critical Fixes

#### ✅ **Update Now Button Actually Works**
- **Problem:** Clicking "Update Now" would show "Update postponed" instead of downloading
- **Root Cause:** Async dispatcher bug caused code to continue before user clicked button
- **Solution:** Changed from `InvokeAsync(async)` to `Invoke()` for proper blocking behavior
- **Impact:** Users can now successfully update from Tools menu or startup prompt

#### ✅ **Version Numbers Now Visible in Update Dialog**
- **Problem:** Update dialog only showed first line of message, hiding version info
- **Solution:** Increased dialog height from 280px to 350px
- **Impact:** Users now see:
  - Current version number
  - Latest available version
  - Full update instructions

#### ✅ **Update URLs Fixed for Reliable Downloads**
- **Problem:** Old installations couldn't update due to 404 errors on release URLs
- **Solution:** Changed from GitHub Releases URLs to raw GitHub content URLs
- **Impact:** All versions can now download updates successfully

---

## 📊 Version History (This Release Cycle)

### v5.4.4 - Current Release
- 🔧 Increased update dialog height to show full message with version numbers

### v5.4.3 - Debug Release
- 🔍 Added detailed logging to diagnose update dialog behavior
- 📋 Operation log now shows update flow progress

### v5.4.2 - Update Fix Attempt
- 🔧 Changed async dispatcher pattern (partial fix)
- ⚠️ Message still cut off due to dialog height

### v5.4.1 - Update Infrastructure Test
- ✅ Verified new download URLs work correctly
- 📦 GitHub release created for backward compatibility

### v5.4.0 - URL Migration
- 🔧 CRITICAL FIX: Update download URLs changed to raw GitHub files
- 📁 Fixed 404 errors from missing GitHub releases
- 🔗 Created v5.4.0 GitHub release with updater binaries

---

## 🚀 Update Process

### For Users on Older Versions (5.0.x - 5.3.x)

1. **Automatic Update (Recommended)**
   - Launch WinImagePrep
   - Dialog will appear showing update is available
   - Click "Update Now"
   - Application will close, download, and restart automatically

2. **Manual Update (If Automatic Fails)**
   - Go to Tools → Check for Updates
   - Click "Update Now" when prompted
   - Follow on-screen instructions

3. **Manual Download (Last Resort)**
   - Download latest `WinImagePrep.exe` from GitHub
   - Replace existing executable
   - No installation required (single-file app)

### What Happens During Update

1. **Download Phase** (~30 seconds)
   - Main app downloads updater (~68MB)
   - Main app downloads new version (~72MB)
   - Files saved to temp directory

2. **Apply Phase** (~10 seconds)
   - Main app closes
   - Updater launches with admin rights (UAC prompt)
   - Old EXE replaced with new version
   - Updater restarts main app
   - Cleanup of temp files

3. **Verification**
   - New version launches
   - Title bar shows updated version number
   - Operation log confirms successful update

---

## 🧪 Testing Performed

### Scenarios Tested
- ✅ First-run update check (startup)
- ✅ Manual update check (Tools menu)
- ✅ Update from v5.0.44 → v5.4.4
- ✅ Update from v5.3.5 → v5.4.4
- ✅ Update with Autopilot mode enabled
- ✅ Update with unattended install configured
- ✅ Update dialog message display and formatting
- ✅ "Update Now" button behavior
- ✅ "Later" button behavior
- ✅ "Don't check for updates automatically" option
- ✅ GitHub CDN cache propagation

### Known Working Upgrade Paths
- v5.0.44 → v5.4.4 ✅
- v5.3.5 → v5.4.4 ✅
- v5.3.7 → v5.4.4 ✅
- v5.4.0 → v5.4.4 ✅
- v5.4.1 → v5.4.4 ✅
- v5.4.2 → v5.4.4 ✅
- v5.4.3 → v5.4.4 ✅

---

## 📝 Technical Details

### Update Service Changes

**Previous (Broken):**
```csharp
await Application.Current.Dispatcher.InvokeAsync(async () =>
{
	await Task.Delay(500);
	var dialog = new UpdatePromptDialog(message);
	var dialogResult = dialog.ShowDialog();
	updateNow = dialog.UpdateNow && dialogResult == true;
});
// updateNow is ALWAYS false here - code continues before user clicks!
```

**Current (Fixed):**
```csharp
Application.Current.Dispatcher.Invoke(() =>
{
	var dialog = new UpdatePromptDialog(message);
	var dialogResult = dialog.ShowDialog();
	updateNow = dialog.UpdateNow && dialogResult == true;
});
// updateNow correctly reflects user's choice - code waits for dialog
```

### URL Changes

**Old (404 Errors):**
- `https://github.com/andy-kemp/Win11ImagePrep/releases/latest/download/WinImagePrep.exe`
- `https://github.com/andy-kemp/Win11ImagePrep/releases/latest/download/WinImagePrep.Updater.exe`

**New (Reliable):**
- `https://raw.githubusercontent.com/andy-kemp/Win11ImagePrep/main/publish/WinImagePrep.exe`
- `https://raw.githubusercontent.com/andy-kemp/Win11ImagePrep/main/publish/WinImagePrep.Updater.exe`

### Dialog Layout Changes

**Previous:**
- Height: 280px
- Message area: Too small, text cut off
- Only first line visible ("A new version of WinImagePrep is available!")

**Current:**
- Height: 350px
- Message area: Full message visible with scrolling
- All version info and instructions displayed

---

## 🐛 Bug Fixes Summary

| Issue | Impact | Status |
|-------|--------|--------|
| Update Now button doesn't work | HIGH - Updates impossible via UI | ✅ FIXED |
| Version numbers not visible | MEDIUM - User confusion | ✅ FIXED |
| 404 errors on update download | HIGH - Updates fail completely | ✅ FIXED |
| Update postponed shows too early | MEDIUM - Confusing UX | ✅ FIXED |

---

## ⚠️ Known Issues

None at this time. All critical update issues have been resolved.

---

## 📞 Support

For issues, questions, or feedback:
- **GitHub Issues:** https://github.com/andy-kemp/Win11ImagePrep/issues
- **Email:** support@andykempconsulting.co.uk
- **Company:** Andy Kemp Consulting Ltd

---

## 📄 License

Proprietary software by Andy Kemp Consulting Ltd.  
All rights reserved.

---

**Previous Release:** [v5.0.44 - Updater Batch Script Fix](RELEASE_NOTES_v5.0.44.md)  
**Next Release:** TBD
