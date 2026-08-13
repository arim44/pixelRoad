using System;

namespace PixelRoad.Data
{
    /// <summary>
    /// Resources/PixelRoad/ar_config.json 의 스키마.
    ///
    /// MapConfig와 별개로 관리한다 - ARScene은 MapScene의 MapConfig/맵 UI 설정에
    /// 의존하지 않고 독립적으로 동작해야 하기 때문이다.
    /// JsonUtility는 JSON에 없는 필드는 여기 적힌 기본값을 그대로 쓰고,
    /// 여기 없는 JSON 키는 조용히 무시한다.
    /// </summary>
    [Serializable]
    public sealed class ARConfig
    {
        // ---------------------------------------------------------------
        // 표시 범위 · 나침반
        // ---------------------------------------------------------------

        /// <summary>AR 화면에 표시할 거점의 최대 반경(m). 이 반경 밖 거점은 스냅샷에서 제외되거나 화면에서 숨겨진다.</summary>
        public float arDisplayRadiusMeters = 1500f;

        /// <summary>나침반 방위각 스무딩 계수(0~1). 클수록 반응이 빠르고 흔들림도 커진다.</summary>
        public float headingSmoothingFactor = 0.15f;

        // ---------------------------------------------------------------
        // 아이콘 · UI
        // ---------------------------------------------------------------

        /// <summary>AR 화면 랜드마크 아이콘 한 변의 크기(UI px).</summary>
        public int iconPixelSize = 96;

        /// <summary>화면 가장자리 방향 화살표 크기(UI px).</summary>
        public int edgeArrowPixelSize = 64;

        /// <summary>가장자리 화살표를 화면 테두리에서 띄우는 여백(px).</summary>
        public float edgeMarginPixels = 48f;

        /// <summary>같은 쪽(좌/우) 가장자리 화살표가 여러 개 겹칠 때 세로로 떨어뜨려 쌓는 간격(px).</summary>
        public float edgeStackSpacingPixels = 100f;

        /// <summary>거점 아이콘 스프라이트를 찾을 Resources 폴더. MapConfig의 동일 값과 같은 폴더를 가리키되 독립적으로 설정한다.</summary>
        public string spotIconResourceFolder = "PixelRoad/Icons";

        /// <summary>아이콘 키·카테고리로 못 찾았을 때 마지막으로 시도할 아이콘 이름.</summary>
        public string defaultSpotIconName = "default";

        // ---------------------------------------------------------------
        // 위치
        // ---------------------------------------------------------------

        /// <summary>GPS에 요청할 목표 정확도(m).</summary>
        public float desiredAccuracyMeters = 15f;

        /// <summary>이 거리(m) 이상 움직였을 때만 위치 갱신을 받는다.</summary>
        public float locationUpdateDistanceMeters = 3f;
    }
}
