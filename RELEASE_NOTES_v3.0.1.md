# Release Notes - v3.0.1

## 🎉 Windows Image Preparation Tool v3.0.1

**Release Date:** May 22, 2026

---

## 🐛 Critical Bug Fix

### Mount Directory Cleanup Issue RESOLVED
**Fixed:** Temp folder accumulation reaching 36GB+ during driver injection

**The Problem:**
- Mount directories were not being properly deleted after DISM unmount operations
- Each edition mount (~25GB) + WinRE mount (~4.5GB) accumulated on disk
- Caused severe disk space issues and performance degradation
- Operations appeared to hang around 60% progress

**The Solution:**
- Implemented proper mount directory lifecycle management
- All mount directories now deleted immediately after unmount/commit
- Temp folder now stays lean during operations (~30GB peak, cleans up after each edition)
- Eliminates the 60% stall issue

---

## 📦 Downloads

### `WinImagePrep_full.exe` (72 MB)
**Self-contained, single-file executable**

✅ **No installation required**  
✅ **No .NET runtime required**  
✅ **No DLL dependencies**  
✅ **Just download and run**  

Compatible with:
- Windows 10 (20H2 or later) x64
- Windows 11 (all versions) x64

---

## 🔧 What's Included

### Driver Injection
- ✅ Injects drivers into WinPE (Index 1)
- ✅ Injects drivers into Windows Setup (Index 2)
- ✅ Injects drivers into all Windows editions (Pro, Enterprise, etc.)
- ✅ Injects drivers into WinRE (Recovery Environment) for each edition
- ✅ Automatic WIM splitting for FAT32 compatibility (>4GB files)

### USB Creation
- ✅ UEFI-compatible bootable USB drives
- ✅ FAT32 formatting with proper GPT partitioning
- ✅ USB volume label matches source ISO
- ✅ Automatic post-injection USB creation workflow

### User Experience
- ✅ Dual progress bars (Overall + Current Operation)
- ✅ Real-time operation log with collapsible view
- ✅ Warning prompts for long-running operations
- ✅ Non-resizable window with dynamic height
- ✅ Splash screen with branding during startup
- ✅ Automatic administrator privilege detection and elevation

---

## 📊 Performance Improvements

| Metric | Before | After |
|--------|--------|-------|
| **Peak Temp Folder Size** | 36GB+ | ~30GB |
| **Leftover Temp Files** | 124,900 files | 0 files |
| **Post-operation Cleanup** | Manual | Automatic |
| **60% Hang Issue** | Present | Resolved |
| **Estimated Runtime** | 60-90 min | 45-60 min |

---

## 🚀 Quick Start

1. **Download** `WinImagePrep_full.exe`
2. **Right-click** → **Run as Administrator**
3. **Select** Windows 11 ISO
4. **Select** Driver MSI package
5. **Click** "Prepare Image with Drivers"
6. **Wait** 45-60 minutes (monitor progress bars)
7. **Create USB** when prompted

---

## 📂 Directory Structure

The application creates the following structure at `C:\WinImagePrep\`:

```
C:\WinImagePrep\
├── SavedImages\       # Persistent: Prepared images for reuse
├── Logs\              # Persistent: Application logs
├── Config\            # Persistent: Configuration files
└── Temp\              # Temporary: Auto-cleaned during operations
	├── Windows11\     # Extracted ISO (deleted after operation)
	├── Drivers\       # Extracted MSI drivers (deleted after operation)
	└── Mount\         # DISM mount points (now properly cleaned!)
```

---

## 🔍 Technical Details

### Mount Directory Lifecycle (Fixed)

**Previous Behavior (BROKEN):**
```
Mount Edition_1 → Inject → Unmount → Leave 25GB on disk ❌
Mount WinRE_1   → Inject → Unmount → Leave 4.5GB on disk ❌
Mount Edition_2 → Inject → Unmount → Leave 25GB on disk ❌
Mount WinRE_2   → Inject → Unmount → Leave 4.5GB on disk ❌
Total: 59GB+ accumulated!
```

**Current Behavior (FIXED):**
```
Mount Edition_1 → Inject → Unmount → DELETE directory ✅
Mount WinRE_1   → Inject → Unmount → DELETE directory ✅
Mount Edition_2 → Inject → Unmount → DELETE directory ✅
Mount WinRE_2   → Inject → Unmount → DELETE directory ✅
Total: Clean slate after each operation!
```

### Code Changes
- Added `deleteMountDirectory` parameter to `DismService.UnmountWimAsync()`
- Set all unmount operations to `deleteMountDirectory: true`
- Mount directories now use `Directory.Delete(mountPath, true)` after successful unmount
- Applies to: WinPE, WinSetup, Edition mounts, and WinRE mounts

---

## ⚠️ System Requirements

### Minimum
- **OS:** Windows 10 (20H2+) or Windows 11 x64
- **RAM:** 8GB recommended
- **Disk:** 25GB free space on C: drive
- **USB:** 14GB+ for bootable media creation
- **Privileges:** Administrator rights required

### Software (Built-in)
- DISM (built into Windows)
- PowerShell (built into Windows)
- .NET 8 Runtime (bundled in `_full.exe`)

---

## 📝 Known Issues

None currently reported. Please submit issues to the [GitHub Issues](https://github.com/andy-kemp/Win11ImagePrep/issues) page.

---

## 🙏 Credits

Based on the original PowerShell implementation (`WinImagePrep_V2.ps1`), now rewritten as a native .NET 8 WPF application for improved reliability, performance, and user experience.

---

## 📄 License

MIT License - See [LICENSE](LICENSE) file for details

---

## 🔗 Links

- **GitHub Repository:** https://github.com/andy-kemp/Win11ImagePrep
- **Report Issues:** https://github.com/andy-kemp/Win11ImagePrep/issues
- **Documentation:** See [README.md](README.md)

---

**Full Changelog:** https://github.com/andy-kemp/Win11ImagePrep/compare/v3.0.0...v3.0.1
