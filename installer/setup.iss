#ifndef MyAppName
#define MyAppName "DMSJ Blood Alcohol"
#endif

#ifndef MyAppVersion
#define MyAppVersion "1.0.0"
#endif

#ifndef MyAppPublisher
#define MyAppPublisher "DMSJ"
#endif

#ifndef MyAppExeName
#define MyAppExeName "DMSJ_Blood Alcohol.exe"
#endif

#ifndef SourcePublishDir
#define SourcePublishDir "..\artifacts\publish\win-x64"
#endif

[Setup]
AppId={{6D6AA02C-4ED3-4E52-B90F-2B3DD12686F6}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=DMSJ_Blood_Alcohol_Setup_{#MyAppVersion}_x64
SetupIconFile=..\Resources\favicon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
ChangesAssociations=no
CloseApplications=yes

[Languages]
Name: "default"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional tasks"

[Dirs]
Name: "{app}\Logs"
Name: "{app}\Config"

[Files]
Source: "{#SourcePublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName} after setup"; Flags: nowait postinstall skipifsilent
