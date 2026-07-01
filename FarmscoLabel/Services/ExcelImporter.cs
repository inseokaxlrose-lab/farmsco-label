using ClosedXML.Excel;
using FarmscoLabel.Models;

namespace FarmscoLabel.Services
{
    // 엑셀(.xlsx)을 읽어서 DeliveryRow 목록으로 바꿔주는 도구.
    public static class ExcelImporter
    {
        // 엑셀 파일 경로를 받아 데이터 목록을 돌려준다.
        // progress: 0~100 사이의 진행률을 보고받고 싶을 때 전달(선택).
        public static List<DeliveryRow> Import(string filePath, IProgress<int>? progress = null)
        {
            var rows = new List<DeliveryRow>();

            // 엑셀 파일 열기 (using: 다 쓰면 자동으로 파일을 닫아줌)
            using var workbook = new XLWorkbook(filePath);

            // "주문현황" 탭을 찾는다. 없으면 첫 번째 시트를 사용.
            IXLWorksheet? ws = null;
            foreach (var sheet in workbook.Worksheets)
            {
                string name = (sheet.Name ?? "").Replace(" ", "").Trim();
                if (name.Contains("주문현황"))
                {
                    ws = sheet;
                    break;
                }
            }
            ws ??= workbook.Worksheet(1);

            // 실제로 값이 있는 영역만 가져오기
            var range = ws.RangeUsed();
            if (range == null) return rows; // 빈 시트면 그대로 반환

            // 첫 줄을 '헤더(제목 줄)'로 보고, 어떤 열이 어떤 값인지 위치를 찾는다.
            var headerRow = range.FirstRow();
            var map = BuildColumnMap(headerRow);

            // 진행률 계산용: 데이터 줄 수(헤더 제외)
            int totalRows = Math.Max(1, range.RowCount() - 1);
            int done = 0;
            int lastReported = -1;

            // 헤더 다음 줄부터 끝까지 한 줄씩 읽는다.
            foreach (var row in range.Rows().Skip(1))
            {
                // 진행률 보고 (매 줄마다 UI로 보내면 과하니 % 값이 바뀔 때만 보고)
                done++;
                if (progress != null)
                {
                    int percent = (int)(done * 100L / totalRows);
                    if (percent != lastReported)
                    {
                        lastReported = percent;
                        progress.Report(percent);
                    }
                }

                var item = new DeliveryRow
                {
                    ShipperName = GetText(row, map, "ShipperName"),
                    DeliveryPlace = GetText(row, map, "DeliveryPlace"),
                    DeliveryCenter = GetText(row, map, "DeliveryCenter"),
                    RequestDate = GetText(row, map, "RequestDate"),
                    Quantity = GetInt(row, map, "Quantity"),
                    Remark = GetText(row, map, "Remark"),
                    ItemName = GetText(row, map, "ItemName"),
                    StorageTypeRaw = GetText(row, map, "StorageTypeRaw"),
                    BoxUnitQty = GetInt(row, map, "BoxUnitQty"),
                };

                // 완전히 빈 줄은 건너뛴다
                if (string.IsNullOrWhiteSpace(item.ItemName) &&
                    string.IsNullOrWhiteSpace(item.DeliveryPlace) &&
                    item.Quantity == 0)
                    continue;

                rows.Add(item);
            }

            return rows;
        }

        // 헤더 줄을 보고 "우리 필드 이름 -> 엑셀 열 번호"를 만든다.
        private static Dictionary<string, int> BuildColumnMap(IXLRangeRow headerRow)
        {
            var map = new Dictionary<string, int>();

            foreach (var cell in headerRow.Cells())
            {
                // 헤더 글자에서 공백/줄바꿈 제거 (예: "BOX\n입수수량" -> "BOX입수수량")
                string h = (cell.GetString() ?? "").Replace(" ", "").Replace("\n", "").Trim();
                int col = cell.Address.ColumnNumber;

                // 어떤 필드인지 글자에 포함된 키워드로 판별
                if (h.Contains("화주")) map["ShipperName"] = col;
                else if (h.Contains("납품처")) map["DeliveryPlace"] = col;
                else if (h.Contains("배송센터")) map["DeliveryCenter"] = col;
                else if (h.Contains("요청") || h.Contains("일자")) map["RequestDate"] = col;
                else if (h.Contains("입수")) map["BoxUnitQty"] = col;   // '입수'를 '수량'보다 먼저 확인
                else if (h.Contains("수량")) map["Quantity"] = col;
                else if (h.Contains("비고")) map["Remark"] = col;
                else if (h.Contains("품목")) map["ItemName"] = col;
                else if (h.Contains("보관")) map["StorageTypeRaw"] = col;
            }

            return map;
        }

        // 특정 필드의 열에서 '글자'를 읽어온다. 열을 못 찾으면 빈 문자열.
        private static string GetText(IXLRangeRow row, Dictionary<string, int> map, string key)
        {
            if (!map.TryGetValue(key, out int col)) return "";
            var cell = row.Cell(col - row.RangeAddress.FirstAddress.ColumnNumber + 1);
            return (cell.GetString() ?? "").Trim();
        }

        // 특정 필드의 열에서 '정수'를 읽어온다. 숫자가 아니면 0.
        private static int GetInt(IXLRangeRow row, Dictionary<string, int> map, string key)
        {
            string text = GetText(row, map, key);
            if (string.IsNullOrWhiteSpace(text)) return 0;

            // 소수점/쉼표가 섞여 있을 수 있으니 정리 후 변환
            text = text.Replace(",", "").Trim();
            if (int.TryParse(text, out int v)) return v;
            if (double.TryParse(text, out double d)) return (int)Math.Round(d);
            return 0;
        }
    }
}
