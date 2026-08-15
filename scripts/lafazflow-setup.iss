#define MyAppName "LafazFlow"

#ifndef MyAppVersion
  #define MyAppVersion "1.1.1"
#endif
#ifndef MyAppSource
  #define MyAppSource "."
#endif
#ifndef MyOutputDir
  #define MyOutputDir "..\artifacts\release"
#endif
#ifndef MyOutputFile
  #define MyOutputFile "LafazFlow-setup"
#endif

[Setup]
AppId={{9E7C1B3A-5D4F-4A6B-9C2E-3F8A1B2C4D5E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=itsLucas02
DefaultDirName={autopf}\LafazFlow
DefaultGroupName=LafazFlow
OutputDir={#MyOutputDir}
OutputBaseFilename={#MyOutputFile}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\LafazFlow.Windows.exe

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"

[Files]
Source: "{#MyAppSource}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\LafazFlow"; Filename: "{app}\LafazFlow.Windows.exe"
Name: "{autodesktop}\LafazFlow"; Filename: "{app}\LafazFlow.Windows.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\LafazFlow.Windows.exe"; Description: "Launch LafazFlow"; Flags: nowait postinstall skipifsilent
