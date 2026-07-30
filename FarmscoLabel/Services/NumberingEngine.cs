using FarmscoLabel.Models;

namespace FarmscoLabel.Services
{
    // 넘버링(박스 분할) 계산기.
    // 엑셀 한 줄(DeliveryRow)을 박스 개수만큼 라벨(LabelItem) 여러 장으로 '펼칩니다'.
    public static class NumberingEngine
    {
        // row 한 건을 라벨 목록으로 펼친다.
        // shippingSource: 출고지 고정값(예: "하이포크")
        // showBoxUnitQty: 품목명 뒤에 "(입수량)"을 붙일지 여부
        public static List<LabelItem> Expand(DeliveryRow row, string shippingSource, bool showBoxUnitQty = true)
        {
            var result = new List<LabelItem>();

            int total = row.Quantity;      // 총수량
            int unit = row.BoxUnitQty;     // 박스당 입수수량
            int totalBox = row.BoxCount;   // 전체 박스 수 (DeliveryRow가 계산해 둔 값)

            // 수량이 없으면 라벨도 없음
            if (total <= 0 || totalBox <= 0)
                return result;

            // 입수수량이 없으면 통째로 1박스로 처리
            if (unit <= 0)
                unit = total;

            int full = total / unit;       // 꽉 찬 박스 개수
            int remain = total % unit;     // 마지막 잔량

            // 1) 꽉 찬 박스들: 각 박스 수량 = unit, 순번 1/N ~ full/N → "박스"
            for (int i = 1; i <= full; i++)
            {
                result.Add(MakeLabel(row, shippingSource, total, unit, i, totalBox, isFullBox: true, showBoxUnitQty));
            }

            // 2) 잔량 박스: 마지막 1장, 박스 수량 = remain, 순번 = N/N → "낱개"
            if (remain > 0)
            {
                result.Add(MakeLabel(row, shippingSource, total, remain, totalBox, totalBox, isFullBox: false, showBoxUnitQty));
            }

            return result;
        }

        // 여러 줄을 한꺼번에 펼치기 (필터된 전체를 출력할 때 사용)
        public static List<LabelItem> ExpandMany(IEnumerable<DeliveryRow> rows, string shippingSource, bool showBoxUnitQty = true)
        {
            var list = new List<LabelItem>();
            foreach (var row in rows)
                list.AddRange(Expand(row, shippingSource, showBoxUnitQty));
            return list;
        }

        // 라벨 1장 만들기 (값 채우기)
        private static LabelItem MakeLabel(
            DeliveryRow row, string shippingSource,
            int totalQty, int boxQty, int currentBox, int totalBox, bool isFullBox, bool showBoxUnitQty)
        {
            return new LabelItem
            {
                ShipperName = row.ShipperName,
                DeliveryCenter = row.DeliveryCenter,
                ShippingSource = shippingSource,
                DeliveryPlace = row.DeliveryPlace,
                RequestDate = row.RequestDate,
                StorageTypeRaw = row.StorageTypeRaw,
                ItemName = BuildItemName(row.ItemName, row.BoxUnitQty, showBoxUnitQty), // 품목명 뒤에 (입수량) 붙임(설정 시)
                Remark = row.Remark,
                TotalQty = totalQty,
                BoxQty = boxQty,
                CurrentBox = currentBox,
                TotalBox = totalBox,
                IsFullBox = isFullBox,
            };
        }

        // 품목명 뒤에 "(입수량)"을 붙인다.
        // 예) "우유 500ml" + 입수 40 -> "우유 500ml (40)"
        // 이미 "(40)"이 붙어 있으면 중복으로 붙이지 않는다.
        // showBoxUnitQty=false 이면 입수량을 붙이지 않고 품목명만 반환한다.
        private static string BuildItemName(string itemName, int boxUnitQty, bool showBoxUnitQty)
        {
            string name = (itemName ?? "").Trim();
            if (!showBoxUnitQty || boxUnitQty <= 0) return name;

            string suffix = $"({boxUnitQty})";
            if (name.Replace(" ", "").EndsWith(suffix)) return name; // 이미 붙어 있으면 그대로

            return $"{name} {suffix}";
        }
    }
}
