# Quick wrapper to update app list with ARM64 packages
# Run this in an elevated PowerShell prompt

$IsoPath = "C:\Users\AndrewKemp\Downloads\en-gb_windows_11_business_editions_version_24h2_updated_may_2026_arm64_dvd_2fa9524e.iso"

# Check if ISO exists
if (-not (Test-Path $IsoPath)) {
	Write-Host "ERROR: ISO not found at: $IsoPath" -ForegroundColor Red
	Write-Host "Press Enter to close..." -ForegroundColor Yellow
	Read-Host
	exit 1
}

Write-Host "Starting ARM64 app list update..." -ForegroundColor Cyan
Write-Host "ISO: $IsoPath" -ForegroundColor Gray
Write-Host ""

# Navigate to tools directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location (Join-Path $scriptDir "tools")

# Run the update script with merge
.\Update-AppList.ps1 -IsoPath $IsoPath -MergeWithExisting

Write-Host ""
Write-Host "Script completed!" -ForegroundColor Green
Write-Host "Press Enter to close..." -ForegroundColor Yellow
Read-Host
