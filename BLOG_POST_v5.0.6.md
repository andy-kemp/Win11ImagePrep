# WinImagePrep v5.0.6: Enterprise-Ready Windows Deployment Made Easy

**Published by:** Andy Kemp Consulting Ltd  
**Date:** June 2026  
**Category:** Windows Deployment, System Administration, IT Tools  

---

## Introduction

Today I'm excited to announce **WinImagePrep v5.0.6**, a professional Windows 11 image preparation tool that makes creating custom deployment media incredibly simple. Whether you're a system administrator managing hundreds of devices or an IT professional deploying Surface hardware, WinImagePrep streamlines the entire process.

This release brings critical UI improvements alongside our recently launched **Autopilot Mode** feature, making it the perfect solution for modern enterprise deployments.

---

## What is WinImagePrep?

WinImagePrep is a native C# WPF application that allows you to:

- 🔧 **Inject drivers** directly into Windows 11 installation images
- 🗑️ **Remove bloatware** before deployment
- 🚀 **Create Autopilot-friendly** installation media
- ⚙️ **Configure unattended installations** with zero user interaction
- 💾 **Generate bootable USB drives** (UEFI-compatible, FAT32)
- 📦 **Save and reuse** customized images

---

## What's New in v5.0.6

### ✅ Fixed: Operation Log UI Issues

The most requested fix from v5.0.5 users:

- **Scrollbar now visible and working properly** – No more hunting for the scrollbar!
- **Proper collapse behavior** – Operation log collapses cleanly without leaving empty space
- **Responsive window sizing** – Window automatically resizes when expanding/collapsing the log
- **Better for smaller screens** – Perfect for Surface tablets and laptops

### Technical Details

The fix involved two key changes:
1. Changed the operation log grid row definition from `Height="Auto"` to `Height="*"` to properly fill available space
2. Replaced `MinHeight`/`MaxHeight` with a fixed `Height="200"` for consistent ScrollViewer behavior

Simple changes, but they make a huge difference in daily use!

---

## Feature Spotlight: Autopilot Mode (v5.0.5)

Since many readers might be discovering WinImagePrep for the first time, let me highlight our game-changing **Autopilot Mode** feature introduced in v5.0.5.

### The Problem

Traditional unattended Windows installations skip the entire OOBE (Out-of-Box Experience), which breaks Autopilot deployments. You lose:
- ❌ Company branding and logos
- ❌ Autopilot enrollment process
- ❌ "Let's set things up..." experience

Users also reported that privacy/telemetry screens (location, diagnostics, speech, inking) were still appearing even with automated installs.

### The Solution: Autopilot Mode

WinImagePrep v5.0.5+ includes a dedicated **"This device is enrolled in Autopilot"** option that:

- ✅ **Preserves OOBE** so company branding appears
- ✅ **Auto-accepts license** (skips EULA screen)
- ✅ **Auto-partitions disk** (perfect for reimage scenarios)
- ✅ **Skips privacy screens** using dual-method suppression (XML + registry keys)
- ✅ **No local admin creation** (Azure AD accounts only)
- ✅ **Keeps wireless setup enabled** (required for Azure AD join)
- ✅ **Smart UI** that hides irrelevant options

### How It Works

When you check the Autopilot Mode checkbox, the unattended configuration dialog automatically:
- Hides the local administrator account section
- Hides the computer name field (Autopilot manages this)
- Hides setup experience options (forced to Autopilot-friendly defaults)
- Forces sensible defaults automatically

The generated `autounattend.xml` file includes both XML-based OOBE settings AND registry commands via `FirstLogonCommands` to ensure privacy screens are suppressed across all Windows 11 builds.

### Real-World Use Case

**Scenario:** You're an IT admin managing 500 Autopilot-enrolled Surface devices. One device needs reimaging due to corruption.

**Traditional approach:**
1. Create standard Windows USB
2. Boot device
3. Manually click through license, disk selection, partitioning
4. Wait for OOBE... but company branding doesn't appear (broken Autopilot)
5. Manually click through privacy screens
6. Hope Autopilot re-enrollment works

**WinImagePrep with Autopilot Mode:**
1. Boot from USB
2. Everything automated: license accepted, disk wiped and partitioned
3. OOBE appears with company branding ✅
4. User connects to Wi-Fi
5. Autopilot enrollment proceeds normally
6. Apps/policies deploy via Intune
7. Done!

---

## Standard Unattended Installation

Not using Autopilot? No problem! WinImagePrep also supports **fully silent installations** with local administrator accounts:

- Complete automation from boot to desktop
- No OOBE screens whatsoever
- Creates local admin account with your credentials
- Optional computer name configuration
- Perfect for domain-joined environments

Just leave the "This device is enrolled in Autopilot" checkbox **unchecked**.

---

## Other Key Features

### 🗑️ Smart App Removal

- Apps automatically load from GitHub-hosted list
- **Quick Select** presets:
  - "Remove Bloatware Only" – Keeps useful apps like Calculator, Notepad, Paint
  - "Remove All Optional Apps" – Minimal installation
- Persistent selections across sessions
- Architecture-aware (handles both x64 and ARM64)

### 🔧 Driver Integration

