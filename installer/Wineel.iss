#define MyAppName "Wineel"
#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif
#define MyAppPublisher "yappologistic"
#define MyAppExeName "Wineel.exe"

[Setup]
AppId={{98DDEEF7-7488-4D42-9B84-A7D0EB8C4D76}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\Wineel
DefaultGroupName=Wineel
PrivilegesRequired=lowest
OutputDir=..\artifacts\installer
OutputBaseFilename=Wineel-{#MyAppVersion}-win-x64-setup
SetupIconFile=..\assets\Wineel.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no

[Files]
Source: "..\artifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Wineel"; Filename: "{app}\{#MyAppExeName}"
Name: "{userstartup}\Wineel"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--tray"; Tasks: startup

[Tasks]
Name: "startup"; Description: "Start Wineel with Windows"; GroupDescription: "Additional options:"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Wineel"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C reg delete HKCU\Software\Microsoft\Windows\CurrentVersion\Run /v Wineel /f"; Flags: runhidden; RunOnceId: "RemoveWineelStartup"
