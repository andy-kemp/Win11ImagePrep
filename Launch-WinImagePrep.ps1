# Launch WinImagePrep as Administrator
# This ensures all features work correctly

$exePath = Join-Path $PSScriptRoot "WinImagePrep\bin\Release\net8.0-windows\WinImagePrep.exe"

if (-not (Test-Path $exePath)) {
	Write-Host "ERROR: WinImagePrep.exe not found at:" -ForegroundColor Red
	Write-Host "  $exePath" -ForegroundColor Red
	Write-Host ""
	Write-Host "Please build the project first:" -ForegroundColor Yellow
	Write-Host "  dotnet build WinImagePrep/WinImagePrep.csproj --configuration Release" -ForegroundColor Yellow
	pause
	exit 1
}

Write-Host "Launching WinImagePrep as Administrator..." -ForegroundColor Green
Write-Host ""
Write-Host "NEW in this build:" -ForegroundColor Cyan
Write-Host "  ✓ Temp files now in AppData Local (auto-cleanup)" -ForegroundColor White
Write-Host "  ✓ Saved images protected in C:\WinImagePrep\SavedImages" -ForegroundColor White
Write-Host "  ✓ Better ISO mount timing (5s wait + verification)" -ForegroundColor White
Write-Host "  ✓ Improved error reporting" -ForegroundColor White
Write-Host ""

try {
	Start-Process -FilePath $exePath -Verb RunAs
	Write-Host "✓ Application started successfully!" -ForegroundColor Green
}
catch {
	Write-Host "ERROR: Failed to launch application" -ForegroundColor Red
	Write-Host $_.Exception.Message -ForegroundColor Red
	pause
}
