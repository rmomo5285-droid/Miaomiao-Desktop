#ifndef AppVersion
  #error AppVersion is required
#endif

#ifndef Arch
  #error Arch is required
#endif

#ifndef PayloadDir
  #error PayloadDir is required
#endif

#ifndef OutputDir
  #error OutputDir is required
#endif

#ifndef InstallerIcon
  #error InstallerIcon is required
#endif

#if Arch == "arm64"
  #define AllowedArchitectures "arm64"
#else
  #define AllowedArchitectures "x64compatible"
#endif

[Setup]
AppId=Miaomiao.Desktop
AppName=Miaomiao
AppVersion={#AppVersion}
AppPublisher=Miaomiao
DefaultDirName={localappdata}\Programs\Miaomiao
DefaultGroupName=Miaomiao
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed={#AllowedArchitectures}
ArchitecturesInstallIn64BitMode={#AllowedArchitectures}
OutputDir={#OutputDir}
OutputBaseFilename=Miaomiao-{#AppVersion}-windows-{#Arch}-setup
SetupIconFile={#InstallerIcon}
UninstallDisplayIcon={app}\Miaomiao.exe
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
UsePreviousAppDir=yes
VersionInfoVersion={#AppVersion}
VersionInfoCompany=Miaomiao
VersionInfoDescription=Miaomiao desktop installer
VersionInfoProductName=Miaomiao
VersionInfoProductVersion={#AppVersion}

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Miaomiao"; Filename: "{app}\Miaomiao.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Miaomiao"; Filename: "{app}\Miaomiao.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Miaomiao.exe"; Description: "Launch Miaomiao"; Flags: nowait postinstall skipifsilent
