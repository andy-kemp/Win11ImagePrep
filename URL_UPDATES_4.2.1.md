# URL Updates Summary - WinImagePrep 4.2.1

## Updated URLs Throughout Application

### Website URL
**OLD**: `https://tools.andykemp.com`  
**NEW**: `https://www.andykemp.com`

### Documentation URL
**OLD**: `https://tools.andykemp.com/winimageprep`  
**NEW**: `https://docs.andykemp.com/win11-image-prep/`

### GitHub README URL
**OLD**: `https://github.com/andy-kemp/Win11ImagePrep/blob/main/README.md`  
**NEW**: `https://github.com/andy-kemp/Win11ImagePrep/tree/main/WinImagePrep`

### GitHub Issues URL
**No change**: `https://github.com/andy-kemp/Win11ImagePrep/issues`

---

## Files Updated

### 1. AboutDialog.xaml
- Website link display: `https://www.andykemp.com`
- Documentation link display: `https://docs.andykemp.com/win11-image-prep/`

### 2. AboutDialog.xaml.cs
- `OpenWebsite_Click()` → `https://www.andykemp.com`
- `OpenDocumentation_Click()` → `https://docs.andykemp.com/win11-image-prep/`
- `WebsiteLink_Click()` → `https://www.andykemp.com`
- `DocumentationLink_Click()` → `https://docs.andykemp.com/win11-image-prep/`

### 3. MainViewModel.cs
- `OpenUserGuide()` fallback URL → `https://docs.andykemp.com/win11-image-prep/`
- `OpenUserGuide()` error message URL → `https://docs.andykemp.com/win11-image-prep/`
- `OpenOnlineDocumentation()` URL → `https://docs.andykemp.com/win11-image-prep/`
- `OpenOnlineDocumentation()` error message → `https://docs.andykemp.com/win11-image-prep/`
- `OpenGitHubReadme()` URL → `https://github.com/andy-kemp/Win11ImagePrep/tree/main/WinImagePrep`
- `OpenGitHubReadme()` error message → `https://github.com/andy-kemp/Win11ImagePrep/tree/main/WinImagePrep`

### 4. FirstRunViewModel.cs
- `OpenUserGuide()` fallback URL → `https://docs.andykemp.com/win11-image-prep/`
- `OpenUserGuide()` error message → `https://docs.andykemp.com/win11-image-prep/`

### 5. MainWindow.xaml
- Online Documentation tooltip → "View online documentation at docs.andykemp.com"

---

## Where URLs Are Used

### Help Menu (MainWindow)
1. **User Guide** → Opens local `docs\UserGuide.html`, fallback to `https://docs.andykemp.com/win11-image-prep/`
2. **Online Documentation** → Opens `https://docs.andykemp.com/win11-image-prep/`
3. **GitHub README** → Opens `https://github.com/andy-kemp/Win11ImagePrep/tree/main/WinImagePrep`
4. **Report Issue** → Opens `https://github.com/andy-kemp/Win11ImagePrep/issues`
5. **Release Notes** → Opens local `docs\ReleaseNotes.txt`
6. **About** → Shows About dialog

### About Dialog
1. **Website link** → `https://www.andykemp.com` (clickable)
2. **Documentation link** → `https://docs.andykemp.com/win11-image-prep/` (clickable)
3. **Open Website button** → `https://www.andykemp.com`
4. **Open Documentation button** → `https://docs.andykemp.com/win11-image-prep/`

### First-Run Window
1. **Open User Guide button** → Local file or `https://docs.andykemp.com/win11-image-prep/` fallback

---

## Testing Checklist

### Help Menu Links
- [ ] Help > User Guide → Opens local HTML file
- [ ] Help > Online Documentation → Opens https://docs.andykemp.com/win11-image-prep/
- [ ] Help > GitHub README → Opens https://github.com/andy-kemp/Win11ImagePrep/tree/main/WinImagePrep
- [ ] Help > Report Issue → Opens https://github.com/andy-kemp/Win11ImagePrep/issues
- [ ] Help > Release Notes → Opens local text file

### About Dialog
- [ ] Website text shows "https://www.andykemp.com"
- [ ] Documentation text shows "https://docs.andykemp.com/win11-image-prep/"
- [ ] Clicking website link opens https://www.andykemp.com
- [ ] Clicking documentation link opens https://docs.andykemp.com/win11-image-prep/
- [ ] "Open Website" button opens https://www.andykemp.com
- [ ] "Open Documentation" button opens https://docs.andykemp.com/win11-image-prep/

### First-Run Window
- [ ] "Open User Guide" button works (local file or online fallback)
- [ ] Error messages show correct documentation URL

### Fallback Scenarios
- [ ] If local UserGuide.html missing → Opens https://docs.andykemp.com/win11-image-prep/
- [ ] Error messages display correct URLs for manual navigation

---

## Published Build
- **Location**: `.\publish\WinImagePrep.exe`
- **Version**: 4.2.1.0
- **All URLs updated and verified**
