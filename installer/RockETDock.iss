#define AppName "Rock ET Dock"
#ifndef AppVersion
#define AppVersion "0.4.0"
#endif
#define AppPublisher "Discasa"
#define AppExeName "Rock ET Dock.exe"
#define SettingsExeName "Rock ET Dock Settings.exe"
#define PackageName "Rock-ET-Dock-" + AppVersion + "-win-x64"

[Setup]
AppId={{9C5B0148-3574-4E01-B3B9-A00D4D31D33F}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/Discasa/Rock-ET-Dock
AppSupportURL=https://github.com/Discasa/Rock-ET-Dock/issues
AppUpdatesURL=https://github.com/Discasa/Rock-ET-Dock/releases
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=..\artifacts\installer
OutputBaseFilename=Rock-ET-Dock-Setup-{#AppVersion}-win-x64
SetupIconFile=..\src\Dock.App\Assets\rock-et-dock-icon.ico
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog commandline
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\{#PackageName}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{#AppName} Settings"; Filename: "{app}\{#SettingsExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
