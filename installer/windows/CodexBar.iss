#ifndef AppVersion
#define AppVersion "dev"
#endif

#ifndef AppRuntime
#define AppRuntime "win-x64"
#endif

#ifndef PublishDir
#define PublishDir "..\..\dist\windows\CodexBar-Windows-dev-win-x64"
#endif

#ifndef OutputDir
#define OutputDir "..\..\dist\windows"
#endif

[Setup]
AppId={{D7FA691B-E77F-4F45-A93F-02A39B2067A1}
AppName=CodexBar for Windows
AppVersion={#AppVersion}
AppPublisher=CodexBar for Windows
AppPublisherURL=https://github.com/dontcallmejames/CodexBar-Windows
AppSupportURL=https://github.com/dontcallmejames/CodexBar-Windows/issues
AppUpdatesURL=https://github.com/dontcallmejames/CodexBar-Windows/releases/latest
DefaultDirName={localappdata}\Programs\CodexBar
DefaultGroupName=CodexBar
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=CodexBar-Windows-{#AppVersion}-{#AppRuntime}.installer
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\CodexBar.WinUI.exe

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\CodexBar"; Filename: "{app}\CodexBar.WinUI.exe"
Name: "{group}\Uninstall CodexBar"; Filename: "{uninstallexe}"
Name: "{userdesktop}\CodexBar"; Filename: "{app}\CodexBar.WinUI.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\CodexBar.WinUI.exe"; Description: "Launch CodexBar"; Flags: nowait postinstall skipifsilent
