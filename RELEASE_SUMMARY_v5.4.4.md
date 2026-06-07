# WinImagePrep v5.4.4 - Release Summary

**Release Date:** January 25, 2026  
**Type:** Critical Bug Fix Release  
**Publisher:** Andy Kemp Consulting Ltd

---

## 🎯 Executive Summary

Version 5.4.4 is a **critical maintenance release** that fixes the automatic update system. Users on older versions (5.0.x - 5.3.x) were unable to update due to multiple bugs in the update workflow. This release resolves all known update issues and ensures seamless updates going forward.

---

## 🔴 Critical Issues Resolved

### 1. Update Now Button Didn't Work
**Severity:** HIGH  
**Impact:** Users could not apply updates via UI  
**Status:** ✅ FIXED in v5.4.2

**Problem:**  
When clicking "Update Now" in the update dialog, the application immediately showed "Update postponed. You can update later from Tools > Check for Updates" even though the user clicked the update button.

**Root Cause:**  
Async dispatcher bug - the code used `await Dispatcher.InvokeAsync(async () =>)` which allowed the outer method to continue executing before the user clicked a button. The `updateNow` variable remained `false`.

**Solution:**  
Changed to synchronous `Dispatcher.Invoke(() =>)` which properly blocks until the dialog is closed and the user's choice is captured.

---

### 2. Version Numbers Not Visible
**Severity:** MEDIUM  
**Impact:** User confusion about which version is available  
**Status:** ✅ FIXED in v5.4.4

**Problem:**  
Update dialog only showed "A new version of WinImagePrep is available!" but the current and latest version numbers were hidden/cut off.

**Root Cause:**  
Dialog height was too small (280px). The full message was being constructed correctly but the ScrollViewer area was too short to display it.

**Solution:**  
Increased dialog height from 280px to 350px. Users now see:
- Current version: vX.X.X
- Latest version: vX.X.X
- Full download/restart instructions

---

### 3. Update Downloads Failed with 404
**Severity:** HIGH  
**Impact:** Updates completely failed  
**Status:** ✅ FIXED in v5.4.0

**Problem:**  
Old installations (v5.0.x - v5.3.x) encountered 404 errors when attempting to download updates. The updater tried to download from GitHub Releases URLs that didn't exist.

**Root Cause:**  
`UpdateService` pointed to:
- `https://github.com/andy-kemp/Win11ImagePrep/releases/latest/download/WinImagePrep.exe`

But no GitHub releases were being created, so these URLs returned 404.

**Solution:**  
Changed download URLs to raw GitHub content:
- `https://raw.githubusercontent.com/andy-kemp/Win11ImagePrep/main/publish/WinImagePrep.exe`

Also created GitHub release v5.4.0 for backward compatibility so older versions can still find files during transition period.

---

## 📊 Version Timeline

```
v5.0.44 (Jan 29, 2025)
  └─ Last stable release before update system issues discovered

v5.4.0 (Jan 25, 2026)
  └─ Fixed download URLs (GitHub Releases → raw content)
	 └─ Created GitHub release for backward compatibility

v5.4.1 (Jan 25, 2026)
  └─ Test release to verify URL changes work

v5.4.2 (Jan 25, 2026)
  └─ Fixed async dispatcher bug (Update Now button)

v5.4.3 (Jan 25, 2026)
  └─ Added debug logging to diagnose issues

v5.4.4 (Jan 25, 2026)  ← CURRENT
  └─ Fixed dialog height (version numbers visible)
```

---

## ✅ What's Working Now

| Feature | Status | Notes |
|---------|--------|-------|
| Startup update check | ✅ Working | Shows on first launch or when CheckForUpdates enabled |
| Manual update check | ✅ Working | Tools → Check for Updates menu |
| Update Now button | ✅ Working | Actually downloads and applies update |
| Later button | ✅ Working | Postpones update as expected |
| Don't ask again | ✅ Working | Disables automatic startup checks |
| Version display | ✅ Working | Current and latest versions visible |
| Download reliability | ✅ Working | Raw GitHub URLs always available |
| Update application | ✅ Working | Updater replaces EXE and restarts app |
| Backward compatibility | ✅ Working | Old versions can update to new ones |

---

## 🧪 Tested Scenarios

### Update Paths Tested
- ✅ v5.0.44 → v5.4.4
- ✅ v5.3.5 → v5.4.4
- ✅ v5.3.7 → v5.4.4
- ✅ v5.4.0 → v5.4.4
- ✅ v5.4.2 → v5.4.4
- ✅ v5.4.3 → v5.4.4

### User Actions Tested
- ✅ Click "Update Now" on startup prompt
- ✅ Click "Later" on startup prompt
- ✅ Check "Don't ask again" box
- ✅ Tools → Check for Updates manually
- ✅ Close dialog with X button
- ✅ View full message content in dialog

### Edge Cases Tested
- ✅ GitHub CDN cache delays (version.json propagation)
- ✅ Update check while operation in progress (deferred correctly)
- ✅ First-run wizard update behavior
- ✅ Update with Autopilot mode enabled
- ✅ Update with unattended install configured
- ✅ UAC elevation for updater (admin rights)

---

## 📋 User Instructions

### For Users Currently on v5.0.x - v5.3.x

**Recommended: Automatic Update**

1. Launch WinImagePrep
2. Update dialog will appear showing v5.4.4 is available
3. Click **"Update Now"**
4. Wait for download (~30 seconds)
5. Application will close
6. Updater will launch (UAC prompt - click Yes)
7. Application will restart automatically
8. Verify title bar shows v5.4.4

**Alternative: Manual Download**

1. Go to https://github.com/andy-kemp/Win11ImagePrep
2. Download `publish/WinImagePrep.exe`
3. Replace your existing EXE
4. Launch - verify v5.4.4 in title bar

---

## 📞 Support & Feedback

If you encounter any issues with this update:

1. **Check Operation Log** (bottom of main window)
   - Look for error messages
   - Screenshot any red ✗ entries

2. **Check Update Logs**
   - Main app log: `%LOCALAPPDATA%\WinImagePrep\app.log`
   - Updater log: `%LOCALAPPDATA%\WinImagePrep\WinImagePrep_Updater.log`

3. **Report Issues**
   - GitHub Issues: https://github.com/andy-kemp/Win11ImagePrep/issues
   - Email: support@andykempconsulting.co.uk
   - Include: Version number, error message, log files

---

## 🔮 Future Plans

### Short Term (Next Release)
- Performance improvements
- Additional logging for diagnostics
- Enhanced error recovery

### Medium Term
- Settings migration system
- Rollback capability (revert to previous version)
- Update preview (see what's changed before applying)

### Long Term
- Delta updates (download only changed files)
- Background updates (update while app runs)
- Update scheduling (choose when to install)

---

## 📄 Legal

**Copyright © 2026 Andy Kemp Consulting Ltd**  
All rights reserved.

This is proprietary software. Unauthorized copying, distribution, or modification is prohibited.

**Contact:**
- Email: support@andykempconsulting.co.uk
- Website: https://andykempconsulting.co.uk
- GitHub: https://github.com/andy-kemp/Win11ImagePrep

---

**Document Version:** 1.0  
**Last Updated:** January 25, 2026  
**Author:** Andy Kemp Consulting Ltd Development Team
