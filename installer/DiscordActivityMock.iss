#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#ifndef SourceDir
  #define SourceDir "..\publish\win-x86-folder"
#endif

#ifndef OutputDir
  #define OutputDir "..\release"
#endif

#ifndef AppExeName
  #define AppExeName "DiscordActivityMock-v2.2.5-win-x86-Portable.exe"
#endif

[Setup]
AppId={{C2A64B4B-69FC-4F28-B048-5C4E661D4F32}
AppName=Discord Activity Mock
AppVersion={#AppVersion}
AppPublisher=iroaK
AppPublisherURL=https://github.com/iiroak/DiscordActivityMock
AppSupportURL=https://github.com/iiroak/DiscordActivityMock/issues
AppUpdatesURL=https://github.com/iiroak/DiscordActivityMock/releases
DefaultDirName={localappdata}\Programs\DiscordActivityMock
DefaultGroupName=Discord Activity Mock
UninstallDisplayIcon={app}\{#AppExeName}
PrivilegesRequired=lowest
ArchitecturesAllowed=x86 x64compatible
CloseApplications=force
RestartApplications=no
OutputDir="{#OutputDir}"
OutputBaseFilename=DiscordActivityMock-{#AppVersion}-win-x86-Setup
SetupIconFile=..\Assets\logo.ico
Uninstallable=yes
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE

[InstallDelete]
Type: files; Name: "{app}\DiscordActivityMock-v*-win-x86-Portable.exe"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Discord Activity Mock"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\Discord Activity Mock"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch Discord Activity Mock"; Flags: nowait postinstall skipifsilent
Filename: "{app}\{#AppExeName}"; Flags: nowait runasoriginaluser; Check: WizardSilent
Filename: "{app}\{#AppExeName}"; Flags: nowait runasoriginaluser; Check: WizardSilent
