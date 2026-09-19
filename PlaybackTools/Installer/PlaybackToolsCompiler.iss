#define MyAppName "Playback Tools"
#define MyAppVersion "Beta 2.0"
#define MyAppPublisher "AV Techkit"
#define MyAppURL "https://avtechkit.com"
#define MyAppExeName "PlaybackTools.exe"

[Setup]
AppId={{C958E417-3D97-4D0A-B7F4-1D04288D6CF2}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=PlaybackToolsSetup
SetupIconFile=..\Assets\PlaybackIcon.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\bin\Release\net10.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Redist\ffmpeg.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKA; Subkey: "Software\Classes\.playback"; ValueType: string; ValueName: ""; ValueData: "PlaybackTools.ShowFile"; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\PlaybackTools.ShowFile"; ValueType: string; ValueName: ""; ValueData: "Playback Tools Show File"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\PlaybackTools.ShowFile\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"
Root: HKA; Subkey: "Software\Classes\PlaybackTools.ShowFile\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent