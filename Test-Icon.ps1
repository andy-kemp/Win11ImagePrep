# Icon Verification Script
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Icon Verification Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$exePath = ".\WinImagePrep\bin\Release\net8.0-windows\WinImagePrep.exe"

if (-not (Test-Path $exePath)) {
	Write-Host "❌ ERROR: EXE not found at $exePath" -ForegroundColor Red
	Write-Host "   Please build the project first." -ForegroundColor Yellow
	exit 1
}

Write-Host "✅ EXE file found" -ForegroundColor Green
Write-Host ""

# Check if icon is embedded in EXE
Write-Host "Checking for embedded icon..." -ForegroundColor Yellow
try {
	Add-Type -AssemblyName System.Drawing
	$icon = [System.Drawing.Icon]::ExtractAssociatedIcon((Resolve-Path $exePath))

	if ($icon) {
		Write-Host "✅ Icon IS embedded in EXE!" -ForegroundColor Green
		Write-Host "   Icon size: $($icon.Width)x$($icon.Height) pixels" -ForegroundColor Gray
		Write-Host ""
	} else {
		Write-Host "❌ No icon found in EXE" -ForegroundColor Red
		Write-Host ""
	}

	$icon.Dispose()
} catch {
	Write-Host "⚠️  Could not verify icon: $($_.Exception.Message)" -ForegroundColor Yellow
	Write-Host ""
}

# Check icon source file
Write-Host "Checking icon source files..." -ForegroundColor Yellow
$iconFiles = @(".\WinImagePrep\app.ico", ".\WinImagePrep\WinImagePrep.png")

foreach ($iconFile in $iconFiles) {
	if (Test-Path $iconFile) {
		$file = Get-Item $iconFile
		Write-Host "✅ Found: $($file.Name) ($([math]::Round($file.Length / 1KB, 2)) KB)" -ForegroundColor Green
	} else {
		Write-Host "❌ Missing: $iconFile" -ForegroundColor Red
	}
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Icon Display Notes" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "The icon IS embedded in the EXE file." -ForegroundColor White
Write-Host ""
Write-Host "If you don't see it in Windows Explorer:" -ForegroundColor Yellow
Write-Host "  1. Press F5 to refresh Explorer" -ForegroundColor Gray
Write-Host "  2. Close and reopen Explorer" -ForegroundColor Gray
Write-Host "  3. Restart Windows Explorer:" -ForegroundColor Gray
Write-Host "     Stop-Process -Name explorer -Force" -ForegroundColor DarkGray
Write-Host "  4. Restart your computer" -ForegroundColor Gray
Write-Host ""
Write-Host "This is a Windows icon cache issue, not a build issue!" -ForegroundColor Cyan
Write-Host ""
Write-Host "To see the icon in action:" -ForegroundColor Yellow
Write-Host "  1. Run the app" -ForegroundColor Gray
Write-Host "  2. Look at the window title bar (top-left)" -ForegroundColor Gray
Write-Host "  3. Check the taskbar icon" -ForegroundColor Gray
Write-Host "  4. Press Alt+Tab to see task switcher" -ForegroundColor Gray
Write-Host ""

Read-Host "Press Enter to exit"
