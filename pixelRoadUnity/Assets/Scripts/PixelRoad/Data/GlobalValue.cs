namespace PixelRoad.Data
{
    /// <summary>
    /// 씬과 시스템을 넘어 공유하는 전역 값 모음.
    ///
    /// AR처럼 지도 UI와 참조를 주고받기 어려운 곳에서도 "지금 무엇을 고른 상태인지"를 알아야 해서
    /// 선택 상태만 여기에 둔다. 값을 늘릴 때는 정말 전역이어야 하는지 먼저 따져 본다.
    /// </summary>
    public static class GlobalValue
    {
        /// <summary>지도에서 선택 중인 랜드마크. 선택이 없으면 null.</summary>
        public static SpotRuntimeState SelectedSpot { get; set; }

        /// <summary>선택 상태를 비운다. 지도 화면을 새로 세울 때 호출한다.</summary>
        public static void Clear()
        {
            SelectedSpot = null;
        }
    }
}
