namespace PixelRoad.Data
{
    /// <summary>
    /// landmarks.json의 category 표기를 화면에서 쓰는 5종으로 정리한다.
    ///
    /// 데이터에 초기 영문 표기(<c>station</c>, <c>test</c>)가 남아 있어 그대로 세면
    /// 도감 필터 칩과 리포트 집계가 7종으로 갈라진다. 표시용 이름만 여기서 맞추고,
    /// 마커 아이콘 키는 파싱 시점에 원본 category로 이미 정해지므로 영향을 받지 않는다.
    /// </summary>
    public static class SpotCategory
    {
        /// <summary>도감 필터와 리포트 집계가 함께 쓰는 표시 순서.</summary>
        public static readonly string[] DisplayOrder = { "역사", "문화", "교통", "공공", "테스트" };

        /// <summary>원본 category를 표시용 이름으로 바꾼다. 아는 표기가 아니면 그대로 돌려준다.</summary>
        public static string Normalize(string rawCategory)
        {
            if (string.IsNullOrWhiteSpace(rawCategory))
            {
                return string.Empty;
            }

            string trimmed = rawCategory.Trim();
            switch (trimmed)
            {
                case "station":
                    return "교통";
                case "test":
                    return "테스트";
                case "culture":
                    return "문화";
                case "history":
                    return "역사";
                case "public":
                    return "공공";
                default:
                    return trimmed;
            }
        }

        /// <summary><see cref="DisplayOrder"/>에서의 위치. 목록에 없으면 -1.</summary>
        public static int IndexOf(string displayCategory)
        {
            for (int i = 0; i < DisplayOrder.Length; i++)
            {
                if (DisplayOrder[i] == displayCategory)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
