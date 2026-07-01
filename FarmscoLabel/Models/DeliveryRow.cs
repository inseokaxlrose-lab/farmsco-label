namespace FarmscoLabel.Models
{
    // 엑셀 한 줄(배송 데이터 1건)을 담는 클래스.
    // 클래스 = 관련된 값들을 하나로 묶어 놓은 '상자'.
    public class DeliveryRow
    {
        // ── 엑셀에서 그대로 읽어오는 값들 ──
        public string ShipperName { get; set; } = "";     // 화주명 (예: (주)팜스코)
        public string DeliveryPlace { get; set; } = "";    // 납품처명
        public string DeliveryCenter { get; set; } = "";   // 배송센터
        public string RequestDate { get; set; } = "";      // 요청일자 (예: 20260630)
        public int Quantity { get; set; }                  // 수량 = 총수량
        public string Remark { get; set; } = "";           // 비고
        public string ItemName { get; set; } = "";         // 품목명 (뒤 (숫자)는 입수량)
        public string StorageTypeRaw { get; set; } = "";   // 보관유형 원본 글자 (예: 냉장보관)
        public int BoxUnitQty { get; set; }                // BOX 입수수량 (박스 1개에 들어가는 개수)

        // ── 계산으로 얻는 값들 (읽기 전용) ──

        // 보관유형을 3종류로 분류한 값 (필터에 사용)
        public StorageCategory Category => StorageClassifier.Classify(StorageTypeRaw);

        // 박스수량 = 이 데이터가 라벨 몇 장으로 나뉘는가
        // 예) 총수량 420, 입수 40 -> 11박스 (완박스 10 + 잔량 1)
        public int BoxCount
        {
            get
            {
                // 입수수량이 0 이하이면(값이 없으면) 나눌 수 없으니 통째로 1박스 처리
                if (BoxUnitQty <= 0)
                    return Quantity > 0 ? 1 : 0;

                int full = Quantity / BoxUnitQty;      // 꽉 찬 박스 개수 (몫)
                int remain = Quantity % BoxUnitQty;    // 남는 개수 (나머지)
                return full + (remain > 0 ? 1 : 0);    // 나머지가 있으면 박스 1개 추가
            }
        }
    }
}
