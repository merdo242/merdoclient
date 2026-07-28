[Setup]
AppName=Aura Network
AppVersion=8.62
AppPublisher=AuraNetwork
AppPublisherURL=https://AuraNetwork.com
DefaultDirName={autopf}\Aura Network
DefaultGroupName=Aura Network
OutputDir=..\installer
OutputBaseFilename=AuraNetwork_Installer
SetupIconFile=
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\AuraNetwork.exe
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "desktopicon"; Description: "Masaüstüne kısayol oluştur"; GroupDescription: "Ek görevler:"
Name: "startmenu"; Description: "Başlat menüsüne kısayol ekle"; GroupDescription: "Ek görevler:"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\publish\Resources\logo_small.png"; DestDir: "{app}\Resources"; Flags: ignoreversion
Source: "..\publish\Resources\news_bg.jpg"; DestDir: "{app}\Resources"; Flags: ignoreversion
Source: "..\AuraNetwork\icon.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Aura Network"; Filename: "{app}\AuraNetwork.exe"; IconFilename: "{app}\icon.ico"
Name: "{autodesktop}\Aura Network"; Filename: "{app}\AuraNetwork.exe"; IconFilename: "{app}\icon.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\AuraNetwork.exe"; Description: "Aura Network'ı başlat"; Flags: nowait postinstall skipifsilent
