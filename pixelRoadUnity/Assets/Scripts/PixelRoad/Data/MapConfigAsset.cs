using System;
using UnityEngine;

namespace PixelRoad.Data
{
    /// <summary>
    /// map_config.json 으로 조정하던 값들을 인스펙터에서 편집하는 ScriptableObject.
    ///
    /// 필드 이름과 설명을 한국어로 두어 기획·디자인 쪽에서 바로 읽고 고칠 수 있게 했다.
    /// 런타임 코드는 계속 <see cref="MapConfig"/>를 쓰고, 이 에셋은 <see cref="ToMapConfig"/>로 변환만 한다.
    ///
    /// 로드 순서: Resources/PixelRoad/MapConfig.asset 이 있으면 그것을 쓰고,
    /// 없으면 기존 Resources/PixelRoad/map_config.json 으로 넘어간다.
    /// </summary>
    [CreateAssetMenu(menuName = "Pixel Road/지도 설정", fileName = "MapConfig")]
    public sealed class MapConfigAsset : ScriptableObject
    {
        /// <summary>거점 데이터가 분포하는 위경도 범위.</summary>
        [Serializable]
        public sealed class 좌표범위
        {
            [Tooltip("북쪽 끝 위도.")]
            public double 북위 = 37.5868009304654;

            [Tooltip("남쪽 끝 위도. 북위보다 작아야 한다.")]
            public double 남위 = 36.95;

            [Tooltip("서쪽 끝 경도.")]
            public double 서경 = 126.68;

            [Tooltip("동쪽 끝 경도. 서경보다 커야 한다.")]
            public double 동경 = 127.15231855246095;
        }

        [Header("앱 기본")]
        [Tooltip("랜드마크 목록 JSON의 Resources 경로. 확장자는 쓰지 않는다.")]
        public string 랜드마크JSON경로 = "PixelRoad/landmarks";

        [Tooltip("거점이 분포하는 대략적인 범위. 지도 표시가 아니라 해금 판정용 공간 인덱스의 기준을 잡는다. 값이 잘못되면 앱이 시작하지 않는다.")]
        public 좌표범위 데이터범위 = new 좌표범위();

        [Tooltip("landmarks.json 의 visitRadius 가 비었거나 0 이하일 때 적용할 기본 방문 반경(m).")]
        public float 기본해금반경미터 = 50f;

        [Header("픽셀 필터")]
        [Tooltip("픽셀 필터의 최초 기본값. 사용자가 한 번이라도 토글하면 저장된 값이 우선한다.")]
        public bool 픽셀필터기본값 = false;

        [Tooltip("픽셀 모드에서 지도를 1/N 크기로 그린 뒤 점 샘플링으로 확대하는 배수. 클수록 거칠어지고 렌더 비용은 줄어든다. UI와 마커는 영향받지 않는다.")]
        public int 픽셀블록크기 = 4;

        [Header("마커 · 아이콘")]
        [Tooltip("지도 위 랜드마크 마커 한 변의 크기(UI px, 기준 해상도 1080x1920).")]
        public int 랜드마크마커크기픽셀 = 56;

        [Tooltip("지도 위 사용자 위치 마커 한 변의 크기(UI px).")]
        public int 사용자마커크기픽셀 = 44;

        [Tooltip("마커의 최소 터치 판정 크기(UI px). 그림보다 크게 잡아 작은 아이콘도 누르기 쉽게 한다. 마커 크기보다 작으면 무시된다.")]
        public int 마커최소터치크기픽셀 = 96;

        [Tooltip("랜드마크 아이콘 스프라이트를 찾을 Resources 폴더.")]
        public string 아이콘리소스폴더 = "PixelRoad/Icons";

        [Tooltip("category 이름으로 지도 마커 아이콘을 못 찾았을 때 마지막으로 시도할 아이콘 이름.")]
        public string 기본랜드마크아이콘이름 = "default";

        [Tooltip("도감에서 thumbnail 이미지가 없거나 아직 해금하지 않았을 때 쓸 대체 이미지 이름.")]
        public string 도감대체이미지이름 = "placeholder";

        [Tooltip("사용자 위치 마커에 쓸 아이콘 이름.")]
        public string 사용자아이콘이름 = "user";

        [Header("라이브 벡터 지도")]
        [Tooltip("네트워크 벡터 타일 지도 사용 여부. 끄면 지도가 표시되지 않고 안내 문구만 나온다.")]
        public bool 라이브벡터지도사용 = true;

        [Tooltip("릴리스 빌드에서도 지도를 허용할지. 끄면 에디터·개발 빌드에서만 지도가 동작한다. 켜려면 PIXELROAD_LIVE_VECTOR_MAP 스크립팅 심볼도 필요하다.")]
        public bool 릴리스빌드에서지도허용 = false;

        [Tooltip("타일 제공자 식별자. 캐시 폴더 구분과 로그에 쓰인다.")]
        public string 타일제공자ID = "osm-shortbread-development";

        [Tooltip("벡터 타일 스키마 이름. 레이어 해석 규칙을 고른다.")]
        public string 타일스키마 = "shortbread_v1";

        [Tooltip("벡터 타일 URL 템플릿. {z}/{x}/{y} 가 치환된다.")]
        public string 타일URL템플릿 = "https://vector.openstreetmap.org/shortbread_v1/{z}/{x}/{y}.mvt";

