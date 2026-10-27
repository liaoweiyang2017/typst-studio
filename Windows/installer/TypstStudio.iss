#ifndef SourceDirectory
 #error SourceDirectory is required
#endif
#ifndef TargetArchitecture
 #error TargetArchitecture is required
#endif
[Setup]
AppId={{9CD1EBB5-89BC-460D-B688-104E2268A31A}
AppName=Typst Studio
AppVersion=0.2.3
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
OutputBaseFilename=Typst-Studio-0.2.3-Windows-{#TargetArchitecture}-Setup
SetupIconFile={#SourceDirectory}\Assets\Studio.ico
UninstallDisplayIcon={app}\TypstStudio.App.exe
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern
ShowLanguageDialog=yes
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimp"; MessagesFile: "Languages\ChineseSimplified.isl"
[CustomMessages]
english.AppLanguageCode=en
chinesesimp.AppLanguageCode=zh
[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
[Files]
Source: "{#SourceDirectory}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\Typst Studio"; Filename: "{app}\TypstStudio.App.exe"; Parameters: "--initial-language {cm:AppLanguageCode}"
Name: "{autodesktop}\Typst Studio"; Filename: "{app}\TypstStudio.App.exe"; Parameters: "--initial-language {cm:AppLanguageCode}"; Tasks: desktopicon
[Run]
Filename: "{app}\TypstStudio.App.exe"; Parameters: "--initial-language {cm:AppLanguageCode}"; Description: "{cm:LaunchProgram,Typst Studio}"; Flags: nowait postinstall skipifsilent

; Only the updater's verified, explicitly approved handoff may restart after silent install.
Filename: "{app}\TypstStudio.App.exe"; Flags: nowait skipifnotsilent; Check: IsStudioUpdate

[Code]
function GetStudioMutex(Param: String): String;
var Dir: String;
begin
  Dir := ExpandConstant('{app}');
  while (Length(Dir) > 3) and (Dir[Length(Dir)] = '\') do Delete(Dir, Length(Dir), 1);
  Result := 'Local\TypstStudio-' + Lowercase(GetSHA256OfUnicodeString(Lowercase(Dir)));
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if CheckForMutexes(GetStudioMutex('')) then
    Result := 'Please close all Typst Studio windows from this installation and try again.';
end;

function IsStudioUpdate: Boolean;
begin
  Result := ExpandConstant('{param:STUDIOUPDATE|0}') = '1';
end;
