# Settings Configuration Guide

## Overview
WinImagePrep uses a JSON configuration file to store application settings. The settings file is automatically created at:

```
C:\ProgramData\Win11ImagePrep\settings.json
```

## Configuration Options

### WorkingRoot
- **Type**: String (file path)
- **Default**: `C:\Win11ImagePrep`
- **Description**: Main directory where all ISO processing, driver extraction, and temporary files are stored
- **Requirements**:
  - Must be on a local fixed drive (not network, removable, or CD-ROM)
  - Must have at least 25 GB free space
  - Cannot be inside OneDrive
  - Must be writable
- **Example**: `"WorkingRoot": "D:\\ImagePrep"`

### DeleteTempFilesOnExit
- **Type**: Boolean
- **Default**: `true`
- **Description**: When true, temporary files are deleted when the application exits
- **Example**: `"DeleteTempFilesOnExit": false`

### LogLevel
- **Type**: String
- **Default**: `"Information"`
- **Options**: 
  - `"Minimal"` - Only critical errors
  - `"Information"` - Normal operation logging (recommended)
  - `"Verbose"` - Detailed debugging information
- **Example**: `"LogLevel": "Verbose"`

## Derived Paths

The following directories are automatically derived from `WorkingRoot`:

| Directory | Path | Purpose |
|-----------|------|---------|
| ISO Extraction | `{WorkingRoot}\Temp\Windows11` | Extracted Windows ISO files |
| Drivers | `{WorkingRoot}\Temp\Drivers` | Extracted driver files |
| Mount | `{WorkingRoot}\Temp\Mount` | WIM mount operations |
| Temp | `{WorkingRoot}\Temp` | Temporary working files |
| Saved Images | `{WorkingRoot}\SavedImages` | Final processed images |
| Logs | `{WorkingRoot}\Logs` | Application logs |

## Example Configuration

### Default Configuration
```json
{
  "WorkingRoot": "C:\\Win11ImagePrep",
  "DeleteTempFilesOnExit": true,
  "LogLevel": "Information"
}
```

### Custom Drive Configuration
```json
{
  "WorkingRoot": "E:\\Windows_Image_Workspace",
  "DeleteTempFilesOnExit": false,
  "LogLevel": "Verbose"
}
```

### Minimal Logging Configuration
```json
{
  "WorkingRoot": "D:\\Win11Prep",
  "DeleteTempFilesOnExit": true,
  "LogLevel": "Minimal"
}
```

## Changing Settings

### Through the Application (Recommended)
1. Open WinImagePrep
2. Go to **Tools > Options**
3. Modify settings in the Options dialog
4. Click **Validate** to check settings
5. Click **Save** to apply changes
6. Restart the application for changes to take effect

### Manual Editing
1. Close WinImagePrep completely
2. Open `C:\ProgramData\Win11ImagePrep\settings.json` in a text editor
3. Modify settings following JSON format
4. Save the file
5. Restart WinImagePrep
6. If settings are invalid, the application will prompt you to fix them

## Validation Rules

The application validates settings on startup:

- ✓ **Path Format**: Must be a valid absolute path
- ✓ **Drive Type**: Must be a fixed local drive
- ✓ **Free Space**: At least 25 GB available
- ✓ **Permissions**: Must be writable
- ✓ **OneDrive**: Must not be inside OneDrive folders
- ✓ **Network**: Cannot be a network share
- ✓ **Removable**: Cannot be on removable media

If validation fails, you'll be prompted to open the Options dialog to fix issues.

## Troubleshooting

### Settings File Not Found
- Application will automatically create `settings.json` with default values on first run
- Default working folder: `C:\Win11ImagePrep`

### Validation Errors
- Use **Tools > Options > Validate** to check settings
- Common issues:
  - Insufficient disk space
  - OneDrive folder selected
  - Network drive selected
  - No write permissions

### Reset to Defaults
1. Open **Tools > Options**
2. Click **Reset to Defaults**
3. Click **Save**
4. Restart the application

Or manually delete: `C:\ProgramData\Win11ImagePrep\settings.json`

### Migration from Previous Versions
If you were using the default `C:\WinImagePrep` folder:
- Your existing files remain in place
- Settings file is created in the new location
- First run will detect and use existing files
- You can change the working folder if desired

## Notes

- **Path Separators**: Use double backslashes (`\\`) in JSON for Windows paths
- **Case Sensitive**: Property names are case-sensitive in JSON
- **Backup**: Consider backing up `settings.json` before making manual edits
- **Administrator Rights**: Some operations require administrator privileges regardless of settings
- **Restart Required**: Changing `WorkingRoot` requires application restart to take full effect

## File Location

Settings are stored separately from the working folder:

```
C:\ProgramData\Win11ImagePrep\settings.json
```

This allows the settings to persist even if you change the working folder location.