        [Tooltip("서버가 제공하는 최소 타일 줌.")]
        public int 타일최소줌 = 5;

        [Tooltip("서버가 제공하는 최대 타일 줌. 화면 줌이 더 커지면 이 줌의 타일을 확대해 쓴다.")]
        public int 타일최대줌 = 14;

        [Tooltip("앱 시작 시 화면 줌 레벨.")]
        public float 시작줌 = 15f;

        [Tooltip("축소 한계 줌 레벨.")]
        public float 최소줌 = 5f;

        [Tooltip("확대 한계 줌 레벨.")]
        public float 최대줌 = 18f;

        [Tooltip("동시에 진행할 타일 다운로드 개수. 크면 빠르지만 모바일 네트워크 부담이 커진다.")]
        public int 동시타일요청수 = 4;

        [Tooltip("메모리에 유지할 타일 개수. 넘으면 오래된 것부터 버린다.")]
        public int 메모리타일개수 = 48;

        [Tooltip("디스크 타일 캐시 상한(MB).")]
        public int 디스크캐시최대MB = 64;

        [Tooltip("디스크 타일 캐시 사용 여부. 끄면 매번 네트워크에서 받는다.")]
        public bool 디스크타일캐시사용 = true;

        [Tooltip("지도 하단 저작자 표시 문구. 타일 제공자 라이선스상 필수다.")]
        public string 저작자표시문구 = "© OpenStreetMap contributors";

        [Tooltip("저작자 표시를 눌렀을 때 열 URL. 비우면 버튼이 비활성화된다.")]
        public string 저작자표시링크 = "https://www.openstreetmap.org/copyright";

        [Header("위치 · 해금")]
        [Tooltip("GPS에 요청할 목표 정확도(m). 작을수록 정확하지만 배터리를 더 쓴다.")]
        public float 목표GPS정확도미터 = 15f;

        [Tooltip("이 거리(m) 이상 움직였을 때만 위치 갱신을 받는다.")]
        public float 위치갱신최소이동미터 = 3f;

        [Header("에디터 시뮬레이션 (빌드 영향 없음)")]
        [Tooltip("에디터 시뮬레이션 시작 위도. 앱 시작 시 지도 중심으로도 쓰인다.")]
        public double 에디터시작위도 = 37.4969698129663;

        [Tooltip("에디터 시뮬레이션 시작 경도.")]
        public double 에디터시작경도 = 127.039093501609;

        [Tooltip("에디터에서 방향키로 이동할 때의 속도(m/s).")]
        public float 에디터이동속도 = 250f;

        [Tooltip("에디터에서 가속 키를 눌렀을 때 이동 속도 배수.")]
        public float 에디터가속배수 = 4f;

        /// <summary>런타임이 쓰는 <see cref="MapConfig"/>로 변환한다.</summary>
        public MapConfig ToMapConfig()
        {
            MapConfig config = new MapConfig();
            config.landmarksJsonResourcePath = 랜드마크JSON경로;
            config.bounds = new MapBounds
            {
                northLat = 데이터범위.북위,
                southLat = 데이터범위.남위,
                westLon = 데이터범위.서경,
                eastLon = 데이터범위.동경,
            };
            config.defaultUnlockRadiusMeters = 기본해금반경미터;

            config.enablePixelFilter = 픽셀필터기본값;
            config.pixelBlockSize = 픽셀블록크기;

            config.spotMarkerPixelSize = 랜드마크마커크기픽셀;
            config.userMarkerPixelSize = 사용자마커크기픽셀;
            config.markerTapMinimumPixelSize = 마커최소터치크기픽셀;
            config.spotIconResourceFolder = 아이콘리소스폴더;
            config.defaultSpotIconName = 기본랜드마크아이콘이름;
            config.placeholderThumbnailName = 도감대체이미지이름;
            config.userIconName = 사용자아이콘이름;

            config.enableLiveVectorMap = 라이브벡터지도사용;
            config.allowLiveVectorMapInRelease = 릴리스빌드에서지도허용;
            config.vectorTileProviderId = 타일제공자ID;
            config.vectorTileSchema = 타일스키마;
            config.vectorTileUrlTemplate = 타일URL템플릿;
            config.vectorTileMinZoom = 타일최소줌;
            config.vectorTileMaxZoom = 타일최대줌;
            config.initialMapZoom = 시작줌;
            config.minimumMapZoom = 최소줌;
            config.maximumMapZoom = 최대줌;
            config.maxConcurrentTileRequests = 동시타일요청수;
            config.maxMemoryTileCount = 메모리타일개수;
            config.maxDiskCacheMegabytes = 디스크캐시최대MB;
            config.enableDiskTileCache = 디스크타일캐시사용;
            config.mapAttribution = 저작자표시문구;
            config.mapAttributionUrl = 저작자표시링크;

            config.desiredAccuracyMeters = 목표GPS정확도미터;
            config.locationUpdateDistanceMeters = 위치갱신최소이동미터;

            config.editorStartLatitude = 에디터시작위도;
            config.editorStartLongitude = 에디터시작경도;
            config.editorMoveSpeedMetersPerSecond = 에디터이동속도;
            config.editorFastMoveMultiplier = 에디터가속배수;
            return config;
        }
    }
}
