; MOS Dock — cài per-user, không cần Admin
#define MyAppName "MOS Dock"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Trường GDS"
#define MyAppURL "https://mos.gds.edu.vn/cai-dat"
#ifndef Dist
  #define Dist "..\..\..\dist-win"
#endif

[Setup]
AppId={{8F3C2A91-0B6E-4D11-9C77-MOSDOCKGDS01}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={localappdata}\MOS\MosDock
DefaultGroupName=MOS GDS
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\..\..\dist-installer
OutputBaseFilename=MOS-Dock-Setup-Windows
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\MosDock.exe
SetupIconFile=
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#Dist}\MosDock.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\MOS Dock"; Filename: "{app}\MosDock.exe"
Name: "{autodesktop}\MOS Dock"; Filename: "{app}\MosDock.exe"; Tasks: desktopicon
Name: "{userstartup}\MOS Dock"; Filename: "{app}\MosDock.exe"

[Tasks]
Name: "desktopicon"; Description: "Tạo lối tắt trên Desktop"; GroupDescription: "Lối tắt:"

[Registry]
Root: HKCU; Subkey: "Software\Classes\mosdock"; ValueType: string; ValueName: ""; ValueData: "URL:MOS Dock"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\mosdock"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\mosdock\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\MosDock.exe"" ""%1"""

[Run]
Filename: "{app}\MosDock.exe"; Description: "Chạy MOS Dock ngay"; Flags: nowait postinstall skipifsilent
