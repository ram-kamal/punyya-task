; Script generated for Punnya Heart Foundation
; Inno Setup 6 Script for Punnya Heart Desk

#define MyAppName "Punnya Heart Desk"
#define MyAppVersion "1.0.0-beta"
#define MyAppPublisher "Punnya Heart Foundation"
#define MyAppURL "https://punnya.org/task"
#define MyAppExeName "PunnyaHeartDesk.exe"
#define MySourceDir "..\publish\PunnyaHeartDesk-Beta-win-x64"

[Setup]
AppId={{C349C5B2-F4A1-42D3-87D9-92381F1BEA88}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
OutputDir=..\publish\installer
OutputBaseFilename=PunnyaHeartDesk-Setup
SetupIconFile=..\src\PHF.Client.Desktop\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "startwithwindows"; Description: "Launch Punnya Heart Desk automatically when Windows starts (Recommended for Workstation Telemetry)"; GroupDescription: "Workstation Telemetry Options:"

[Files]
Source: "{#MySourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"
Source: "..\src\PHF.Client.Desktop\app.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\app.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\app.ico"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "PunnyaHeartDesk"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startwithwindows

[Run]
Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  // Automatically terminate any running instance of Punnya Heart Desk before installing
  Exec('taskkill.exe', '/F /IM PunnyaHeartDesk.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode);
  Exec('taskkill.exe', '/F /IM PunnyaHeartDesk.Wpf.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode);
  Sleep(600);
end;

function InitializeUninstall(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  Exec('taskkill.exe', '/F /IM PunnyaHeartDesk.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode);
  Exec('taskkill.exe', '/F /IM PunnyaHeartDesk.Wpf.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode);
  Sleep(600);
end;
