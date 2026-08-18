using System;

namespace PixelRoad.Data
{
    /// <summary>
    /// Resources/PixelRoad/map_config.json 의 스키마.
    ///
    /// JSON은 주석을 지원하지 않으므로 각 값의 의미는 이 파일의 주석과
    /// docs/MAP_CONFIG.md 표를 기준으로 삼는다.
    /// JsonUtility는 JSON에 없는 필드는 여기 적힌 기본값을 그대로 쓰고,
    /// 여기 없는 JSON 키는 조용히 무시한다(그래서 "_docs" 같은 메모 키를 넣어도 안전하다).
    /// </summary>
    [Serializable]
    public sealed class MapConfig
    {
        // ---------------------------------------------------------------
        // 앱 기본
        // ---------------------------------------------------------------

        /// <summary>랜드마크 목록 JSON의 Resources 경로(확장자 제외).</summary>
        public string landmarksJsonResourcePath = "PixelRoad/landmarks";

        /// <summary>
        /// 거점 데이터가 분포하는 대략적인 위경도 범위.
        /// 지도 표시가 아니라 해금 판정용 공간 인덱스의 기준 위도를 잡는 데 쓰인다.
        /// 값이 유효하지 않으면 앱이 시작하지 않는다.
        /// </summary>
        public MapBounds bounds = new MapBounds();

        /// <summary>JSON visitRadius가 비었거나 0 이하일 때 적용할 기본 방문 반경(m).</summary>
        public float defaultUnlockRadiusMeters = 50f;

        // ---------------------------------------------------------------
        // 픽셀 필터
        // ---------------------------------------------------------------

        /// <summary>픽셀 필터의 최초 기본값. 사용자가 토글하면 PlayerPrefs 값이 우선한다.</summary>
        public bool enablePixelFilter = false;

        /// <summary>
        /// 픽셀 모드에서 지도 RenderTexture를 1/N 크기로 렌더한 뒤 점 샘플링으로 확대하는 배수.
        /// 클수록 거칠어지고 렌더 비용은 줄어든다. UI와 마커는 이 효과를 받지 않는다.
        /// </summary>
        public int pixelBlockSize = 4;

        // ---------------------------------------------------------------
        // 마커 · 아이콘
        // ---------------------------------------------------------------

        /// <summary>지도 위 거점(랜드마크) 마커 한 변의 크기(UI px, 캔버스 기준 해상도 1080x1920 기준).</summary>
        public int spotMarkerPixelSize = 56;

        /// <summary>지도 위 사용자 마커 한 변의 크기(UI px).</summary>
        public int userMarkerPixelSize = 44;

        /// <summary>
        /// 거점 마커의 최소 터치 판정 크기(UI px).
        /// 그림보다 크게 잡아 작은 아이콘도 손가락으로 누를 수 있게 한다.
        /// spotMarkerPixelSize보다 작으면 무시된다.
        /// </summary>
        public int markerTapMinimumPixelSize = 96;

        /// <summary>거점 아이콘 스프라이트를 찾을 Resources 폴더.</summary>
        public string spotIconResourceFolder = "PixelRoad/Icons";

        /// <summary>
        /// landmarks.json의 category 이름으로 지도 마커 아이콘을 못 찾았을 때 마지막으로 시도할 아이콘 이름.
        /// 이것도 없으면 코드로 생성한 도형 마커를 쓴다.
        /// </summary>
        public string defaultSpotIconName = "default";

        /// <summary>
        /// 도감에서 thumbnail 이미지가 없거나 아직 해금하지 않은 랜드마크에 쓸 대체 이미지 이름.
        /// 지도 마커에는 쓰이지 않는다.
        /// </summary>
        public string placeholderThumbnailName = "placeholder";

        /// <summary>사용자 위치 마커에 쓸 아이콘 이름. 없으면 원형 도형 마커로 대체된다.</summary>
        public string userIconName = "user";

        // ---------------------------------------------------------------
        // 라이브 벡터 지도
        // ---------------------------------------------------------------

        /// <summary>
        /// 라이브 벡터 지도(네트워크 타일) 사용 여부.
        /// 정적 지도 폴백을 제거했으므로 false로 두면 지도가 아예 표시되지 않고 안내 문구만 나온다.
        /// </summary>
        public bool enableLiveVectorMap = true;