- Supports MSI packages (Surface drivers, etc.)
- Supports extracted folders
- Supports ZIP archives
- Injects into:
  - WinPE (boot.wim index 1)
  - WinSetup (boot.wim index 2)
  - All Windows editions (install.wim)
  - WinRE (Recovery Environment)

### 💾 Bootable USB Creation

- UEFI-compatible FAT32 format
- Automatic WIM splitting for >4GB files
- Volume label preservation
- 14GB+ USB drives supported

### 📦 Save & Reuse Images

- Save prepared images for future use
- Quick USB creation from saved images
- No need to re-run full preparation
- Stored in `C:\ProgramData\Win11ImagePrep\SavedImages\`

---

## Getting Started

### Download

Download the latest self-contained executable from the [GitHub Releases](https://github.com/andy-kemp/Win11ImagePrep/releases) page:

- **`WinImagePrep.exe`** (68.75 MB) - Single file, includes .NET 8 runtime
- No installation required
- No dependencies needed
- Just download and run as Administrator

### Basic Workflow

1. **Run as Administrator** – Right-click and select "Run as administrator"
2. **Select Windows 11 ISO** – Click Browse, select your ISO
3. **(Optional) Select driver source** – MSI, folder, or ZIP
4. **(Optional) Configure app removal** – Choose which apps to remove
5. **(Optional) Enable unattended installation** – Autopilot or Standard mode
6. **Click "Prepare Image with Drivers"** – Takes 20-60 minutes
7. **Create bootable USB** – Select drive and click "Create USB"
8. **Deploy!**

### System Requirements

- Windows 10/11 (x64)
- Administrator privileges
- 25 GB free disk space
- Windows 11 ISO file
- USB drive (14 GB+) for bootable media

---

## Documentation

Complete documentation is available:

- **User Guide** – Comprehensive HTML guide in `docs/UserGuide.html`
- **Autopilot Mode Guide** – Detailed Autopilot documentation in `AUTOPILOT_MODE.md`
- **README** – Quick start and feature overview
- **CHANGELOG** – Complete version history from v3.0.0 to v5.0.6

All documentation is included in the download and also available on [GitHub](https://github.com/andy-kemp/Win11ImagePrep).

---

## Version History Highlights

- **v5.0.6** (June 2026) – Operation log UI fixes
- **v5.0.5** (June 2026) – Autopilot Mode, privacy screen suppression
- **v5.0.0** (June 2026) – Autopilot-friendly unattended installation
- **v4.5.0** (June 2026) – Unattended installation feature
- **v4.4.x** (May-June 2026) – Auto-update, persistent settings, UI polish
- **v4.3.0** (May 2026) – Dynamic app discovery from ISO
- **v4.0.0** (May 2026) – Complete C# WPF rewrite

---

## Why I Built WinImagePrep

As a consultant working with enterprise clients deploying Surface devices and managing Autopilot enrollments, I was frustrated by the lack of tools that could:

1. Inject Surface drivers into Windows images properly
2. Remove bloatware before deployment
3. Work seamlessly with Autopilot enrollments
4. Provide a clean, professional Windows GUI (not command-line scripts)

WinImagePrep solves all of these problems in one tool. It's the tool I wished existed when I started doing Windows deployments.

---

## Feedback & Support

WinImagePrep is developed and maintained by **Andy Kemp Consulting Ltd**.

- **Report Issues:** [GitHub Issues](https://github.com/andy-kemp/Win11ImagePrep/issues)
- **Feature Requests:** GitHub Issues
- **Updates:** Built-in auto-updater checks for new versions
- **Documentation:** Included in download and on GitHub

---

## What's Next?

I'm continuing to improve WinImagePrep based on user feedback. Some ideas for future releases:

- 📊 **Deployment analytics** – Track successful deployments
- 🔐 **BitLocker pre-provisioning** – Configure BitLocker during image prep
- 🌐 **Multi-language UI** – Support for additional languages
- 📱 **ARM64 optimizations** – Better support for ARM-based devices
- 🔄 **Batch operations** – Process multiple ISOs/driver packages

Have suggestions? [Open an issue on GitHub](https://github.com/andy-kemp/Win11ImagePrep/issues)!

---

## Conclusion

WinImagePrep v5.0.6 represents months of refinement based on real-world usage in enterprise environments. From the initial Autopilot Mode design to the latest UI polish, every feature has been battle-tested in production deployments.

Whether you're managing a handful of devices or thousands, WinImagePrep makes Windows deployment significantly easier. And it's completely free to use!

**Download WinImagePrep v5.0.6 today:** [GitHub Releases](https://github.com/andy-kemp/Win11ImagePrep/releases)

---

## About Andy Kemp Consulting Ltd

We specialize in Windows deployment solutions, Microsoft 365 consulting, and enterprise IT infrastructure. WinImagePrep is one of several tools we've developed to solve real-world IT challenges.

**Contact:**
- Website: https://tools.andykemp.com
- GitHub: https://github.com/andy-kemp
- LinkedIn: [Add your LinkedIn profile]

---

**Tags:** #Windows11 #Autopilot #Deployment #SystemAdministration #ITTools #Surface #Microsoft #PowerShell #CSharp #WPF #Enterprise

---

*Have you tried WinImagePrep? Share your deployment stories in the comments below!*
