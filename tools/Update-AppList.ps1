<#
.SYNOPSIS
	Admin tool to scan a Windows ISO and generate/update the GitHub app list.

.DESCRIPTION
	This script mounts a Windows ISO, extracts install.wim, mounts the first edition,
	scans all provisioned apps, and generates an updated app-list.json file for the repository.

.PARAMETER IsoPath
	Path to the Windows 11 ISO file to scan.

.PARAMETER OutputPath
	Path where the app-list.json will be saved. Default: ../app-list.json

.PARAMETER MergeWithExisting
	If specified, merges new apps with existing app-list.json (keeps descriptions).

.EXAMPLE
	.\Update-AppList.ps1 -IsoPath "C:\ISOs\Win11_23H2.iso"

.EXAMPLE
	.\Update-AppList.ps1 -IsoPath "C:\ISOs\Win11_24H2.iso" -MergeWithExisting

.NOTES
	Requires: Administrator privileges
	Duration: ~5-10 minutes
	Author: Andy Kemp
#>

[CmdletBinding()]
param(
	[Parameter(Mandatory = $true)]
	[ValidateScript({ Test-Path $_ -PathType Leaf })]
	[string]$IsoPath,

	[Parameter(Mandatory = $false)]
	[string]$OutputPath = "..\app-list.json",

	[Parameter(Mandatory = $false)]
	[switch]$MergeWithExisting
)

# Global error handling
$ErrorActionPreference = "Stop"
$script:tempRoot = $null
$script:tempMount = $null

# Cleanup function
function Cleanup {
	if ($script:tempRoot -and (Test-Path $script:tempRoot)) {
		Write-Host "`nCleaning up temp files..." -ForegroundColor Yellow
		try {
			if ($script:tempMount -and (Test-Path $script:tempMount)) {
				dism /Unmount-Wim /MountDir:"$script:tempMount" /Discard 2>&1 | Out-Null
			}
		} catch {}
		Remove-Item -Path $script:tempRoot -Recurse -Force -ErrorAction SilentlyContinue
	}
}

# Trap errors
trap {
	Write-Host "`n========================================" -ForegroundColor Red
	Write-Host "ERROR OCCURRED!" -ForegroundColor Red
	Write-Host "========================================" -ForegroundColor Red
	Write-Host $_.Exception.Message -ForegroundColor Red
	Write-Host "`nStack Trace:" -ForegroundColor Yellow
	Write-Host $_.ScriptStackTrace -ForegroundColor Gray
	Cleanup
	Write-Host "`nPress Enter to close..." -ForegroundColor Yellow
	Read-Host
	exit 1
}

# Check for admin privileges
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
	Write-Error "This script requires Administrator privileges. Please run as Administrator."
	Write-Host "`nPress Enter to close..." -ForegroundColor Yellow
	Read-Host
	exit 1
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Windows App List Generator" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Mount ISO
Write-Host "[1/7] Mounting ISO..." -ForegroundColor Yellow
$mountResult = Mount-DiskImage -ImagePath $IsoPath -PassThru -StorageType ISO
Start-Sleep -Seconds 3
$driveLetter = ($mountResult | Get-Volume).DriveLetter

if (-not $driveLetter) {
	Write-Error "Failed to mount ISO or get drive letter."
	exit 1
}

Write-Host "      ✓ ISO mounted to ${driveLetter}:\" -ForegroundColor Green

# Step 2: Extract ISO to temp folder
$script:tempRoot = "$env:TEMP\WinAppListGen_$([guid]::NewGuid().ToString('N'))"
$tempExtract = Join-Path $script:tempRoot "extract"
$script:tempMount = Join-Path $script:tempRoot "mount"

try {
	New-Item -ItemType Directory -Path $tempExtract -Force | Out-Null
	New-Item -ItemType Directory -Path $tempMount -Force | Out-Null
} catch {
	throw "Failed to create temp directories: $_"
}

Write-Host "[2/7] Extracting ISO contents to temp folder..." -ForegroundColor Yellow
Write-Host "      This may take 3-5 minutes..." -ForegroundColor Gray

$robocopyArgs = @(
	"${driveLetter}:\",
	$tempExtract,
	"/E",
	"/COPY:DAT",
	"/R:1",
	"/W:1",
	"/NJH",
	"/NJS",
	"/NDL",
	"/NC",
	"/NS"
)

$robocopyResult = Start-Process -FilePath "robocopy.exe" -ArgumentList $robocopyArgs -Wait -NoNewWindow -PassThru

if ($robocopyResult.ExitCode -ge 8) {
	Dismount-DiskImage -ImagePath $IsoPath | Out-Null
	throw "Failed to extract ISO contents (robocopy exit code: $($robocopyResult.ExitCode))"
}

Write-Host "      ✓ ISO extracted" -ForegroundColor Green

# Step 3: Dismount ISO (we have local copy now)
Write-Host "[3/7] Dismounting ISO..." -ForegroundColor Yellow
Dismount-DiskImage -ImagePath $IsoPath | Out-Null
Write-Host "      ✓ ISO dismounted" -ForegroundColor Green

