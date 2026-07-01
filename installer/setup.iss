; ─────────────────────────────────────────────────────────────
;  팜스코 라벨 출력 - Inno Setup 설치 스크립트
;  Inno Setup(무료) 설치 후, 이 파일을 열고 [Compile]하면
;  setup.exe(설치파일)가 만들어집니다.  https://jrsoftware.org/isdl.php
; ─────────────────────────────────────────────────────────────

#define AppName "팜스코 라벨 출력"
#define AppVersion "1.0.0"
#define AppExe "FarmscoLabel.exe"

[Setup]
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={autopf}\FarmscoLabel
DefaultGroupName={#AppName}
; 설치파일이 만들어질 위치와 이름
OutputDir=Output
OutputBaseFilename=FarmscoLabel_Setup_{#AppVersion}
Compression=lzma2
SolidCompression=yes
; 64비트 Windows에 설치
ArchitecturesInstallIn64BitMode=x64compatible
; 설치 화면 언어
WizardStyle=modern

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Files]
; dotnet publish로 만든 결과 폴더(publish) 전체를 설치에 포함
; 아래 Source 경로는 실제 publish 폴더에 맞게 수정하세요.
Source: "..\FarmscoLabel\bin\Release\net8.0-windows\publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs

[Icons]
; 시작 메뉴 & 바탕화면 바로가기
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{#AppName} 제거"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "바탕화면에 바로가기 만들기"; GroupDescription: "추가 아이콘:"

[Run]
; 설치가 끝나면 바로 실행할지 물어봄
Filename: "{app}\{#AppExe}"; Description: "지금 실행하기"; Flags: nowait postinstall skipifsilent
