# Teams Removal Fix - All Versions

## Problem
The Teams app was not being removed from the Windows 11 image. The issue affected both:
- **Consumer version**: MicrosoftTeams
- **Enterprise/Work version**: MSTeams (installed on Enterprise builds)

## Root Cause
The app removal logic had two issues:

1. **Incomplete package matching**: The code was passing short package names (like "MicrosoftTeams") directly to DISM, but DISM requires the full package name with version information (like "MicrosoftTeams_8wekyb3d8bbwe" or "MSTeams_8wekyb3d8bbwe").

2. **Missing Enterprise Teams**: The app list only included "MicrosoftTeams" (consumer), but Windows Enterprise builds include "MSTeams" which has a different package name.

## Solution

### 1. Fixed Package Name Matching (DismService.cs)
Updated `RemoveProvisionedAppsAsync` to:
- **Get all provisioned packages** from the mounted image using DISM
- **Match short names** from the UI against full package names (case-insensitive substring matching)
- **Remove matched packages** using their full names with version info
- **Report detailed progress** showing what was found and matched

This ensures that any variation of a package name will be found and removed, regardless of version numbers or exact naming.

### 2. Added Teams Enterprise Entry (MainViewModel.cs)
Added a second Teams entry to the Windows Apps list:
- **MicrosoftTeams**: "Microsoft Teams (Consumer)" - for consumer edition
- **MSTeams**: "Microsoft Teams (Work/School)" - for enterprise/work edition

Now users can select either or both versions to ensure complete Teams removal.

## How It Works Now

When you select Teams for removal:

1. The tool mounts the Windows image
2. Runs `DISM /Get-ProvisionedAppxPackages` to list ALL installed packages
3. For each selected app (e.g., "MicrosoftTeams", "MSTeams"):
   - Searches for matching full package names (e.g., finds "MSTeams_8wekyb3d8bbwe")
   - Reports how many matches were found
4. Removes each matched package using its full name
5. Reports success/failure for each removal

## Testing

To verify Teams removal:
1. Select both Teams entries in the app removal dialog
2. Run the image preparation process
3. Check the log output for:
   - "Scanning for apps to remove..."
   - "'MicrosoftTeams' matched X package(s)"
   - "'MSTeams' matched X package(s)"
   - "✓ Removed X of Y app(s)"

## Additional Benefits

This fix improves removal of ALL Windows apps, not just Teams:
- Handles version number variations automatically
- More resilient to package naming changes across Windows builds
- Better error reporting and logging
- Works with both Consumer and Enterprise Windows editions
