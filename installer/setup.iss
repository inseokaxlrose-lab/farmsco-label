; ─────────────────────────────────────────────────────────────
;  팜스코 라벨 출력 - Inno Setup 설치 스크립트
;  Inno Setup(무료) 설치 후, 이 파일을 열고 [Compile]하면
;  setup.exe(설치파일)가 만들어집니다.  https://jrsoftware.org/isdl.php
;
;  동작: 설치파일 실행 → 실행 중인 프로그램 자동 종료 → 파일 업데이트 → 재실행
; ─────────────────────────────────────────────────────────────

#define AppName "팜스코 라벨 출력"
#define AppVersion "1.0.0"
#define AppExe "FarmscoLabel.exe"
; .NET 8 Desktop Runtime (x64) 최신판 상시 다운로드 링크 (Microsoft 공식)
#define DotNetUrl "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe"

[Setup]
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Farmsco
DefaultDirName={autopf}\FarmscoLabel
DefaultGroupName={#AppName}
; 설치파일이 만들어질 위치와 이름
OutputDir=Output
OutputBaseFilename=FarmscoLabel_Setup_{#AppVersion}
Compression=lzma2
SolidCompression=yes
; 64비트 Windows에 설치
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
; 설치 화면
WizardStyle=modern
; 관리자 권한으로 표준 위치(C:\Program Files\FarmscoLabel)에 설치.
; .NET 런타임 자동 설치 시에도 어차피 UAC 가 필요하므로 admin 으로 통일.
PrivilegesRequired=admin
; 업데이트 시 실행 중이면 자동으로 닫도록 Inno Setup 에 알림
CloseApplications=yes
CloseApplicationsFilter=*.exe
; 종료 대상을 확실히 잡기 위해 설치될 exe 를 지정
AppMutex=

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Files]
; dotnet publish 결과 폴더(publish) 전체를 설치에 포함
Source: "..\FarmscoLabel\bin\Release\net8.0-windows\publish\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
; 시작 메뉴 & 바탕화면 바로가기
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{#AppName} 제거"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "바탕화면에 바로가기 만들기"; GroupDescription: "추가 아이콘:"; Flags: checkedonce

[Run]
; 설치가 끝나면 프로그램을 자동으로 다시 실행 (업데이트 후 재실행)
Filename: "{app}\{#AppExe}"; Description: "{#AppName} 실행"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
{ ── 설치/업데이트 시작 전에 실행 중인 프로그램을 확실하게 종료한다 ──
  CloseApplications 지시자와 별개로, 파일 잠금으로 인한 복사 실패를 막기 위해
  taskkill 로 한 번 더 강제 종료한다. }
var
  DownloadPage: TDownloadWizardPage;

procedure KillRunningApp;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#AppExe}', '',
    SW_HIDE, ewWaitUntilTerminated, ResultCode);
  { 프로세스가 완전히 내려갈 시간을 준다 }
  Sleep(800);
end;

{ ── .NET 8 Desktop Runtime(x64) 설치 여부 확인 ──
  이 앱은 프레임워크 의존(framework-dependent) 방식이라
  대상 PC에 Microsoft.WindowsDesktop.App 8.x 가 있어야 실행된다.
  런타임은 항상 64비트 Program Files\dotnet\shared 에 설치된다. }
function IsDotNet8DesktopInstalled(): Boolean;
var
  FindRec: TFindRec;
  BaseDir: String;
begin
  Result := False;
  BaseDir := ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App');
  if FindFirst(BaseDir + '\8.*', FindRec) then
  try
    repeat
      if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
      begin
        Result := True;
        Break;
      end;
    until not FindNext(FindRec);
  finally
    FindClose(FindRec);
  end;
end;

{ 다운로드받은 런타임 설치 프로그램을 조용히 실행(설치엔 관리자 권한이 필요해 UAC가 뜰 수 있음) }
function InstallDotNetRuntime(): Boolean;
var
  ResultCode: Integer;
  ExePath: String;
begin
  ExePath := ExpandConstant('{tmp}\windowsdesktop-runtime-8-x64.exe');
  Result := ShellExec('', ExePath, '/install /quiet /norestart', '',
    SW_SHOW, ewWaitUntilTerminated, ResultCode);
  { 0 = 성공, 3010 = 성공했으나 재부팅 필요 }
  Result := Result and ((ResultCode = 0) or (ResultCode = 3010));
end;

{ ── 선택한 폴더가 개발 소스/git 저장소인지 검사 ──
  .git 폴더나 .csproj/.sln 이 있으면 소스 트리로 보고 설치를 막는다.
  (소스에 빌드산출물이 섞이거나, 제거 시 소스가 삭제되는 사고 방지) }
function IsSourceFolder(Dir: String): Boolean;
begin
  Result :=
    DirExists(Dir + '\.git') or
    FileExists(Dir + '\FarmscoLabel.sln') or
    FileExists(Dir + '\FarmscoLabel\FarmscoLabel.csproj');
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  { 설치 폴더 선택 페이지에서 소스/git 폴더면 거부 }
  if CurPageID = wpSelectDir then
  begin
    if IsSourceFolder(WizardDirValue) then
    begin
      MsgBox(
        '선택한 폴더는 프로그램 소스(개발) 폴더로 보입니다:' + #13#10 +
        WizardDirValue + #13#10#13#10 +
        '이 폴더에 설치하면 소스가 손상되거나 제거 시 삭제될 수 있습니다.' + #13#10 +
        '다른 폴더(예: C:\Program Files\FarmscoLabel)를 선택하세요.',
        mbCriticalError, MB_OK);
      Result := False;
      Exit;
    end;
  end;

  { '설치' 직전(ready 페이지)에서 런타임을 점검 → 없으면 다운로드 후 설치 }
  if (CurPageID = wpReady) and (not IsDotNet8DesktopInstalled()) then
  begin
    DownloadPage.Clear;
    DownloadPage.Add('{#DotNetUrl}', 'windowsdesktop-runtime-8-x64.exe', '');
    DownloadPage.Show;
    try
      try
        DownloadPage.Download;
      except
        SuppressibleMsgBox(
          '.NET 8 런타임 다운로드에 실패했습니다.' + #13#10 +
          '인터넷 연결을 확인한 뒤 다시 시도하세요.' + #13#10#13#10 +
          '(오류: ' + GetExceptionMessage + ')',
          mbCriticalError, MB_OK, IDOK);
        Result := False;
        Exit;
      end;
    finally
      DownloadPage.Hide;
    end;

    if Result then
    begin
      if not InstallDotNetRuntime() then
      begin
        SuppressibleMsgBox(
          '.NET 8 런타임 설치에 실패했습니다.' + #13#10 +
          '설치를 계속할 수 없습니다.',
          mbCriticalError, MB_OK, IDOK);
        Result := False;
      end;
    end;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  { 파일을 실제로 덮어쓰기 직전에 호출됨 → 여기서 종료해야 잠금이 풀림 }
  KillRunningApp();
  Result := '';
end;

procedure InitializeWizard;
begin
  { 마법사가 뜨자마자도 한 번 종료해 둔다 }
  KillRunningApp();
  { .NET 런타임 다운로드용 진행 페이지 준비 }
  DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing),
    SetupMessage(msgPreparingDesc), nil);
end;

{ 제거(Uninstall) 시에도 실행 중이면 먼저 종료 }
function InitializeUninstall(): Boolean;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#AppExe}', '',
    SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(500);
  Result := True;
end;
