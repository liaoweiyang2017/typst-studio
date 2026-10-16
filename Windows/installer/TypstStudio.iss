#ifndef SourceDirectory
 #error SourceDirectory is required
#endif
#ifndef TargetArchitecture
 #error TargetArchitecture is required
#endif
[Setup]
AppId={{9CD1EBB5-89BC-460D-B688-104E2268A31A}
AppName=Typst Studio
AppVersion=0.1.0
AppPublisher=Weiyang Liao
DefaultDirName={localappdata}\Programs\Typst Studio
DefaultGroupName=Typst Studio
PrivilegesRequired=lowest
MinVersion=10.0.19041
#if TargetArchitecture == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif
OutputDir=..\artifacts\installers
OutputBaseFilename=Typst-Studio-0.1.0-Windows-{#TargetArchitecture}-Setup
SetupIconFile={#SourceDirectory}\Assets\Studio.ico
UninstallDisplayIcon={app}\TypstStudio.App.exe
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
[Files]
Source: "{#SourceDirectory}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\Typst Studio"; Filename: "{app}\TypstStudio.App.exe"
[Run]
Filename: "{app}\TypstStudio.App.exe"; Description: "启动 Typst Studio"; Flags: nowait postinstall skipifsilent
