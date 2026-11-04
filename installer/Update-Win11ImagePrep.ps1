<#
  Win11 Image Prep automatic update. Installed by Win11ImagePrep_Setup.exe into the app's Program Files folder and run
  by the scheduled task "\Andy Kemp Consulting\Win11 Image Prep update" as SYSTEM (daily, and after start-up).

  1. Reads https://downloads.andykemp.com/win11-image-prep/latest.json.
  2. If its version is newer than the installed one, downloads Win11ImagePrep_Setup.exe into the app's own Updates
     folder (only administrators and SYSTEM can write there) and checks its SHA-256 against latest.json.
  3. Runs it silently. The installer keeps the device's update choice.

  It does nothing while the app is open, unless the app asked for the update (Update now writes update-now.flag
  and closes). It does nothing if updates are managed by the organisation (ManagedUpdates = 1).
  Log: <install folder>\Logs\auto-update.log. As it runs as SYSTEM, it only writes under the install folder
  (administrators only), never under ProgramData, which users can write to.

  -Register / -Unregister create or remove the scheduled task (run by the installer and uninstaller). The task can be
  started by any signed-in user (that is how Update now works without a UAC prompt), but only runs this script.

  -Manifest <path> reads a local latest.json whose setup has a local "path" instead of a URL. Only for testing:
  the scheduled task never passes it, and only administrators can change the task.
#>
param([string]$Manifest, [switch]$Register, [switch]$Unregister)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$AppKey     = 'HKLM:\SOFTWARE\Andy Kemp Consulting\Win11 Image Prep'
$PolicyKey  = 'HKLM:\SOFTWARE\Policies\Andy Kemp Consulting\Win11 Image Prep'
$LatestUrl  = 'https://downloads.andykemp.com/win11-image-prep/latest.json'
$TaskPath   = '\Andy Kemp Consulting\'
$TaskName   = 'Win11 Image Prep update'
# Written by the app (Update now); only read here, never written or deleted as SYSTEM
$RequestFlag = Join-Path $env:ProgramData 'Win11ImagePrep\update-now.flag'
$LogFile    = $null

