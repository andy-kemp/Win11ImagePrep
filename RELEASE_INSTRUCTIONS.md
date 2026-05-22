# Release v3.0.1 - Instructions

## ✅ What's Been Done

1. **Fixed Critical Bug**: Mount directory cleanup issue resolved
2. **Built Self-Contained EXE**: `WinImagePrep_full.exe` (69 MB)
3. **Updated README**: Added download and build instructions
4. **Created Release Notes**: Comprehensive `RELEASE_NOTES_v3.0.1.md`
5. **Committed & Pushed**: All changes pushed to GitHub
6. **Created Git Tag**: `v3.0.1` created and pushed

---

## 📦 Release Package Location

The release package is ready in the `Release\` folder:

```
Release\
├── WinImagePrep_full.exe     (69 MB - Self-contained executable)
├── README.md                 (Project documentation)
└── RELEASE_NOTES_v3.0.1.md   (Release notes)
```

---

## 🚀 Next Steps - Create GitHub Release

### Option 1: Via GitHub Web Interface (Recommended)

1. Go to: https://github.com/andy-kemp/Win11ImagePrep/releases/new

2. **Choose tag**: Select `v3.0.1` from dropdown

3. **Release title**: `v3.0.1 - Mount Directory Cleanup Fix`

4. **Description**: Copy/paste the contents of `RELEASE_NOTES_v3.0.1.md`

5. **Upload file**: 
   - Click "Attach binaries by dropping them here or selecting them"
   - Upload `Release\WinImagePrep_full.exe`

6. **Publish release**: Click "Publish release" button

### Option 2: Via GitHub CLI (If installed)

```powershell
cd Release
gh release create v3.0.1 `
	--title "v3.0.1 - Mount Directory Cleanup Fix" `
	--notes-file RELEASE_NOTES_v3.0.1.md `
	WinImagePrep_full.exe
```

---

## 📋 What Users Will See

When users visit your releases page, they'll see:
- **Tag**: v3.0.1
- **Title**: v3.0.1 - Mount Directory Cleanup Fix
- **Download**: `WinImagePrep_full.exe` (69 MB)
- **Release Notes**: Full formatted description of the bug fix and features
- **Assets**: Single executable file for download

---

## 🔍 Verification

You can verify the release is ready:

```powershell
# Check the tag exists
git tag -l v3.0.1

# Check the release package
Get-ChildItem Release\ | Format-Table Name, Length

# Check the executable
.\Release\WinImagePrep_full.exe
```

---

## 📝 Release Summary

**Version**: 3.0.1  
**Date**: May 22, 2026  
**Critical Fix**: Mount directory cleanup (36GB temp folder issue)  
**File**: WinImagePrep_full.exe (69 MB, self-contained)  
**Platforms**: Windows 10/11 x64  
**Dependencies**: None (fully self-contained)  

---

## 🎯 Key Features in This Release

✅ Fixed temp folder accumulation (36GB → ~30GB peak)  
✅ Resolved 60% progress hang  
✅ Proper mount directory lifecycle management  
✅ Self-contained single-file executable  
✅ No .NET installation required  
✅ Complete driver injection workflow  
✅ USB bootable media creation  
✅ Dual progress bars with real-time updates  
✅ Automatic cleanup after operations  

---

**The release is ready to publish on GitHub!** 🎉
