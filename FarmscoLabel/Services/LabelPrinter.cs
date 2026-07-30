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
        // progress: 지금까지 인쇄한 장수(1..총장수)를 보고받고 싶을 때 전달(선택).
        public void Print(List<LabelItem> labels, string? printerName = null, IProgress<int>? progress = null)
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
                // 표 선과 글자를 매끄럽게
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                // 라벨은 항상 90도 회전 출력(기존 라벨 양식과 동일 방향).
                // 인쇄 페이지는 100(폭)x120(급지) 그대로 두고, 내용은 120x100 가로형 캔버스로
                // 그린 뒤 90도 돌려 페이지에 얹는다(헤드 폭 100mm를 넘지 않아 잘림 없음).
                g.TranslateTransform((float)_settings.LabelWidthMm, 0);
                g.RotateTransform(90);

                // 회전 후 캔버스 크기: 가로=라벨세로(120), 세로=라벨가로(100)
                DrawLabel(g, labels[index], (float)_settings.LabelHeightMm, (float)_settings.LabelWidthMm);

                index++;
                // 한 장 찍을 때마다 진행 상황 보고 (예: 3/120장)
                progress?.Report(index);
                // 아직 남은 라벨이 있으면 다음 페이지를 계속 인쇄
                e.HasMorePages = index < labels.Count;
            };

            doc.Print();
        }

        // 라벨 한 장(표 전체)을 그린다.
        // 양식지가 아니라 빈 라벨지에 인쇄하므로, 테두리·항목명·값을 모두 직접 그린다.
        // W, H = 그릴 캔버스 크기(mm). 90도 회전 출력이라 가로=라벨세로, 세로=라벨가로가 넘어온다.
        private void DrawLabel(Graphics g, LabelItem label, float W, float H)
        {
            // 인쇄 시작점 보정(회전 기준, 필요 시 조정) + 설정값(OffsetX/Y)
            const float startX = 0f;  // 시작점 X 보정(mm)
            const float startY = 2f;  // 시작점 Y 보정(mm)
            g.TranslateTransform((float)_settings.OffsetXMm + startX, (float)_settings.OffsetYMm + startY);

            float m = 3f;                 // 바깥 여백(mm)
            float left = m;
            float right = W - m;
            float top = m;
            float tableW = right - left;

            float labelW = 20f;           // 왼쪽 '항목명' 칸 너비
            float xVal = left + labelW;   // 값 영역 시작 X
            float valW = right - xVal;    // 값 영역 너비

            using var pen = new Pen(Color.Black, 0.3f);

            // 글자 크기(pt) — 한 곳에서 관리
            const float fsTitle = 19f, fsLabel = 13f, fsValue = 13f,
                        fsItem = 14f, fsQtyHead = 12f, fsQtyVal = 14f;

            // ── 각 행 높이(mm): 기준(114mm) 레이아웃을 캔버스 높이에 비례 스케일 ──
            float available = H - 2 * m;
            float scale = available / 114f;
            float hTitle = 13f * scale;   // 제목: (주)팜스코
            float hRow = 13f * scale;     // 일반 행(배송센터/납품처명/배송일자)
            float hItem = 15f * scale;    // 품목명
            float hQtyHead = 9f * scale;  // 수량 헤더(총수량/수량/순번)
            float hQtyVal = 12f * scale;  // 수량 값
            float used = hTitle + hRow * 3 + hItem + hQtyHead + hQtyVal;
            float hRemark = available - used; // 비고: 나머지 전부
            if (hRemark < 10f) hRemark = 10f;

            float y = top;

            // 1) 제목: (주)팜스코 — 전체 너비, 가운데, 크게
            Cell(g, pen, left, y, tableW, hTitle);
            DrawText(g, label.ShipperName, left, y, tableW, hTitle, fsTitle, bold: true, hCenter: true);
            y += hTitle;

            // 2) 배송센터 | 값(왼쪽) | 출고지 | 값(왼쪽)  — 항목명 볼드 없음
            DrawFourCol(g, pen, left, xVal, right, y, hRow, labelW, fsLabel, fsValue,
                "배송센터", label.DeliveryCenter, valueLeft1: true,
                "출고지", label.ShippingSource, valueLeft2: true, labelBold: false);
            y += hRow;

            // 3) 납품처명 | 값(왼쪽)  — 항목명 볼드 없음
            DrawLabelValue(g, pen, left, xVal, y, labelW, valW, hRow, fsLabel, fsValue,
                "납품처명", label.DeliveryPlace, valueLeft: true, labelBold: false);
            y += hRow;

            // 4) 배송일자 | 값(가운데) | 보관유형 | 값(가운데)  — 항목명 볼드 없음
            DrawFourCol(g, pen, left, xVal, right, y, hRow, labelW, fsLabel, fsValue,
                "배송일자", label.RequestDate, valueLeft1: false,
                "보관유형", label.StorageTypeRaw, valueLeft2: false, labelBold: false);
            y += hRow;

            // 5) 품목명 | 값(왼쪽)
            DrawLabelValue(g, pen, left, xVal, y, labelW, valW, hItem, fsLabel, fsItem,
                "품목명", label.ItemName, valueLeft: true);
            y += hItem;

            // 6) 수량 | [총수량/수량/순번] 헤더 + 값 (수량 라벨은 두 줄 높이 병합)
            float hQty = hQtyHead + hQtyVal;
            Cell(g, pen, left, y, labelW, hQty);
            DrawText(g, "수량", left, y, labelW, hQty, fsLabel, bold: true, hCenter: true);

            float c1 = valW * 0.28f;      // 총수량
            float c2 = valW * 0.40f;      // 수량(박스/총)
            float c3 = valW - c1 - c2;    // 순번
            // 헤더
            Cell(g, pen, xVal, y, c1, hQtyHead);       DrawText(g, "총수량", xVal, y, c1, hQtyHead, fsQtyHead, true, true);
            Cell(g, pen, xVal + c1, y, c2, hQtyHead);  DrawText(g, "수량", xVal + c1, y, c2, hQtyHead, fsQtyHead, true, true);
            Cell(g, pen, xVal + c1 + c2, y, c3, hQtyHead); DrawText(g, "순번", xVal + c1 + c2, y, c3, hQtyHead, fsQtyHead, true, true);
            // 값
            float yv = y + hQtyHead;
            Cell(g, pen, xVal, yv, c1, hQtyVal);       DrawText(g, label.TotalQty.ToString(), xVal, yv, c1, hQtyVal, fsQtyVal, true, true);
            Cell(g, pen, xVal + c1, yv, c2, hQtyVal);  DrawText(g, label.QtyText, xVal + c1, yv, c2, hQtyVal, fsQtyVal, true, true);
            Cell(g, pen, xVal + c1 + c2, yv, c3, hQtyVal); DrawText(g, label.SequenceText, xVal + c1 + c2, yv, c3, hQtyVal, fsQtyVal, true, true);
            y += hQty;

            // 7) 비고 | 값(왼쪽·세로 가운데)
            Cell(g, pen, left, y, labelW, hRemark);
            DrawText(g, "비고", left, y, labelW, hRemark, fsLabel, bold: true, hCenter: true);
            Cell(g, pen, xVal, y, valW, hRemark);
            DrawText(g, label.Remark, xVal, y, valW, hRemark, fsValue, bold: true, hCenter: false, vCenter: true);
        }

        // '항목명 | 값' 한 줄을 그린다. (값은 볼드, valueLeft=true면 왼쪽정렬)
        // labelBold=false면 항목명(왼쪽 칸)은 보통 굵기로 그린다.
        private void DrawLabelValue(Graphics g, Pen pen, float left, float xVal, float y,
            float labelW, float valW, float h, float labelSize, float valueSize,
            string labelText, string? value, bool valueLeft, bool labelBold = true)
        {
            Cell(g, pen, left, y, labelW, h);
            DrawText(g, labelText, left, y, labelW, h, labelSize, bold: labelBold, hCenter: true);
            Cell(g, pen, xVal, y, valW, h);
            DrawText(g, value, xVal, y, valW, h, valueSize, bold: true, hCenter: !valueLeft);
        }

        // '항목명 | 값 | 항목명2 | 값2' (4칸) 한 줄을 그린다. (값은 볼드)
        // labelBold=false면 항목명(l1,l2)은 보통 굵기로 그린다.
        private void DrawFourCol(Graphics g, Pen pen, float left, float xVal, float right, float y,
            float h, float labelW, float labelSize, float valueSize,
            string l1, string? v1, bool valueLeft1, string l2, string? v2, bool valueLeft2, bool labelBold = true)
        {
            float v1w = 42f;                 // 첫 값 칸 너비(가로형 캔버스라 넓게)
            float l2w = 20f;                 // 둘째 항목명 칸 너비("보관유형"이 한 줄에 들어가도록)
            float x2 = xVal + v1w;           // 둘째 항목명 시작
            float x3 = x2 + l2w;             // 둘째 값 시작
            float v2w = right - x3;

            Cell(g, pen, left, y, labelW, h);       DrawText(g, l1, left, y, labelW, h, labelSize, labelBold, true);
            Cell(g, pen, xVal, y, v1w, h);          DrawText(g, v1, xVal, y, v1w, h, valueSize, true, hCenter: !valueLeft1);
            Cell(g, pen, x2, y, l2w, h);            DrawText(g, l2, x2, y, l2w, h, labelSize, labelBold, true);
            Cell(g, pen, x3, y, v2w, h);            DrawText(g, v2, x3, y, v2w, h, valueSize, true, hCenter: !valueLeft2);
        }

        // 한 칸(테두리 사각형)을 그린다.
        private static void Cell(Graphics g, Pen pen, float x, float y, float w, float h)
            => g.DrawRectangle(pen, x, y, w, h);

        // 칸 안에 글자를 그린다.
        // - 칸 너비를 넘으면 자동 줄바꿈(word wrap)
        // - 그래도 칸 높이를 넘으면 글자 크기를 조금씩 줄여 영역 안에 맞춘다
        private void DrawText(Graphics g, string? text, float x, float y, float w, float h,
            float baseSize, bool bold, bool hCenter, bool vCenter = true)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            const float pad = 1f; // 칸 안쪽 여백(mm)
            var rect = new RectangleF(x + pad, y + pad, w - 2 * pad, h - 2 * pad);
            if (rect.Width <= 0 || rect.Height <= 0) return;

            using var fmt = new StringFormat
            {
                Alignment = hCenter ? StringAlignment.Center : StringAlignment.Near,
                LineAlignment = vCenter ? StringAlignment.Center : StringAlignment.Near,
                Trimming = StringTrimming.Character, // 최후엔 잘림 대신 문자단위 처리
            };

            var style = bold ? FontStyle.Bold : FontStyle.Regular;
            float size = baseSize;

            // 영역 높이에 맞을 때까지 글자 크기를 낮춘다(최소 5pt)
            while (size > 5f)
            {
                using var probe = new Font(_settings.FontFamily, size, style, GraphicsUnit.Point);
                var measured = g.MeasureString(text, probe, new SizeF(rect.Width, 100000f), fmt);
                if (measured.Height <= rect.Height)
                    break;
                size -= 0.5f;
            }

            using var font = new Font(_settings.FontFamily, Math.Max(size, 5f), style, GraphicsUnit.Point);
            g.DrawString(text, font, Brushes.Black, rect, fmt);
        }

        // 밀리미터를 1/100 인치로 변환 (PaperSize가 요구하는 단위)
        private static int MmToHundredthInch(double mm) => (int)Math.Round(mm / 25.4 * 100.0);
    }
}
