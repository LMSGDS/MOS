; MOS-KulKul — bộ cài nhỏ (web stub). Khi bấm Cài sẽ tải bản đầy đủ từ máy chủ.
#define MyAppName "MOS-KulKul"
#define MyAppVersion "1.14.8"
#define MyAppPublisher "Trường GDS"
#define MyAppURL "https://mos.gds.edu.vn/cai-dat"
#ifndef FullUrl
  #define FullUrl "https://mos.gds.edu.vn/cai-dat/windows-full"
#endif

[Setup]
AppId={{8F3C2A91-0B6E-4D11-9C77-MOSDOCKWEB01}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
CreateAppDir=no
Uninstallable=no
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\..\..\dist-installer
OutputBaseFilename=MOS-KulKul-Setup-Windows
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=kulkul.ico
WizardSmallImageFile=wizard-small.bmp
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyMemo=no
DisableWelcomePage=no
RestartIfNeededByRun=no
CloseApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
WelcomeLabel1=Cài MOS-KulKul
WelcomeLabel2=File này nhỏ vì chỉ chứa liên kết máy chủ.%n%nKhi bấm Cài đặt, máy sẽ tải bản đầy đủ từ:%n{#FullUrl}%n%nrồi chạy trình cài MOS-KulKul (không cần Admin).
ReadyLabel1=Sẵn sàng tải bộ cài đầy đủ MOS-KulKul từ máy chủ nhà trường.
FinishedHeadingLabel=Đã mở trình cài MOS-KulKul
FinishedLabelNoIcons=Bộ cài đầy đủ đã được tải từ máy chủ. Làm theo cửa sổ cài đặt MOS-KulKul nếu nó vẫn đang mở.
ButtonInstall=&Cài đặt

[Run]
Filename: "{tmp}\MOS-KulKul-Setup-Full.exe"; StatusMsg: "Đang chạy bộ cài đầy đủ MOS-KulKul…"; Flags: waituntilterminated skipifdoesntexist

[Code]
var
  DownloadPage: TDownloadWizardPage;

function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  if ProgressMax <> 0 then
    DownloadPage.SetProgress(Progress, ProgressMax)
  else
    DownloadPage.SetText('Đang tải MOS-KulKul từ máy chủ…', '');
  Result := True;
end;

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage('Tải bộ cài đầy đủ',
    'Đang tải MOS-KulKul từ {#FullUrl}', @OnDownloadProgress);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Dest: String;
  Got: Int64;
begin
  Result := True;
  if CurPageID <> wpReady then
    Exit;

  Dest := ExpandConstant('{tmp}\MOS-KulKul-Setup-Full.exe');
  DownloadPage.Clear;
  DownloadPage.Add('{#FullUrl}', 'MOS-KulKul-Setup-Full.exe', '');
  DownloadPage.Show;
  Got := 0;
  try
    try
      Got := DownloadPage.Download;
    except
      if not DownloadPage.AbortedByUser then
        MsgBox('Không tải được bộ cài đầy đủ từ máy chủ.'#13#10#13#10 +
          '{#FullUrl}'#13#10#13#10 +
          'Kiểm tra mạng rồi thử lại, hoặc tải bản đầy đủ (offline) trên trang Cài đặt.',
          mbCriticalError, MB_OK);
      Result := False;
    end;
  finally
    DownloadPage.Hide;
  end;

  if not Result then
    Exit;

  if (not FileExists(Dest)) or (Got < 5000000) then
  begin
    MsgBox('File tải về không phải bộ cài đầy đủ MOS-KulKul.'#13#10#13#10 +
      '{#FullUrl}', mbCriticalError, MB_OK);
    Result := False;
  end;
end;
