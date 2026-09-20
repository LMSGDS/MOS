; MOS-KulKul — cài per-user, không cần Admin
#define MyAppName "MOS-KulKul"
#define MyAppVersion "1.21.3"
#define MyAppPublisher "mos.gds.edu.vn"
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
DefaultDirName={localappdata}\MOS\KulKul
DefaultGroupName=MOS-KulKul
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\..\..\dist-installer
OutputBaseFilename=MOS-KulKul-Setup-Windows-Full
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\MOS-KulKul.exe
SetupIconFile=kulkul.ico
WizardSmallImageFile=wizard-small.bmp
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#Dist}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,install.ps1"

[Icons]
Name: "{group}\MOS-KulKul"; Filename: "{app}\MOS-KulKul.exe"
Name: "{autodesktop}\MOS-KulKul"; Filename: "{app}\MOS-KulKul.exe"; Tasks: desktopicon
Name: "{userstartup}\MOS-KulKul"; Filename: "{app}\MOS-KulKul.exe"

[Tasks]
Name: "desktopicon"; Description: "Tạo lối tắt trên Desktop"; GroupDescription: "Lối tắt:"

[Registry]
Root: HKCU; Subkey: "Software\Classes\mosdock"; ValueType: string; ValueName: ""; ValueData: "URL:MOS-KulKul"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\mosdock"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\mosdock\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\MOS-KulKul.exe"" ""%1"""
Root: HKCU; Subkey: "Software\Classes\mos-kulkul"; ValueType: string; ValueName: ""; ValueData: "URL:MOS-KulKul"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\mos-kulkul"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""
Root: HKCU; Subkey: "Software\Classes\mos-kulkul\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\MOS-KulKul.exe"" ""%1"""

[Run]
Filename: "{app}\MOS-KulKul.exe"; Description: "Chạy MOS-KulKul ngay"; Flags: nowait postinstall skipifsilent
