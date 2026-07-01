namespace FarmscoLabel.Models
{
    // 보관유형을 3가지로 구분하는 종류(enum).
    // enum = "정해진 몇 가지 중 하나"만 담는 상자라고 생각하면 돼요.
    public enum StorageCategory
    {
        상온,
        냉장,
        냉동,
        기타   // 위 3가지로 분류가 안 될 때(빈 값 등)
    }

    // 엑셀에 적힌 글자("상온보관" 등)를 StorageCategory로 바꿔주는 도우미
    public static class StorageClassifier
    {
        // 예: "냉장보관" -> StorageCategory.냉장
        public static StorageCategory Classify(string? raw)
        {
            // 값이 비어 있으면 '기타'로 처리
            if (string.IsNullOrWhiteSpace(raw))
                return StorageCategory.기타;

            // 글자 안에 특정 단어가 들어 있는지로 판별 ("냉동보관"에도 "냉동"이 들어있음)
            if (raw.Contains("냉동")) return StorageCategory.냉동;
            if (raw.Contains("냉장")) return StorageCategory.냉장;
            if (raw.Contains("상온")) return StorageCategory.상온;

            return StorageCategory.기타;
        }
    }
}
