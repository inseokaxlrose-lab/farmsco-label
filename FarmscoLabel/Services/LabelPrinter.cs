using System.Drawing;
using System.Drawing.Printing;
using FarmscoLabel.Models;

namespace FarmscoLabel.Services
{
    // 라벨(LabelItem) 목록을 선인쇄 양식지에 '값만' 찍어 인쇄하는 도구.
    public class LabelPrinter
    {
        private readonly AppSettings _settings;

        public LabelPrinter(AppSettings settings)
        {
            _settings = settings;
        }

        // Windows에 설치된 프린터 이름 목록을 돌려준다 (화면 드롭다운용).
        public static List<string> GetInstalledPrinters()
        {
            var list = new List<string>();
            foreach (string name in PrinterSettings.InstalledPrinters)
                list.Add(name);
            return list;
        }

        // 라벨 목록을 인쇄한다. printerName이 비어있으면 설정값/기본 프린터 사용.
        public void Print(List<LabelItem> labels, string? printerName = null)
        {
            if (labels == null || labels.Count == 0)
                return;

            // 인쇄 문서 준비
            using var doc = new PrintDocument();

            // 프린터 지정 (우선순위: 인자 > 설정값 > 시스템 기본)
            string target = printerName ?? _settings.PrinterName;
            if (!string.IsNullOrWhiteSpace(target))
                doc.PrinterSettings.PrinterName = target;

            // 라벨 용지 크기 지정 (mm -> 1/100 인치 단위로 변환)
            int wHundredth = MmToHundredthInch(_settings.LabelWidthMm);
            int hHundredth = MmToHundredthInch(_settings.LabelHeightMm);
            doc.DefaultPageSettings.PaperSize = new PaperSize("Farmsco", wHundredth, hHundredth);
            doc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0); // 여백 0
            doc.DefaultPageSettings.Landscape = false;                 // 세로 방향
            doc.OriginAtMargins = false;

            // 인쇄할 라벨을 한 장씩 넘기기 위한 위치 표시(인덱스)
            int index = 0;

            // 한 페이지(라벨 한 장)를 그릴 때마다 호출되는 부분
            doc.PrintPage += (sender, e) =>
            {
                var g = e.Graphics!;
                // 좌표 단위를 밀리미터로 설정 → 이후 X,Y를 mm로 다룰 수 있음
                g.PageUnit = GraphicsUnit.Millimeter;

                // 급지 방향 문제로 회전이 필요하면 좌표계를 90도 돌린다
                if (_settings.Rotate90)
                {
                    g.TranslateTransform((float)_settings.LabelWidthMm, 0);
                    g.RotateTransform(90);
                }

                DrawLabel(g, labels[index]);

                index++;
                // 아직 남은 라벨이 있으면 다음 페이지를 계속 인쇄
                e.HasMorePages = index < labels.Count;
            };

            doc.Print();
        }

        // 라벨 한 장의 값들을 각 좌표에 그린다.
        private void DrawLabel(Graphics g, LabelItem label)
        {
            var f = _settings.Fields;

            // 칸 이름별로 값을 하나씩 그린다.
            DrawField(g, f, AppSettings.Keys.Title, label.ShipperName);
            DrawField(g, f, AppSettings.Keys.DeliveryCenter, label.DeliveryCenter);
            DrawField(g, f, AppSettings.Keys.ShippingSource, label.ShippingSource);
            DrawField(g, f, AppSettings.Keys.DeliveryPlace, label.DeliveryPlace);
            DrawField(g, f, AppSettings.Keys.RequestDate, label.RequestDate);
            DrawField(g, f, AppSettings.Keys.StorageType, label.StorageTypeRaw);
            DrawField(g, f, AppSettings.Keys.ItemName, label.ItemName);
            DrawField(g, f, AppSettings.Keys.TotalQty, label.TotalQty.ToString());
            DrawField(g, f, AppSettings.Keys.Qty, label.QtyText);         // "40 / 420"
            DrawField(g, f, AppSettings.Keys.Sequence, label.SequenceText); // "11 / 11"
            DrawField(g, f, AppSettings.Keys.Remark, label.Remark);
        }

        // 한 칸의 값을 정해진 위치/글꼴로 그린다.
        private void DrawField(Graphics g, Dictionary<string, LabelField> fields, string key, string? value)
        {
            if (string.IsNullOrEmpty(value)) return;             // 값 없으면 안 그림
            if (!fields.TryGetValue(key, out var pos)) return;   // 위치 정보 없으면 건너뜀

            var style = pos.Bold ? FontStyle.Bold : FontStyle.Regular;
            using var font = new Font(_settings.FontFamily, pos.FontSize, style, GraphicsUnit.Point);

            // 전체 보정값(OffsetX/Y)을 더해 위치를 살짝 이동시킬 수 있음
            float x = (float)(pos.XMm + _settings.OffsetXMm);
            float y = (float)(pos.YMm + _settings.OffsetYMm);

            g.DrawString(value, font, Brushes.Black, x, y);
        }

        // 밀리미터를 1/100 인치로 변환 (PaperSize가 요구하는 단위)
        private static int MmToHundredthInch(double mm) => (int)Math.Round(mm / 25.4 * 100.0);
    }
}
