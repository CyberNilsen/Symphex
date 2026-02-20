[Setup]
AppName=Symphex
AppVersion=1.3.1
AppPublisher=CyberNilsen
AppPublisherURL=https://github.com/CyberNilsen/Symphex
DefaultDirName={autopf}\Symphex
DefaultGroupName=Symphex
OutputDir=C:\Users\ander\source\repos\CyberNilsen\Symphex\Symphex\release
OutputBaseFilename=Symphex-1.3.1-win-x64-setup
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
SetupIconFile=C:\Users\ander\source\repos\CyberNilsen\Symphex\Symphex\Assets\SymphexLogo.ico
UninstallDisplayIcon={app}\Symphex.exe
WizardStyle=modern
PrivilegesRequired=admin

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "C:\Users\ander\source\repos\CyberNilsen\Symphex\Symphex\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Symphex"; Filename: "{app}\Symphex.exe"
Name: "{autodesktop}\Symphex"; Filename: "{app}\Symphex.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Symphex.exe"; Description: "{cm:LaunchProgram,Symphex}"; Flags: nowait postinstall skipifsilent