# Step 4: Find install.wim
Write-Host "[4/7] Locating install.wim..." -ForegroundColor Yellow
$wimPath = Join-Path $tempExtract "sources\install.wim"
if (-not (Test-Path $wimPath)) {
	$wimPath = Join-Path $tempExtract "sources\install.esd"
	if (-not (Test-Path $wimPath)) {
		Cleanup
		throw "install.wim or install.esd not found in ISO."
	}
}
Write-Host "      ✓ Found: $(Split-Path $wimPath -Leaf)" -ForegroundColor Green

# Step 5: Get WIM info and mount first edition
Write-Host "[5/7] Reading Windows editions..." -ForegroundColor Yellow
$wimInfo = dism /Get-WimInfo /WimFile:"$wimPath"
$imageIndex = 1
Write-Host "      ✓ Using Index $imageIndex" -ForegroundColor Green

Write-Host "[6/7] Mounting Windows image (this may take 2-3 minutes)..." -ForegroundColor Yellow
$dismMount = dism /Mount-Wim /WimFile:"$wimPath" /Index:$imageIndex /MountDir:"$tempMount" /ReadOnly
if ($LASTEXITCODE -ne 0) {
	Cleanup
	throw "Failed to mount WIM image (DISM exit code: $LASTEXITCODE)"
}
Write-Host "      ✓ Image mounted" -ForegroundColor Green

# Step 6: Get provisioned apps
Write-Host "[7/7] Scanning provisioned apps..." -ForegroundColor Yellow
$dismOutput = dism /Image:"$tempMount" /Get-ProvisionedAppxPackages

# Parse DISM output
$apps = @()
$currentApp = @{}

foreach ($line in $dismOutput) {
	$line = $line.Trim()

	if ($line -match "^DisplayName\s*:\s*(.+)$") {
		if ($currentApp.Count -gt 0) {
			$apps += [PSCustomObject]$currentApp
		}
		$currentApp = @{
			DisplayName = $matches[1]
			PackageName = ""
			Version = ""
		}
	}
	elseif ($line -match "^PackageName\s*:\s*(.+)$") {
		$currentApp.PackageName = $matches[1]
	}
	elseif ($line -match "^Version\s*:\s*(.+)$") {
		$currentApp.Version = $matches[1]
	}
}

# Add last app
if ($currentApp.Count -gt 0) {
	$apps += [PSCustomObject]$currentApp
}

Write-Host "      ✓ Found $($apps.Count) provisioned apps" -ForegroundColor Green

# Step 7: Unmount and cleanup
Write-Host ""
Write-Host "Cleaning up..." -ForegroundColor Yellow
Cleanup
Write-Host "✓ Cleanup complete" -ForegroundColor Green

# Step 8: Generate JSON
Write-Host ""
Write-Host "Generating app list JSON..." -ForegroundColor Yellow

# Load existing if merging
$existingApps = @{}
if ($MergeWithExisting -and (Test-Path $OutputPath)) {
	Write-Host "Loading existing app-list.json..." -ForegroundColor Gray
	$existing = Get-Content $OutputPath -Raw | ConvertFrom-Json
	foreach ($app in $existing) {
		$existingApps[$app.packageName] = $app
	}
	Write-Host "✓ Loaded $($existing.Count) existing apps" -ForegroundColor Green
}

# Build new list
$newAppList = @()
foreach ($app in ($apps | Sort-Object DisplayName)) {
	$packageName = $app.PackageName

	# Check if we have existing description
	if ($existingApps.ContainsKey($packageName)) {
		$newAppList += [PSCustomObject]@{
			packageName = $packageName
			displayName = $existingApps[$packageName].displayName
			description = $existingApps[$packageName].description
		}
		Write-Host "  • $($app.DisplayName) (kept existing description)" -ForegroundColor Gray
	}
	else {
		# Generate friendly name
		$displayName = $app.DisplayName
		if ($displayName -match '\.([^.]+)$') {
			$displayName = $matches[1]
		}

		$newAppList += [PSCustomObject]@{
			packageName = $packageName
			displayName = $displayName
			description = "Windows provisioned app"
		}
		Write-Host "  + $($app.DisplayName) (NEW)" -ForegroundColor Cyan
	}
}

# Save JSON
$json = $newAppList | ConvertTo-Json -Depth 10
$json | Set-Content -Path $OutputPath -Encoding UTF8

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "✓ SUCCESS!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "App list saved to: $OutputPath" -ForegroundColor White
Write-Host "Total apps: $($newAppList.Count)" -ForegroundColor White
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Review the generated file" -ForegroundColor Gray
Write-Host "  2. Edit descriptions as needed" -ForegroundColor Gray
Write-Host "  3. Commit and push to GitHub:" -ForegroundColor Gray
Write-Host "     git add app-list.json" -ForegroundColor DarkGray
Write-Host "     git commit -m `"Update app list from [ISO name]`"" -ForegroundColor DarkGray
Write-Host "     git push origin main" -ForegroundColor DarkGray
Write-Host ""
Write-Host "Press Enter to close..." -ForegroundColor Yellow
Read-Host
