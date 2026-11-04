; Win11 Image Prep installer (Inno Setup 6). Built by .github/workflows/release.yml:
;   iscc /DAppVersion=5.9.11 /DSourceDir=..\out\app /DUpdaterDir=..\out\updater /O..\release installer\Win11ImagePrep.iss
;
; Installs for all users in C:\Program Files\Andy Kemp Consulting\Win11 Image Prep, with a Start menu shortcut.
;
; Silent install (Intune, Configuration Manager, a script):
;   Win11ImagePrep_Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
;
; Updates: by default a scheduled task (SYSTEM, daily and after start-up) installs new versions from
; downloads.andykemp.com silently, never while the app is open; the app's Update now starts it without a UAC prompt.
;   /MANAGEDUPDATES=1  no automatic or in-app updates: Intune (or similar) deploys new versions
;   /AUTOUPDATE=0      no scheduled task; the app still offers updates (the updater asks for administrator rights)
; Without these switches an upgrade keeps the device's current choice.
; Silent uninstall:
;   "C:\Program Files\Andy Kemp Consulting\Win11 Image Prep\unins000.exe" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
;
; Settings, saved images and logs stay in C:\ProgramData\Win11ImagePrep and are kept on uninstall.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\out\app"
#endif
#ifndef UpdaterDir
  #define UpdaterDir "..\out\updater"
#endif

#define AppName "Win11 Image Prep"
#define AppPublisher "Andy Kemp Consulting Ltd"
#define AppExe "WinImagePrep.exe"
; Also written by the app and updater: HKLM\SOFTWARE\Andy Kemp Consulting\Win11 Image Prep
#define AppKey "SOFTWARE\Andy Kemp Consulting\Win11 Image Prep"

[Setup]
; Never change AppId: it is how Windows and Intune recognise upgrades and the uninstall entry
AppId={{669DE22E-E42E-4D1B-AE68-1A81A43BCF12}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://www.andykemp.com/
AppSupportURL=https://docs.andykemp.com/win11-image-prep/
AppUpdatesURL=https://downloads.andykemp.com/win11-image-prep/
AppCopyright=Copyright © Andy Kemp Consulting Ltd
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} setup
VersionInfoProductName={#AppName}
DefaultDirName={autopf}\Andy Kemp Consulting\Win11 Image Prep
DisableDirPage=auto
DefaultGroupName=Andy Kemp Consulting
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputBaseFilename=Win11ImagePrep_Setup
SetupIconFile=..\WinImagePrep\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
; Close a running copy (Restart Manager) before replacing it
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\WinImagePrep.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#UpdaterDir}\WinImagePrep.Updater.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Update-Win11ImagePrep.ps1"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; Comment: "Build Windows 11 install media with your drivers"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Where the app is installed; the app and updater keep InstalledVersion up to date after an in-app update
Root: HKLM; Subkey: "{#AppKey}"; ValueType: string; ValueName: "InstallLocation"; ValueData: "{app}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "{#AppKey}"; ValueType: string; ValueName: "InstalledVersion"; ValueData: "{#AppVersion}"
; /MANAGEDUPDATES=1: updates come from Intune (or similar), so the app doesn't check or update itself.
; /MANAGEDUPDATES=0 turns its own updates back on. Without the switch, an upgrade keeps the current choice.
Root: HKLM; Subkey: "{#AppKey}"; ValueType: dword; ValueName: "ManagedUpdates"; ValueData: "{param:MANAGEDUPDATES|0}"; Check: ManagedUpdatesGiven
; 1: the scheduled task installs updates automatically
Root: HKLM; Subkey: "{#AppKey}"; ValueType: dword; ValueName: "AutoUpdate"; ValueData: "{code:AutoUpdateValue}"

[InstallDelete]
; Left by an in-app update (the previous version, renamed)
Type: files; Name: "{app}\{#AppExe}.old"
Type: files; Name: "{app}\{#AppExe}.new"

[UninstallDelete]
Type: files; Name: "{app}\{#AppExe}.old"
Type: files; Name: "{app}\{#AppExe}.new"
Type: filesandordirs; Name: "{app}\Updates"
Type: filesandordirs; Name: "{app}\Logs"

[Run]
; Not elevated, so drag and drop works (Windows blocks it into an administrator window)
Filename: "{app}\{#AppExe}"; Description: "Start {#AppName}"; Flags: nowait postinstall skipifsilent runasoriginaluser

; The automatic update task: created when automatic updates are on, removed when they're off
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{app}\Update-Win11ImagePrep.ps1"" -Register"; Flags: runhidden waituntilterminated; StatusMsg: "Setting up automatic updates..."; Check: AutoUpdateOn
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{app}\Update-Win11ImagePrep.ps1"" -Unregister"; Flags: runhidden waituntilterminated; Check: not AutoUpdateOn

[UninstallRun]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{app}\Update-Win11ImagePrep.ps1"" -Unregister"; Flags: runhidden waituntilterminated; RunOnceId: "RemoveUpdateTask"

[Code]
function ManagedUpdatesGiven: Boolean;
begin
  Result := Pos('/MANAGEDUPDATES=', Uppercase(GetCmdTail)) > 0;
end;

function ReadDword(Name: String; var Value: Cardinal): Boolean;
begin
  Result := RegQueryDWordValue(HKLM64, '{#AppKey}', Name, Value);
end;

{ Automatic updates. /MANAGEDUPDATES=1 turns them off and /MANAGEDUPDATES=0 back on; /AUTOUPDATE=0 or 1 chooses
  the task. Without switches, the device's existing choice is kept; a new install has them on. }
function AutoUpdateOn: Boolean;
var
  Value: Cardinal;
  AutoGiven: Boolean;
begin
  AutoGiven := Pos('/AUTOUPDATE=', Uppercase(GetCmdTail)) > 0;
  if ManagedUpdatesGiven and (ExpandConstant('{param:MANAGEDUPDATES|0}') = '1') then
    Result := False
  else if AutoGiven then
    Result := ExpandConstant('{param:AUTOUPDATE|1}') <> '0'
  else if ManagedUpdatesGiven then
    Result := True
  else if ReadDword('ManagedUpdates', Value) and (Value = 1) then
    Result := False
  else if ReadDword('AutoUpdate', Value) then
    Result := Value = 1
  else
    Result := True;
end;

function AutoUpdateValue(Param: String): String;
begin
  if AutoUpdateOn then Result := '1' else Result := '0';
end;
