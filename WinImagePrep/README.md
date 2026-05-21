# Windows Image Preparation Tool

A professional Windows desktop application for creating customized Windows 11 installation media with injected drivers, specifically designed for Surface devices but compatible with most hardware.

## Features

- **ISO Management**: Mount, validate, and extract Windows 11 ISO files
- **Driver Injection**: Extract drivers from MSI files and inject them into Windows images
- **Multiple Image Support**: Process specific Windows editions or all editions at once
- **Bootable USB Creation**: Create bootable USB drives from prepared images
- **Saved Image Management**: Save prepared images and reuse them later
- **Progress Tracking**: Real-time progress monitoring with cancellation support
- **Comprehensive Logging**: Both UI and file-based logging for troubleshooting
- **Error Recovery**: Automatic cleanup of mounted images and error handling

## System Requirements

- **Operating System**: Windows 10 or Windows 11
- **Framework**: .NET 8.0 Runtime
- **Permissions**: Administrator privileges required
- **Disk Space**: Minimum 25 GB free space recommended
- **USB Drive**: 14 GB or larger for bootable media creation

## Building from Source

### Prerequisites
- Visual Studio 2022 or later
- .NET 8.0 SDK
- Windows 10/11 SDK

### Build Steps
```bash
cd WinImagePrep
dotnet restore
dotnet build --configuration Release
```

The compiled application will be in `WinImagePrep\bin\Release\net8.0-windows\`

## Usage

1. **Run as Administrator** - Right-click WinImagePrep.exe and select "Run as administrator"
2. **Select Windows ISO** - Browse to your Windows 11 ISO file
3. **Select Driver MSI** - Browse to your Surface driver MSI file
4. **Prepare Image** - Click "Prepare Image with Drivers" to inject drivers
5. **Create USB** - Insert USB drive and click "Create USB" for bootable media

## Directory Structure

```
C:\WinImagePrep\
├── Windows11\          # Extracted ISO with injected drivers
├── Drivers\            # Extracted driver files
├── Mount\              # Temporary WIM mount points
├── SavedImages\        # Previously prepared images
└── Logs\               # Application logs
```

## Version

**Version 3.0.0** - Complete native Windows application
- Converted from PowerShell script to C# WPF application
- Enhanced UI with MVVM architecture
- Improved error handling and logging
- Multi-threaded operations with cancellation support
