; Inno Setup Script for Claude Code Hooks Notifier
; Compile: ISCC.exe setup.iss

#define MyAppName "Claude Code Hooks Notifier"
#define MyAppVersion "1.4.0"
#define MyAppPublisher "Claude Code Hooks Notifier"
#define MyAppExeName "hooks-notifier.exe"

[Setup]
AppId={{F6A1B2C3-D4F5-6789-ABCD-EF0123456789}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\ClaudeHooksNotifier
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableDirPage=no
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=.
OutputBaseFilename=ClaudeCodeHooksNotifier-Setup
; SetupIconFile=铃铛.ico
Compression=lzma2/max
SolidCompression=yes
UninstallDisplayIcon={app}\hooks-notifier.exe
UninstallDisplayName={#MyAppName}
WizardStyle=modern
WizardSizePercent=120

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinese"; MessagesFile: "installer\ChineseSimplified.isl"

[CustomMessages]
english.PostInstallLabel=Launch [name] now
chinese.PostInstallLabel=立即启动 [name]

english.FinishedLabel=Setup has completed installing [name].{newline}{newline}Click Finish to exit Setup.
chinese.FinishedLabel=安装程序已完成安装 [name]。{newline}{newline}单击"完成"退出安装程序。

[Tasks]
Name: "autostart"; Description: "Start automatically when I log in"; GroupDescription: "Startup options:"

[Files]
Source: "bin\hooks-notifier.exe"; DestDir: "{app}"; Flags: ignoreversion 64bit
Source: "bin\hooks-notifier.dll"; DestDir: "{app}"; Flags: ignoreversion 64bit
Source: "bin\hooks-notifier.deps.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\hooks-notifier.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\WinRT.Runtime.dll"; DestDir: "{app}"; Flags: ignoreversion 64bit
Source: "bin\Microsoft.Windows.SDK.NET.dll"; DestDir: "{app}"; Flags: ignoreversion 64bit
Source: "bin\hooks-notifier.pdb"; DestDir: "{app}"; Flags: ignoreversion
; Support scripts
Source: "setup.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "hooks\notify.ps1"; DestDir: "{app}\hooks"; Flags: ignoreversion
Source: "publish.ps1"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--tray"; WorkingDir: "{app}"; Comment: "Launch system tray with bell icon"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"; WorkingDir: "{app}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--tray"; Description: "Launch {#MyAppName} now"; Flags: nowait postinstall skipifsilent

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ClaudeCodeHooksNotifier"; ValueData: """{app}\{#MyAppExeName}"" --tray"; Flags: uninsdeletevalue; Tasks: autostart

[UninstallRun]
Filename: "taskkill"; Parameters: "/f /im hooks-notifier.exe"; Flags: runhidden

[Code]
var
  ResultCode: Integer;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    Exec(ExpandConstant('{app}\hooks-notifier.exe'), '--register', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;
