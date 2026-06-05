# WinImagePrep Build and Publish Script with Updater
# This script builds both the main app and the updater, then combines them for deployment

param(
	[string]$Version = "5.0.22",
	[switch]$SkipTests
)

$ErrorActionPreference = "Stop"

Write-Host "Building WinImagePrep v$Version with Updater" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Build and publish the Updater
Write-Host "Step 1: Building WinImagePrep.Updater..." -ForegroundColor Yellow
Push-Location WinImagePrep.Updater
try {
	dotnet publish -c Release -r win-x64 --self-contained
	if ($LASTEXITCODE -ne 0) {
		throw "Updater build failed"
	}
	Write-Host "✓ Updater built successfully" -ForegroundColor Green
}
finally {
	Pop-Location
}

Write-Host ""

# Step 2: Build and publish the main app
Write-Host "Step 2: Building WinImagePrep..." -ForegroundColor Yellow
Push-Location WinImagePrep
try {
	dotnet publish -c Release -r win-x64 --self-contained
	if ($LASTEXITCODE -ne 0) {
		throw "Main app build failed"
	}
	Write-Host "✓ Main app built successfully" -ForegroundColor Green
}
finally {
	Pop-Location
}

Write-Host ""

# Step 3: Copy updater to main app's publish directory
Write-Host "Step 3: Copying updater to publish directory..." -ForegroundColor Yellow
$updaterSource = "WinImagePrep.Updater\bin\Release\net8.0-windows\win-x64\publish\WinImagePrep.Updater.exe"
$publishDir = "WinImagePrep\bin\Release\net8.0-windows\win-x64\publish"
$updaterDest = Join-Path $publishDir "WinImagePrep.Updater.exe"

if (-not (Test-Path $updaterSource)) {
	throw "Updater EXE not found at: $updaterSource"
}

if (-not (Test-Path $publishDir)) {
	throw "Publish directory not found at: $publishDir"
}

Copy-Item $updaterSource $updaterDest -Force
Write-Host "✓ Updater copied to publish directory" -ForegroundColor Green

Write-Host ""

# Step 4: Copy documentation
Write-Host "Step 4: Copying documentation..." -ForegroundColor Yellow
$docsSource = "docs"
$docsDest = Join-Path $publishDir "docs"

if (Test-Path $docsSource) {
	if (-not (Test-Path $docsDest)) {
		New-Item -ItemType Directory -Path $docsDest -Force | Out-Null
	}

	Copy-Item "$docsSource\*" $docsDest -Recurse -Force
	Write-Host "✓ Documentation copied" -ForegroundColor Green
} else {
	Write-Host "⚠ Documentation directory not found - skipping" -ForegroundColor Yellow
}

# Also copy root docs
$rootDocs = @("README.md", "CHANGELOG.md")
foreach ($doc in $rootDocs) {
	if (Test-Path $doc) {
		Copy-Item $doc $publishDir -Force
		Write-Host "  ✓ Copied $doc" -ForegroundColor Gray
	}
}

Write-Host ""

# Step 5: Display publish summary
Write-Host "Publish Summary" -ForegroundColor Cyan
Write-Host "===============" -ForegroundColor Cyan
$mainExe = Join-Path $publishDir "WinImagePrep.exe"
$updaterExe = $updaterDest

if (Test-Path $mainExe) {
	$mainSize = (Get-Item $mainExe).Length / 1MB
	Write-Host "Main EXE:    $mainSize MB" -ForegroundColor White
}

if (Test-Path $updaterExe) {
	$updaterSize = (Get-Item $updaterExe).Length / 1MB
	Write-Host "Updater EXE: $updaterSize MB" -ForegroundColor White
}

Write-Host ""
Write-Host "Publish location: $publishDir" -ForegroundColor Green
Write-Host ""
Write-Host "✓ Build complete! Ready for deployment." -ForegroundColor Green
