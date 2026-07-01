# 팜스코 라벨 출력 (FarmscoLabel)

엑셀 배송 데이터를 업로드해서 **박스 단위로 넘버링한 라벨**을 선인쇄 양식지에 출력하는 Windows 설치형 프로그램입니다.

- 별도 데이터베이스 없음 → 엑셀을 **메모리**에 올려서 처리
- 라벨은 **선인쇄 양식지**(표·항목명이 미리 인쇄된 용지)에 **값만** 인쇄
- 프린터: **Zebra ZT411**, **Honeywell PC42T** (Windows 드라이버로 이미지 방식 출력 → 프린터 종류와 무관하게 동작)
- 라벨 용지: **100 × 120 mm, 세로 방향**

---

## 📁 프로젝트 구조

```
farmsco-label/
├─ FarmscoLabel.sln                 # 솔루션 파일(비주얼 스튜디오)
├─ FarmscoLabel/
│  ├─ FarmscoLabel.csproj           # 프로젝트 설정
│  ├─ App.xaml(.cs)                 # 프로그램 시작점
│  ├─ MainWindow.xaml(.cs)          # 메인 화면 (업로드/필터/표/상세/출력)
│  ├─ SettingsWindow.xaml(.cs)      # 설정 화면
│  ├─ Models/
│  │  ├─ StorageType.cs             # 보관유형 분류(상온/냉장/냉동)
│  │  ├─ DeliveryRow.cs             # 엑셀 한 줄 + 박스수량 계산
│  │  ├─ LabelItem.cs               # 라벨 1장(박스 1개)
│  │  └─ AppSettings.cs             # 설정 저장/불러오기(좌표 포함)
│  └─ Services/
│     ├─ ExcelImporter.cs           # 엑셀 읽기(ClosedXML)
│     ├─ NumberingEngine.cs         # 넘버링(박스 분할) 계산
│     └─ LabelPrinter.cs            # 양식지에 값만 인쇄
└─ installer/
   └─ setup.iss                     # 설치파일(setup.exe) 스크립트(Inno Setup)
```

---

## 🛠 빌드 방법 (Windows에서)

> ⚠️ WPF는 **Windows 전용**입니다. macOS/Linux에서는 빌드·실행이 안 됩니다.

**준비물**: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```powershell
# 1) 프로젝트 폴더로 이동
cd farmsco-label

# 2) 라이브러리 내려받기
dotnet restore

# 3) 실행(개발용)
dotnet run --project FarmscoLabel

# 4) 배포용 빌드(설치파일에 넣을 결과물 생성)
dotnet publish FarmscoLabel -c Release -r win-x64 --self-contained false -o FarmscoLabel/bin/Release/net8.0-windows/publish
```

> `--self-contained true`로 하면 .NET 런타임까지 포함되어 용량이 커지지만, 대상 PC에 .NET 설치가 필요 없습니다. 현장 PC에 .NET이 없으면 `true`를 권장합니다.

### 설치파일(setup.exe) 만들기
1. [Inno Setup](https://jrsoftware.org/isdl.php) 설치
2. `installer/setup.iss` 파일을 열고 상단 **Source** 경로가 위 publish 폴더와 맞는지 확인
3. **Compile** → `installer/Output/FarmscoLabel_Setup_1.0.0.exe` 생성

---

## 🖨 사용 방법

1. **엑셀 업로드**: [📁 엑셀 업로드]로 배송 데이터(.xlsx)를 불러옵니다.
   - 표에 원본 데이터 + **박스수량** 컬럼이 자동 표시됩니다.
2. **필터**: 상단 체크박스로 **상온 / 냉장 / 냉동**을 골라서 볼 수 있어요(다중 선택).
3. **상세 확인**: 표에서 행을 선택하면 아래에 **박스별 순번(1/11 ~ 11/11)** 이 펼쳐집니다.
4. **프린터 선택** 후,
   - **선택 행 출력**: 표에서 고른 행들만 인쇄
   - **전체(필터) 출력**: 현재 필터로 보이는 전체 인쇄

### 넘버링 규칙
`총수량 ÷ 입수수량` 으로 박스 수를 계산하고, 마지막 박스는 잔량으로 처리합니다.

> 예) 총수량 **420**, 입수 **40** → 박스 **11장**
> - `40 / 420` 라벨 10장 (순번 1/11 ~ 10/11)
> - `20 / 420` 라벨 1장 (순번 11/11)

---

## 🎯 양식지 위치(좌표) 맞추기 — 중요!

라벨은 **표와 항목명이 미리 인쇄된 양식지**에 값만 찍습니다. 그래서 처음 한 번은 **값이 찍히는 위치를 양식지에 맞게 조정**해야 합니다.

- 간단한 보정: [⚙ 설정]에서 **인쇄 위치 보정(X/Y mm)** 으로 전체를 상하좌우로 밀 수 있습니다.
- 세밀한 보정: 설정 파일에서 각 칸의 좌표를 직접 수정합니다.
  - 위치: `%APPDATA%\FarmscoLabel\settings.json`
  - `Fields` 안의 각 항목(`XMm`, `YMm`, `FontSize`, `Bold`)을 양식지에 맞춰 조정하세요.
- **팁**: 실물 라벨을 낭비하지 않으려면 프린터를 **"Microsoft Print to PDF"** 로 먼저 골라 PDF로 뽑아보고, 양식지 스캔본과 겹쳐 위치를 맞춘 뒤 실물 출력하세요.

---

## ✅ 점검 체크리스트
- [ ] 엑셀 업로드 시 박스수량 컬럼이 보이는가
- [ ] 총수량 420·입수 40 행의 박스수량이 **11** 인가
- [ ] 필터(냉장만/다중선택)가 정상 동작하는가
- [ ] 행 선택 시 상세에 순번 1/11 ~ 11/11, 마지막 20/420 이 나오는가
- [ ] "Microsoft Print to PDF"로 값 위치가 맞는가
- [ ] ZT411 / PC42T 실물 출력 후 위치 보정 완료
