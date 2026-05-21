# Manual Cleanup Procedure

## Current Situation
A DISM unmount operation is running:
```
dism /Unmount-Wim /MountDir:C:\WinImagePrep\Temp\Mount\Edition_1 /Discard
```

## What This Does
- Unmounts the WIM image from `Edition_1` mount point
- `/Discard` flag = discards any uncommitted changes
- This is safe and will not affect your prepared images

## After Unmount Completes

### Step 1: Check for Any Other Mounted Images
```powershell
dism /Get-MountedImageInfo
```

If any images are still mounted, unmount them:
```powershell
dism /Unmount-Wim /MountDir:"<path>" /Discard
```

### Step 2: Clean the Temp Mount Directory
```powershell
Remove-Item -Path "C:\WinImagePrep\Temp\Mount" -Recurse -Force -ErrorAction SilentlyContinue
```

### Step 3: Optional - Clean Other Temp Directories
If you want to reclaim more space:
```powershell
# Clean extracted ISO files (if you're done creating USBs)
Remove-Item -Path "C:\WinImagePrep\Temp\Windows11" -Recurse -Force -ErrorAction SilentlyContinue

# Clean extracted drivers
Remove-Item -Path "C:\WinImagePrep\Temp\Drivers" -Recurse -Force -ErrorAction SilentlyContinue
```

### Step 4: Verify Space Reclaimed
```powershell
Get-ChildItem "C:\WinImagePrep\Temp" -Recurse | Measure-Object -Property Length -Sum | Select-Object @{Name="SizeGB";Expression={[math]::Round($_.Sum / 1GB, 2)}}
```

## What NOT to Delete
- **`C:\WinImagePrep\SavedImages\`** - Your prepared/saved images
- **`C:\WinImagePrep\Logs\`** - Application logs
- **`C:\WinImagePrep\Config\`** - Configuration files

## Using the App's Cleanup Button
Alternatively, once the unmount finishes, you can:
1. Close the application (if running)
2. Restart it
3. Click the **Repair/Cleanup** button
4. This will run the cleanup automatically

## Expected Results
After cleanup:
- `C:\WinImagePrep\Temp\Mount\` should be empty or not exist
- Disk space should drop from 37GB to < 1GB in the temp folder
- Application is ready for the next image preparation run

## The Fix Going Forward
The updated code now automatically deletes mount directories after each unmount, so this accumulation won't happen again. The temp folder should stay lean during future operations.
