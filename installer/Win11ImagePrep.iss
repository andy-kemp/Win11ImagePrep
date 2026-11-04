; Win11 Image Prep installer (Inno Setup 6). Built by .github/workflows/release.yml:
;   iscc /DAppVersion=5.9.11 /DSourceDir=..\out\app /DUpdaterDir=..\out\updater /O..\release installer\Win11ImagePrep.iss
;
; Installs for all users in C:\Program Files\Andy Kemp Consulting\Win11 Image Prep, with a Start menu shortcut.
;
; Silent install (Intune, Configuration Manager, a script):
;   Win11ImagePrep_Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
; Add /MANAGEDUPDATES=1 to turn the app's own update check off, when Intune (or similar) deploys new versions.
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

[InstallDelete]
; Left by an in-app update (the previous version, renamed)
Type: files; Name: "{app}\{#AppExe}.old"
Type: files; Name: "{app}\{#AppExe}.new"

[UninstallDelete]
Type: files; Name: "{app}\{#AppExe}.old"
Type: files; Name: "{app}\{#AppExe}.new"

[Run]
; Not elevated, so drag and drop works (Windows blocks it into an administrator window)
Filename: "{app}\{#AppExe}"; Description: "Start {#AppName}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
function ManagedUpdatesGiven: Boolean;
begin
  Result := Pos('/MANAGEDUPDATES=', Uppercase(GetCmdTail)) > 0;
end;
