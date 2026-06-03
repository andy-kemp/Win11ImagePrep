# Quick Start: Using the New Settings System

## For End Users

### Opening Settings
1. Launch WinImagePrep
2. Click **Tools > Options** from the menu
3. The Options window opens

### Changing Working Folder
1. In the Options window, click **Browse...**
2. Select your desired folder (e.g., `D:\Win11Work`)
3. Click **Validate** to check if the folder is suitable
4. If validation passes, click **Save**
5. Restart the application for changes to take effect

### Understanding Derived Paths
When you change the Working Folder, these paths update automatically:
- **ISO Extraction**: `{WorkingFolder}\Temp\Windows11`
- **Drivers**: `{WorkingFolder}\Temp\Drivers`
- **Mount**: `{WorkingFolder}\Temp\Mount`
- **Temp**: `{WorkingFolder}\Temp`
- **Saved Images**: `{WorkingFolder}\SavedImages`
- **Logs**: `{WorkingFolder}\Logs`

### Requirements
Your chosen folder must:
- ✅ Be on a local fixed drive (not USB or network)
- ✅ Have at least 25 GB free space
- ✅ Not be inside OneDrive
- ✅ Be writable (you have permissions)

### Troubleshooting
**Validation Failed?**
- Check error message in Options window
- Common issues:
  - Not enough free space
  - Selected a USB drive
  - Selected a network location
  - Selected OneDrive folder

**Reset to Defaults?**
1. Open Tools > Options
2. Click **Reset to Defaults**
3. Click **Save**

## For Developers

### Accessing Settings in Code

```csharp
// Load settings
var settingsService = new SettingsService();
var settings = await settingsService.LoadSettingsAsync();

// Get working root
string workingRoot = settings.WorkingRoot;

// Get derived paths
string logsDir = settings.LogsDirectory;
string mountDir = settings.MountDirectory;

// Create AppConfiguration with settings
var config = new AppConfiguration(settings);
```

### Validating Settings

```csharp
var validationResult = await settingsService.ValidateSettingsAsync(settings);
if (!validationResult.IsValid)
{
	Console.WriteLine(validationResult.GetErrorMessages());
}
```

### Saving Settings

```csharp
settings.WorkingRoot = @"D:\NewPath";
var saved = await settingsService.SaveSettingsAsync(settings);
if (saved)
{
	await settingsService.CreateRequiredDirectoriesAsync(settings);
}
```

### Settings File Location

```
C:\ProgramData\Win11ImagePrep\settings.json
```

### Default Settings

```json
{
  "WorkingRoot": "C:\\Win11ImagePrep",
  "DeleteTempFilesOnExit": true,
  "LogLevel": "Information"
}
```

## Key Features

✅ **Automatic Creation**: Settings file created on first run
✅ **Validation**: Comprehensive checks before saving
✅ **Thread-Safe**: Safe for concurrent access
✅ **Atomic Writes**: No corruption from crashes
✅ **Fallback**: Uses defaults if settings fail to load
✅ **Logging**: All operations logged
✅ **UI**: User-friendly Options dialog
✅ **Backward Compatible**: Existing installations work as-is

## Architecture

```
App Startup
	↓
Load Settings (SettingsService)
	↓
Validate Configuration
	↓
Create Directories
	↓
Initialize MainViewModel
	↓
	Uses AppConfiguration (with settings)
	↓
All operations use configured paths
```

## Menu Structure

```
File
  └─ Save Configuration...
  └─ Open Configuration...
  └─ Exit

Tools
  └─ Options...          ← NEW

Help
  └─ Documentation
  └─ How to Use
  └─ Support / Report Issue
  └─ Check for Updates
  └─ About
```

## Questions?

See **SETTINGS_GUIDE.md** for complete documentation.
See **SETTINGS_IMPLEMENTATION_SUMMARY.md** for technical details.