if ($Register) {
    $action = New-ScheduledTaskAction -Execute "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" `
        -Argument "-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$PSCommandPath`""
    $daily = New-ScheduledTaskTrigger -Daily -At '12:00' -RandomDelay (New-TimeSpan -Hours 4)
    $startup = New-ScheduledTaskTrigger -AtStartup
    $startup.Delay = 'PT10M'
    $settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -RunOnlyIfNetworkAvailable -AllowStartIfOnBatteries `
        -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Hours 1) -MultipleInstances IgnoreNew
    $principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
    Register-ScheduledTask -TaskPath $TaskPath -TaskName $TaskName -Action $action -Trigger $daily, $startup `
        -Settings $settings -Principal $principal -Description 'Keeps Win11 Image Prep up to date from downloads.andykemp.com' -Force | Out-Null
    # Let signed-in users start it (Update now in the app); only administrators can change it
    $svc = New-Object -ComObject 'Schedule.Service'
    $svc.Connect()
    $svc.GetFolder($TaskPath.TrimEnd('\')).GetTask($TaskName).SetSecurityDescriptor('D:(A;;FA;;;SY)(A;;FA;;;BA)(A;;GRGX;;;AU)', 0)
    Write-Output "Registered $TaskPath$TaskName"
    exit 0
}
if ($Unregister) {
    Unregister-ScheduledTask -TaskPath $TaskPath -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue
    try {
        $svc = New-Object -ComObject 'Schedule.Service'; $svc.Connect()
        $folder = $svc.GetFolder($TaskPath.TrimEnd('\'))
        if ($folder.GetTasks(1).Count -eq 0 -and $folder.GetFolders(0).Count -eq 0) { $svc.GetFolder('\').DeleteFolder($TaskPath.Trim('\'), 0) }
    } catch { }
    Write-Output "Removed $TaskPath$TaskName"
    exit 0
}

function Log([string]$Message) {
    if (-not $LogFile) { Write-Output $Message; return }
    try {
        New-Item -ItemType Directory -Force (Split-Path $LogFile) | Out-Null
        if ((Test-Path $LogFile) -and (Get-Item $LogFile).Length -gt 1MB) { Move-Item $LogFile "$LogFile.old" -Force }
        Add-Content -Path $LogFile -Value ("{0:yyyy-MM-dd HH:mm:ss}  {1}" -f (Get-Date), $Message)
    } catch { }
    Write-Output $Message
}

function Get-Dword($Path, $Name) {
    try { (Get-ItemProperty -Path $Path -Name $Name -ErrorAction Stop).$Name } catch { $null }
}

try {
    $installDir = (Get-ItemProperty -Path $AppKey -ErrorAction Stop).InstallLocation
    if (-not $installDir -or -not (Test-Path (Join-Path $installDir 'WinImagePrep.exe'))) {
        Log 'Win11 Image Prep is not installed here; nothing to do'
        exit 0
    }
    $LogFile = Join-Path $installDir 'Logs\auto-update.log'

    if ((Get-Dword $PolicyKey 'ManagedUpdates') -eq 1 -or (Get-Dword $AppKey 'ManagedUpdates') -eq 1) {
        Log 'Updates are managed by the organisation (ManagedUpdates = 1); nothing to do'
        exit 0
    }

    # Installed version: the EXE's own version (the source of truth), else the registry
    $exe = Join-Path $installDir 'WinImagePrep.exe'
    $installed = [version]'0.0.0'
    try {
        $fv = (Get-Item $exe).VersionInfo
        $installed = [version]::new($fv.FileMajorPart, $fv.FileMinorPart, $fv.FileBuildPart)
    } catch {
        try { $installed = [version](Get-ItemProperty -Path $AppKey).InstalledVersion } catch { }
    }

    # Latest version
    if ($Manifest) {
        $latest = Get-Content $Manifest -Raw | ConvertFrom-Json
    } else {
        $latest = Invoke-RestMethod -Uri ("{0}?t={1}" -f $LatestUrl, [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()) `
                                    -UserAgent 'Win11ImagePrep-AutoUpdate' -Headers @{ 'Cache-Control' = 'no-cache' } -TimeoutSec 60
    }
    $available = [version]$latest.version
    if ($available -le $installed) {
        Log "Up to date ($installed; latest is $available)"
        exit 0
    }
    if (-not $latest.setup -or $latest.setup.sha256 -notmatch '^[0-9a-fA-F]{64}$') {
        Log "Version $available is available, but latest.json has no installer; nothing to do"
        exit 0
    }

    # Don't close the app under someone who is using it, unless they asked for the update
    $requested = (Test-Path $RequestFlag) -and ((Get-Item $RequestFlag).LastWriteTime -gt (Get-Date).AddMinutes(-30))
    $running = Get-Process -Name 'WinImagePrep' -ErrorAction SilentlyContinue
    if ($running -and $requested) {
        # Update now: the app is closing; give it a moment
        for ($i = 0; $i -lt 30 -and (Get-Process -Name 'WinImagePrep' -ErrorAction SilentlyContinue); $i++) { Start-Sleep -Seconds 2 }
        $running = Get-Process -Name 'WinImagePrep' -ErrorAction SilentlyContinue
    }
    if ($running) {
        Log "Version $available is available, but the app is open; will try again later"
        exit 0
    }

    # Download into the app's Updates folder: under Program Files, so only administrators and SYSTEM can write there
    $updates = Join-Path $installDir 'Updates'
    New-Item -ItemType Directory -Force $updates | Out-Null
    $setup = Join-Path $updates 'Win11ImagePrep_Setup.exe'
    Remove-Item $setup -Force -ErrorAction SilentlyContinue
    if ($Manifest) {
        Copy-Item $latest.setup.path $setup
    } else {
        $url = [uri]$latest.setup.url
        if ($url.Scheme -ne 'https' -or $url.Host -ne 'downloads.andykemp.com') {
            Log "Refusing to download the installer from $url (only https://downloads.andykemp.com)"
            exit 1
        }
        Log "Downloading $available from $url"
        Invoke-WebRequest -Uri $url -OutFile $setup -UserAgent 'Win11ImagePrep-AutoUpdate' -TimeoutSec 600 -UseBasicParsing
    }

    $hash = (Get-FileHash $setup -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $latest.setup.sha256.ToLowerInvariant()) {
        Remove-Item $setup -Force -ErrorAction SilentlyContinue
        Log "The installer's SHA-256 ($hash) doesn't match latest.json ($($latest.setup.sha256)); not installed"
        exit 1
    }

    Log "Installing $available (was $installed)"
    $p = Start-Process -FilePath $setup -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=`"$(Join-Path $installDir 'Logs\auto-update-setup.log')`"" -Wait -PassThru
    Remove-Item $setup -Force -ErrorAction SilentlyContinue
    if ($p.ExitCode -ne 0) {
        Log "Setup finished with exit code $($p.ExitCode); see auto-update-setup.log"
        exit $p.ExitCode
    }
    Log "Updated to $available"
    exit 0
}
catch {
    Log "Update check failed: $($_.Exception.Message)"
    exit 1
}
