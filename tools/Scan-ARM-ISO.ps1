# ARM64 Windows ISO Scanner
# Scans ARM64 ISO and merges with existing app-list.json

param([string]$IsoPath = "C:\Users\AndrewKemp\Downloads\en-gb_windows_11_business_editions_version_24h2_updated_may_2026_arm64_dvd_2fa9524e.iso")

if (-not (Test-Path $IsoPath)) {
    Write-Host "ERROR: ISO not found" -ForegroundColor Red
    Read-Host "Press Enter"
    exit
}

Write-Host "ARM64 App List Update Starting..." -ForegroundColor Cyan
Write-Host "This will take several minutes"  -ForegroundColor Yellow
Write-Host ""

$tempRoot = "$env:TEMP\ARM-Scan-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
$tempExtract = "$tempRoot\extract"
$tempMount = "$tempRoot\mount"

try {
    Write-Host "[1/6] Mounting ISO..."
    $mount = Mount-DiskImage -ImagePath $IsoPath -PassThru
    $drive = ($mount | Get-Volume).DriveLetter
    if (-not $drive) { throw "Failed to mount ISO" }
    Write-Host "  OK Mounted to $drive`:"
    
    Write-Host "[2/6] Extracting ISO..."
    New-Item -ItemType Directory -Path $tempExtract, $tempMount -Force | Out-Null
    $result = Start-Process robocopy -ArgumentList "$drive`:\", $tempExtract, "/E", "/R:1", "/W:1", "/NJH", "/NJS" -Wait -NoNewWindow -PassThru
    if ($result.ExitCode -ge 8) { throw "Extract failed" }
    Write-Host "  OK Extracted"
    
    Write-Host "[3/6] Dismounting ISO..."
    Dismount-DiskImage -ImagePath $IsoPath | Out-Null
    Write-Host "  OK Dismounted"
    
    Write-Host "[4/6] Mounting WIM..."
    $wim = "$tempExtract\sources\install.wim"
    if (-not (Test-Path $wim)) { throw "install.wim not found" }
    dism /Mount-Wim /WimFile:"$wim" /Index:1 /MountDir:"$tempMount" /ReadOnly | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "WIM mount failed" }
    Write-Host "  OK Mounted"
    
    Write-Host "[5/6] Scanning apps..."
    $output = dism /Image:"$tempMount" /Get-ProvisionedAppxPackages
    
    $apps = @()
    $current = @{}
    foreach ($line in $output) {
        if ($line -match "^DisplayName\s*:\s*(.+)") {
            if ($current.Count -gt 0) { $apps += [PSCustomObject]$current }
            $current = @{ DisplayName = $matches[1]; PackageName = "" }
        }
        elseif ($line -match "^PackageName\s*:\s*(.+)") {
            $current.PackageName = $matches[1]
        }
    }
    if ($current.Count -gt 0) { $apps += [PSCustomObject]$current }
    Write-Host "  OK Found $($apps.Count) apps"
    
    Write-Host "[6/6] Merging with existing..."
    $existing = @{}
    if (Test-Path "app-list.json") {
        (Get-Content "app-list.json" | ConvertFrom-Json) | ForEach-Object {
            $existing[$_.packageName] = $_
        }
    }
    
    $merged = @()
    foreach ($app in $apps) {
        $pkg = $app.PackageName
        if ($existing.ContainsKey($pkg)) {
            $merged += $existing[$pkg]
            Write-Host "  kept: $($app.DisplayName)" -ForegroundColor Gray
        } else {
            $name = if ($app.DisplayName -match '\.([^.]+)$') { $matches[1] } else { $app.DisplayName }
            $merged += [PSCustomObject]@{
                packageName = $pkg
                displayName = $name
                description = "Windows provisioned app"
            }
            Write-Host "  NEW: $($app.DisplayName)" -ForegroundColor Cyan
        }
    }
    
    foreach ($key in $existing.Keys) {
        if (-not ($merged.packageName -contains $key)) {
            $merged += $existing[$key]
        }
    }
    
    $merged | Sort-Object displayName | ConvertTo-Json -Depth 10 | Set-Content "app-list.json" -Encoding UTF8
    
    Write-Host ""
    Write-Host "SUCCESS!" -ForegroundColor Green
    Write-Host "Total apps: $($merged.Count)" -ForegroundColor White
    Write-Host ""
    Write-Host "Next: git add app-list.json && git commit && git push" -ForegroundColor Yellow
    
} catch {
    Write-Host ""
    Write-Host "ERROR: $_" -ForegroundColor Red
} finally {
    Write-Host ""
    Write-Host "Cleaning up..."
    try { dism /Unmount-Wim /MountDir:"$tempMount" /Discard 2>&1 | Out-Null } catch {}
    Remove-Item $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Done"
}

Write-Host ""
Read-Host "Press Enter to close"
