using System.IO;
using System.Text.Json;

namespace FarmscoLabel.Models
{
    // 라벨의 한 칸에 값을 찍을 '위치와 글꼴 크기'.
    // 양식지는 이미 인쇄되어 있으므로, 값을 어디에(X,Y mm) 얼마 크기로 찍을지만 정해주면 됩니다.
    public class LabelField
    {
        public double XMm { get; set; }        // 왼쪽에서 몇 mm
        public double YMm { get; set; }        // 위에서 몇 mm
        public float FontSize { get; set; } = 9f;  // 글자 크기(pt)
        public bool Bold { get; set; } = false;    // 굵게 여부

        public LabelField() { }
        public LabelField(double x, double y, float size = 9f, bool bold = false)
        {
            XMm = x; YMm = y; FontSize = size; Bold = bold;
        }
    }

    // 프로그램 전체 설정
    public class AppSettings
    {
        // 출고지: 엑셀에 없는 값이라 고정으로 사용 (설정에서 변경 가능)
        public string ShippingSource { get; set; } = "하이포크";

        // 사용할 프린터 이름 (Windows에 설치된 프린터 이름). 비어있으면 화면에서 선택.
        public string PrinterName { get; set; } = "";

        // 라벨(양식지) 크기 (mm). 100 x 120, 세로 방향.
        public double LabelWidthMm { get; set; } = 100;
        public double LabelHeightMm { get; set; } = 120;

        // 한글 글꼴
        public string FontFamily { get; set; } = "맑은 고딕";

        // 양식지와 인쇄 위치가 살짝 어긋날 때 전체를 밀어주는 보정값 (mm)
        public double OffsetXMm { get; set; } = 0;
        public double OffsetYMm { get; set; } = 0;

        // 급지 방향 문제로 90도 회전이 필요하면 true
        public bool Rotate90 { get; set; } = false;

        // 각 칸의 인쇄 위치. Key는 칸 이름, Value는 위치/글꼴.
        // ※ 아래 좌표는 '기본 추정값'입니다. 실제 양식지에 맞춰 설정 화면에서 조정하세요.
        public Dictionary<string, LabelField> Fields { get; set; } = DefaultFields();

        // 칸 이름 상수 (오타 방지용)
        public static class Keys
        {
            public const string Title = "Title";                 // 화주명(상단 제목)
            public const string DeliveryCenter = "DeliveryCenter"; // 배송센터
            public const string ShippingSource = "ShippingSource"; // 출고지
            public const string DeliveryPlace = "DeliveryPlace";   // 납품처명
            public const string RequestDate = "RequestDate";       // 배송일자
            public const string StorageType = "StorageType";       // 보관유형
            public const string ItemName = "ItemName";             // 품목명
            public const string TotalQty = "TotalQty";             // 총수량
            public const string Qty = "Qty";                       // 수량(박스/총)
            public const string Sequence = "Sequence";             // 순번
            public const string Remark = "Remark";                 // 비고
        }

        // 기본 좌표값 (100 x 120mm 세로 기준 추정치 → 실제 양식지에 맞춰 보정 필요)
        public static Dictionary<string, LabelField> DefaultFields() => new()
        {
            { Keys.Title,          new LabelField(35, 8, 12f, true) },
            { Keys.DeliveryCenter, new LabelField(30, 25, 9f) },
            { Keys.ShippingSource, new LabelField(80, 25, 9f) },
            { Keys.DeliveryPlace,  new LabelField(30, 40, 9f) },
            { Keys.RequestDate,    new LabelField(30, 55, 9f) },
            { Keys.StorageType,    new LabelField(80, 55, 9f) },
            { Keys.ItemName,       new LabelField(30, 70, 10f, true) },
            { Keys.TotalQty,       new LabelField(20, 90, 10f, true) },
            { Keys.Qty,            new LabelField(45, 90, 10f, true) },
            { Keys.Sequence,       new LabelField(75, 90, 10f, true) },
            { Keys.Remark,         new LabelField(20, 105, 9f) },
        };

        // ── 저장/불러오기 ──
        // 설정 파일 위치: %APPDATA%\FarmscoLabel\settings.json (쓰기 권한 문제 없는 폴더)
        private static string SettingsPath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "FarmscoLabel");
                Directory.CreateDirectory(dir); // 폴더가 없으면 만든다
                return Path.Combine(dir, "settings.json");
            }
        }

        // 설정 파일을 읽어온다. 없으면 기본 설정을 만들어 준다.
        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    var s = JsonSerializer.Deserialize<AppSettings>(json);
                    if (s != null)
                    {
                        // 저장된 파일에 칸 정보가 비어 있으면 기본값으로 채운다(하위 호환)
                        if (s.Fields == null || s.Fields.Count == 0)
                            s.Fields = DefaultFields();
                        return s;
                    }
                }
            }
            catch
            {
                // 설정 파일이 깨졌더라도 프로그램은 기본값으로 계속 동작
            }
            return new AppSettings();
        }

        // 현재 설정을 파일로 저장한다.
        public void Save()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, options));
        }
    }
}
