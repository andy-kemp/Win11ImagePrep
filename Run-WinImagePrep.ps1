# Windows Image Preparation Tool - Launch Script
Write-Host ""
Write-Host "========================================"  -ForegroundColor Cyan
Write-Host " Windows Image Preparation Tool"  -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$exePath = Join-Path $PSScriptRoot "WinImagePrep\bin\Release\net8.0-windows\WinImagePrep.exe"

if (-not (Test-Path $exePath)) {
	Write-Host "ERROR: Executable not found!" -ForegroundColor Red
	Write-Host "Expected location: $exePath" -ForegroundColor Yellow
	Write-Host ""
	Write-Host "Please build the project first:" -ForegroundColor Yellow
	Write-Host "  cd WinImagePrep" -ForegroundColor White
	Write-Host "  dotnet build --configuration Release" -ForegroundColor White
	Write-Host ""
	pause
	exit 1
}

Write-Host "Launching WinImagePrep as Administrator..." -ForegroundColor Green
Write-Host ""

try {
	Start-Process -FilePath $exePath -Verb RunAs
	Write-Host "Application launched successfully!" -ForegroundColor Green
	Write-Host ""
	Write-Host "Note: If the app doesn't appear, check Task Manager." -ForegroundColor Yellow
	Write-Host "      Press Ctrl+C here if you need to troubleshoot." -ForegroundColor Yellow
}
catch {
	Write-Host "ERROR: Failed to launch application!" -ForegroundColor Red
	Write-Host $_.Exception.Message -ForegroundColor Yellow
	Write-Host ""
	pause
}
