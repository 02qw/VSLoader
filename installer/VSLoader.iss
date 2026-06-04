#ifndef AppName
#define AppName "VSLoader"
#endif

#ifndef AppVersion
#define AppVersion "1.0.0"
#endif

#ifndef Publisher
#define Publisher "shee"
#endif

#ifndef OutputBaseFilename
#define OutputBaseFilename "VSLoader_Setup_1.0.0"
#endif

#ifndef SourceDir
#define SourceDir "..\publish"
#endif

[Setup]
AppId={{7AE46B09-A65F-42D8-B87D-7D7F3D934FE8}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#Publisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename={#OutputBaseFilename}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\VSLoader\Assets\tomato.ico
UninstallDisplayIcon={app}\VSLoader.exe
CloseApplications=yes
CloseApplicationsFilter=VSLoader.exe
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务："; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\VSLoader.exe"
Name: "{group}\卸载 {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\VSLoader.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\VSLoader.exe"; Description: "启动 {#AppName}"; Flags: nowait postinstall skipifsilent
