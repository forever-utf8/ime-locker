#define MyAppName "ImeLocker"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "ImeLocker"
#define MyAppExeName "ImeLocker.exe"

[Setup]
AppId={{E8F3A1B2-5C7D-4E9F-B6A8-2D1C3E4F5A6B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\output
OutputBaseFilename=ImeLocker-Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequiredOverridesAllowed=dialog
PrivilegesRequired=lowest
SetupIconFile=..\src\ImeLocker\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "开机自启动"; GroupDescription: "其他选项:"

[Files]
Source: "..\publish\ImeLocker.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish\D3DCompiler_47_cor3.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish\PenImc_cor3.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish\PresentationNative_cor3.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish\vcruntime140_cor3.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish\wpfgfx_cor3.dll"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Auto-start entry (only if task selected)
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Kill the running process before uninstalling
Filename: "taskkill"; Parameters: "/F /IM {#MyAppExeName}"; Flags: runhidden; RunOnceId: "KillApp"

[UninstallDelete]
; Clean up AppData config and logs
Type: filesandordirs; Name: "{userappdata}\{#MyAppName}"

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    { Kill the process first }
    Exec('taskkill', '/F /IM ' + '{#MyAppExeName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(500);

    { Remove auto-start registry entry (in case it was added at runtime) }
    RegDeleteValue(HKEY_CURRENT_USER, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Run', '{#MyAppName}');

    { Remove AppData folder: config.yaml, logs, etc. }
    DelTree(ExpandConstant('{userappdata}\{#MyAppName}'), True, True, True);
  end;

  if CurUninstallStep = usPostUninstall then
  begin
    { Remove installation directory if anything remains }
    DelTree(ExpandConstant('{app}'), True, True, True);
  end;
end;