        /// <summary>
        /// 릴리스 빌드에서도 라이브 벡터 지도를 허용할지.
        /// false면 에디터·개발 빌드에서만 지도가 동작한다(컴플라이언스 게이트, docs/CONTEST_COMPLIANCE.md 참고).
        /// 릴리스에서 지도를 쓰려면 이 값을 true로 두고 PIXELROAD_LIVE_VECTOR_MAP 스크립팅 심볼도 정의해야 한다.
        /// </summary>
        public bool allowLiveVectorMapInRelease = false;

        /// <summary>타일 제공자 식별자. 캐시 폴더 구분과 로그에 쓰인다.</summary>
        public string vectorTileProviderId = "osm-shortbread-development";

        /// <summary>벡터 타일 스키마 이름. 레이어 해석 규칙을 고른다.</summary>
        public string vectorTileSchema = "shortbread_v1";

        /// <summary>벡터 타일 URL 템플릿. {z}/{x}/{y}가 치환된다.</summary>
        public string vectorTileUrlTemplate = "https://vector.openstreetmap.org/shortbread_v1/{z}/{x}/{y}.mvt";

        /// <summary>서버가 제공하는 최소 타일 줌.</summary>
        public int vectorTileMinZoom = 5;

        /// <summary>
        /// 서버가 제공하는 최대 타일 줌.
        /// 화면 줌이 이보다 커지면 이 줌의 타일을 확대해서 쓴다(오버줌).
        /// </summary>
        public int vectorTileMaxZoom = 14;

        /// <summary>앱 시작 시 화면 줌 레벨.</summary>
        public float initialMapZoom = 15f;

        /// <summary>축소 한계 줌 레벨.</summary>
        public float minimumMapZoom = 5f;

        /// <summary>확대 한계 줌 레벨.</summary>
        public float maximumMapZoom = 18f;

        /// <summary>동시에 진행할 타일 다운로드 개수. 크면 빠르지만 모바일 네트워크 부담이 커진다.</summary>
        public int maxConcurrentTileRequests = 4;

        /// <summary>메모리에 유지할 타일 메시 개수. 초과분은 오래된 것부터 버린다.</summary>
        public int maxMemoryTileCount = 48;

        /// <summary>디스크 타일 캐시 상한(MB).</summary>
        public int maxDiskCacheMegabytes = 64;

        /// <summary>디스크 타일 캐시 사용 여부. false면 매번 네트워크에서 받는다.</summary>
        public bool enableDiskTileCache = true;

        /// <summary>지도 하단 저작자 표시 문구. 타일 제공자 라이선스상 필수.</summary>
        public string mapAttribution = "© OpenStreetMap contributors";

        /// <summary>저작자 표시를 눌렀을 때 열 URL. 비우면 버튼이 비활성화된다.</summary>
        public string mapAttributionUrl = "https://www.openstreetmap.org/copyright";

        // ---------------------------------------------------------------
        // 위치 · 해금
        // ---------------------------------------------------------------

        /// <summary>GPS에 요청할 목표 정확도(m). 작을수록 정확하지만 배터리를 더 쓴다.</summary>
        public float desiredAccuracyMeters = 15f;

        /// <summary>이 거리(m) 이상 움직였을 때만 위치 갱신을 받는다.</summary>
        public float locationUpdateDistanceMeters = 3f;

        // ---------------------------------------------------------------
        // 에디터 시뮬레이션 (빌드에는 영향 없음)
        // ---------------------------------------------------------------

        /// <summary>에디터 시뮬레이션 시작 위도. 앱 시작 시 지도 중심으로도 쓰인다.</summary>
        public double editorStartLatitude = 37.579617;

        /// <summary>에디터 시뮬레이션 시작 경도.</summary>
        public double editorStartLongitude = 126.977041;

        /// <summary>에디터에서 방향키로 이동할 때의 속도(m/s).</summary>
        public float editorMoveSpeedMetersPerSecond = 250f;

        /// <summary>에디터에서 가속 키를 눌렀을 때 이동 속도 배수.</summary>
        public float editorFastMoveMultiplier = 4f;
    }
}
