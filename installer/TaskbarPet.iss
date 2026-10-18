; Inno Setup script for Taskbar Pet. Build with build.ps1 (it publishes the app first),
; or manually: ISCC.exe installer\TaskbarPet.iss  (expects publish\TaskbarPet.exe).
;
; Per-user install (no admin rights): %LOCALAPPDATA%\Programs\TaskbarPet,
; Start-menu entry, optional desktop icon and "Start with Windows", uninstall entry
; in Settings -> Apps.

#define AppName "Taskbar Pet"
#define AppExe "TaskbarPet.exe"
#define SourceExe "..\publish\" + AppExe
; Version comes from the exe itself (the <Version> in TaskbarPet.csproj).
#define AppVersion GetStringFileInfo(SourceExe, "ProductVersion")

[Setup]
; AppId identifies the app for upgrades and uninstall. Never change it.
AppId={{6F1D3C2A-8B4E-4F7A-9C1D-2E5B7A9F4C31}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Taskbar Pet
DefaultDirName={autopf}\TaskbarPet
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=..\dist
OutputBaseFilename=TaskbarPet-{#AppVersion}-setup
SetupIconFile=..\src\TaskbarPet\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

[Tasks]
Name: "autostart"; Description: "Start Taskbar Pet when I sign in to Windows"; GroupDescription: "Startup:"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Same entry the app's own "Start with Windows" switch uses (StartupManager.cs).
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "TaskbarPet"; \
    ValueData: """{app}\{#AppExe}"""; Tasks: autostart; Flags: uninsdeletevalue
; Remove the entry on uninstall even if it was switched on later from the app.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "TaskbarPet"; \
    Flags: dontcreatekey uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Settings (%APPDATA%\TaskbarPet\settings.json).
Type: filesandordirs; Name: "{userappdata}\TaskbarPet"

[Code]
// Close a running pet before files are replaced or removed. It has no window to
// close politely, and it saves settings whenever they change, so ending it is safe.
procedure StopRunningPet();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#AppExe}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(500);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopRunningPet();
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  StopRunningPet();
  Result := True;
end;
