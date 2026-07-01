namespace FarmscoLabel.Models
{
    // 라벨 1장(=박스 1개)에 해당하는 데이터.
    // DeliveryRow 1건이 박스 개수만큼 여러 개의 LabelItem으로 '펼쳐집니다'.
    public class LabelItem
    {
        // ── 양식지에 인쇄할 값들 ──
        public string ShipperName { get; set; } = "";     // 상단 제목 (화주명)
        public string DeliveryCenter { get; set; } = "";  // 배송센터
        public string ShippingSource { get; set; } = "";  // 출고지 (고정값 "하이포크")
        public string DeliveryPlace { get; set; } = "";   // 납품처명
        public string RequestDate { get; set; } = "";     // 배송일자
        public string StorageTypeRaw { get; set; } = "";  // 보관유형
        public string ItemName { get; set; } = "";        // 품목명
        public string Remark { get; set; } = "";          // 비고

        // ── 넘버링 값들 ──
        public int TotalQty { get; set; }    // 총수량 (예: 420)
        public int BoxQty { get; set; }      // 이 박스의 수량 (예: 40 또는 잔량 20)
        public int CurrentBox { get; set; }  // 현재 순번 (예: 11)
        public int TotalBox { get; set; }    // 전체 박스 수 (예: 11)

        // 이 라벨이 '꽉 찬 박스'인지 여부.
        // true  = 입수량만큼 가득 찬 박스 → "박스"
        // false = 입수량보다 적은 잔량 박스 → "낱개"
        public bool IsFullBox { get; set; }

        // ── 화면/인쇄에서 쓰기 좋은 형태로 가공한 글자 ──

        // 구분 칸: "박스" 또는 "낱개"
        public string PackTypeText => IsFullBox ? "박스" : "낱개";

        // 수량 칸: "40 / 420" 처럼 표기
        public string QtyText => $"{BoxQty} / {TotalQty}";

        // 순번 칸: "11 / 11" 처럼 표기
        public string SequenceText => $"{CurrentBox} / {TotalBox}";
    }
}
