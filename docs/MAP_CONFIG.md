# map_config.json 설정 값 설명

파일 위치: `pixelRoadUnity/Assets/Resources/PixelRoad/map_config.json`
스키마 정의: `pixelRoadUnity/Assets/Scripts/PixelRoad/Data/MapConfig.cs`

JSON은 주석을 지원하지 않으므로 설명은 이 문서와 `MapConfig.cs`의 XML 주석에 둔다.
파싱은 `JsonUtility.FromJson<MapConfig>`로 하며, 다음 두 가지 성질을 전제로 한다.

- JSON에 **없는** 키는 `MapConfig.cs`에 적힌 기본값이 그대로 쓰인다.
- `MapConfig.cs`에 **없는** 키(예: 맨 위의 `_docs`)는 조용히 무시된다.

---

## 앱 기본

| 키 | 타입 | 기본값 | 설명 |
| --- | --- | --- | --- |
| `appTitle` | string | `Pixel Road` | 상단 바 제목. |
| `landmarksJsonResourcePath` | string | `PixelRoad/landmarks` | 랜드마크 JSON의 Resources 경로(확장자 제외). |
| `projection` | string | `WebMercator` | 좌표 투영 표기. 현재 구현은 WebMercator 고정. |
| `bounds` | object | — | 거점이 분포하는 대략적 위경도 범위. `northLat` / `southLat` / `westLon` / `eastLon`. 지도 표시가 아니라 해금 판정용 공간 인덱스의 기준 위도를 잡는 데 쓰인다. 유효하지 않으면 앱이 시작하지 않는다. |
| `defaultUnlockRadiusMeters` | float | `50` | JSON `visitRadius`가 비었거나 0 이하일 때 쓰는 기본 방문 반경(m). |

## 픽셀 필터

| 키 | 타입 | 기본값 | 설명 |
| --- | --- | --- | --- |
| `enablePixelFilter` | bool | `false` | 픽셀 필터 최초 기본값. 사용자가 상단 "픽셀" 버튼을 한 번이라도 누르면 PlayerPrefs 값이 우선한다. |
| `pixelBlockSize` | int | `4` | 픽셀 모드에서 지도 RenderTexture를 1/N로 렌더한 뒤 점 샘플링으로 확대하는 배수. UI와 마커는 영향받지 않는다. |

## 마커 · 아이콘

| 키 | 타입 | 기본값 | 설명 |
| --- | --- | --- | --- |
| `spotMarkerPixelSize` | int | `56` | 지도 위 거점 마커 한 변 크기(UI px, 캔버스 기준 해상도 1080x1920). |
| `userMarkerPixelSize` | int | `44` | 지도 위 사용자 마커 한 변 크기(UI px). |
| `markerTapMinimumPixelSize` | int | `96` | 거점 마커의 최소 터치 판정 크기(UI px). 그림보다 넓게 잡아 작은 아이콘도 누를 수 있게 한다. `spotMarkerPixelSize`보다 작으면 무시. 너무 키우면 인접 마커끼리 판정이 겹친다. |
| `spotIconResourceFolder` | string | `PixelRoad/Icons` | 거점 아이콘 스프라이트를 찾을 Resources 폴더. |
| `defaultSpotIconName` | string | `default` | JSON `thumbnail`·`category`로 못 찾았을 때 마지막으로 시도할 아이콘 이름. |
| `userIconName` | string | `user` | 사용자 위치 마커 아이콘 이름. |

아이콘 조회 규칙은 `Assets/Resources/PixelRoad/Icons/README.md` 참고.

## 라이브 벡터 지도

| 키 | 타입 | 기본값 | 설명 |
| --- | --- | --- | --- |
| `enableLiveVectorMap` | bool | `true` | 네트워크 벡터 타일 지도 사용 여부. 정적 지도 폴백을 제거했으므로 `false`면 지도가 아예 표시되지 않고 안내 문구만 나온다. |
| `allowLiveVectorMapInRelease` | bool | `false` | 릴리스 빌드에서도 라이브 지도를 허용할지. `false`면 에디터·개발 빌드에서만 지도가 동작한다(컴플라이언스 게이트). 릴리스에서 지도를 쓰려면 이 값을 `true`로 두고 `PIXELROAD_LIVE_VECTOR_MAP` 스크립팅 심볼도 정의해야 한다. |
| `vectorTileProviderId` | string | `osm-shortbread-development` | 타일 제공자 식별자. 디스크 캐시 구분과 로그에 쓰인다. |
| `vectorTileSchema` | string | `shortbread_v1` | 벡터 타일 스키마 이름. 레이어 해석 규칙을 고른다. |
| `vectorTileUrlTemplate` | string | OSM shortbread URL | 타일 URL 템플릿. `{z}` `{x}` `{y}`가 치환된다. |
| `vectorTileMinZoom` | int | `5` | 서버가 제공하는 최소 타일 줌. |
| `vectorTileMaxZoom` | int | `14` | 서버가 제공하는 최대 타일 줌. 화면 줌이 이보다 크면 이 줌의 타일을 확대해 쓴다(오버줌). |
| `initialMapZoom` | float | `15` | 앱 시작 시 화면 줌 레벨. |
| `minimumMapZoom` | float | `5` | 축소 한계. |
| `maximumMapZoom` | float | `18` | 확대 한계. |
| `maxConcurrentTileRequests` | int | `4` | 동시 타일 다운로드 개수. 크면 빠르지만 모바일 네트워크 부담이 커진다. |
| `maxMemoryTileCount` | int | `48` | 메모리에 유지할 타일 메시 개수. 초과분은 오래된 것부터 버린다. |
| `maxDiskCacheMegabytes` | int | `64` | 디스크 타일 캐시 상한(MB). |
| `enableDiskTileCache` | bool | `true` | 디스크 캐시 사용 여부. `false`면 매번 네트워크에서 받는다. |
| `mapAttribution` | string | `© OpenStreetMap contributors` | 지도 저작자 표시 문구. 타일 제공자 라이선스상 필수. |
| `mapAttributionUrl` | string | OSM copyright URL | 저작자 표시를 눌렀을 때 열 URL. 비우면 버튼이 비활성화된다. |

## 위치 · 해금

| 키 | 타입 | 기본값 | 설명 |
| --- | --- | --- | --- |
| `enableBackgroundUnlock` | bool | `false` | 백그라운드 해금 사용 여부. 현재 미구현 예약 값. |
| `maxActiveGeofences` | int | `100` | 동시 감시 지오펜스 최대 개수. 현재 미구현 예약 값. |
| `desiredAccuracyMeters` | float | `15` | GPS에 요청할 목표 정확도(m). 작을수록 정확하지만 배터리를 더 쓴다. |
| `locationUpdateDistanceMeters` | float | `3` | 이 거리(m) 이상 움직였을 때만 위치 갱신을 받는다. |

## 에디터 시뮬레이션 (빌드 동작에는 영향 없음)

| 키 | 타입 | 기본값 | 설명 |
| --- | --- | --- | --- |
| `editorStartLatitude` | double | `37.579617` | 시뮬레이션 시작 위도. 앱 시작 시 지도 중심으로도 쓰인다. |
| `editorStartLongitude` | double | `126.977041` | 시뮬레이션 시작 경도. |
| `editorMoveSpeedMetersPerSecond` | float | `250` | 에디터에서 방향키 이동 속도(m/s). |
| `editorFastMoveMultiplier` | float | `4` | 가속 키를 눌렀을 때 속도 배수. |
| `editorFollowSimulatedLocation` | bool | `true` | 시뮬레이션 위치를 지도 중심이 계속 따라갈지 여부. |
