# 지도 설정 값 설명

설정은 **인스펙터에서 편집하는 ScriptableObject**가 1순위, 예전 JSON이 2순위다.

| 순위 | 파일 | 비고 |
| --- | --- | --- |
| 1 | `pixelRoadUnity/Assets/Resources/PixelRoad/MapConfig.asset` | 필드 이름과 툴팁이 한국어. 스키마는 `Data/MapConfigAsset.cs` |
| 2 | `pixelRoadUnity/Assets/Resources/PixelRoad/map_config.json` | 에셋이 없을 때만 읽는다. 스키마는 `Data/MapConfig.cs` |

`PixelRoadApp.LoadConfig()`가 위 순서로 읽고, `MapConfigAsset.ToMapConfig()`로 런타임 `MapConfig`를 만든다.
값을 바꿀 때는 **에셋 쪽을 고친다.** JSON만 고치면 에셋이 있는 한 반영되지 않는다.
새 항목을 추가할 때는 `MapConfig.cs`(런타임 스키마)와 `MapConfigAsset.cs`(에디터 편집용)를 함께 고친다.
아래 표의 키 이름은 JSON 기준이며, 에셋의 한국어 필드가 1:1로 대응한다.

JSON은 주석을 지원하지 않으므로 설명은 이 문서와 `MapConfig.cs`의 XML 주석에 둔다.
파싱은 `JsonUtility.FromJson<MapConfig>`로 하며, 다음 두 가지 성질을 전제로 한다.

- JSON에 **없는** 키는 `MapConfig.cs`에 적힌 기본값이 그대로 쓰인다.
- `MapConfig.cs`에 **없는** 키(예: 맨 위의 `_docs`)는 조용히 무시된다.

---

## 앱 기본

| 키 | 타입 | 기본값 | 설명 |
| --- | --- | --- | --- |
| `landmarksJsonResourcePath` | string | `PixelRoad/landmarks` | 랜드마크 JSON의 Resources 경로(확장자 제외). |
| `bounds` | object | — | 거점이 분포하는 대략적 위경도 범위. `northLat` / `southLat` / `westLon` / `eastLon`. 지도 표시가 아니라 해금 판정용 공간 인덱스의 기준 위도를 잡는 데 쓰인다. 유효하지 않으면 앱이 시작하지 않는다. |
| `defaultUnlockRadiusMeters` | float | `50` | JSON `visitRadius`가 비었거나 0 이하일 때 쓰는 기본 방문 반경(m). |

## 마커 · 아이콘

| 키 | 타입 | 기본값 | 설명 |
| --- | --- | --- | --- |
| `spotMarkerPixelSize` | int | `56` | 지도 위 거점 마커 한 변 크기(UI px, 캔버스 기준 해상도 1080x1920). |
| `userMarkerPixelSize` | int | `44` | 지도 위 사용자 마커 한 변 크기(UI px). |
| `markerTapMinimumPixelSize` | int | `96` | 거점 마커의 최소 터치 판정 크기(UI px). 그림보다 넓게 잡아 작은 아이콘도 누를 수 있게 한다. `spotMarkerPixelSize`보다 작으면 무시. 너무 키우면 인접 마커끼리 판정이 겹친다. |
| `spotIconResourceFolder` | string | `PixelRoad/Icons` | 거점 아이콘 스프라이트를 찾을 Resources 폴더. |
| `defaultSpotIconName` | string | `default` | JSON `category`로 지도 마커 아이콘을 못 찾았을 때 마지막으로 시도할 아이콘 이름. |
| `placeholderThumbnailName` | string | `placeholder` | 도감에서 `thumbnail` 이미지가 없거나 아직 해금하지 않은 랜드마크에 쓸 대체 이미지 이름. 지도 마커에는 쓰이지 않는다. |
| `userIconName` | string | `user` | 사용자 위치 마커 아이콘 이름. |

아이콘 조회 규칙은 `Assets/Resources/PixelRoad/Icons/README.md` 참고.

## 라이브 벡터 지도

| 키 | 타입 | 기본값 | 설명 |
| --- | --- | --- | --- |
라이브 벡터 지도는 **끌 수 없다.** 켜고 끄는 스위치(`enableLiveVectorMap`)와 릴리스 전용 게이트
(`allowLiveVectorMapInRelease`)를 모두 제거했고, 지도는 모든 빌드에서 항상 동작한다.
오프라인 심사용 빌드(`PIXELROAD_OFFLINE_REVIEW`)도 2026-08-22에 없앴으므로 안드로이드 빌드는
`Pixel Road > Build Android APK` 하나뿐이고 `INTERNET` 권한은 항상 들어간다.
지도를 못 쓰게 만들려면 타일 제공자 설정이 검증에 걸리게 하는 방법뿐이다(예: `vectorTileUrlTemplate` 를 비움).
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
| `desiredAccuracyMeters` | float | `15` | GPS에 요청할 목표 정확도(m). 작을수록 정확하지만 배터리를 더 쓴다. |
| `locationUpdateDistanceMeters` | float | `3` | 이 거리(m) 이상 움직였을 때만 위치 갱신을 받는다. |

## AI 탐험 리포트

| 키 | 타입 | 기본값 | 설명 |
| --- | --- | --- | --- |
| `reportApiUrl` | string | `""` | 분석을 요청할 주소(POST). **비워 두면 서버를 부르지 않고 임시(목) 응답으로 동작한다.** 예: `https://example.com/api/report` |
| `reportRequestTimeoutSeconds` | int | `20` | 응답을 기다리는 최대 시간(초). |
| `reportMockDelaySeconds` | float | `1.5` | 임시 응답으로 동작할 때 흉내 낼 지연(초). 분석중 화면을 눈으로 확인하려고 둔다. |
| `reportToastAutoHideSeconds` | float | `3` | 갱신 완료 토스트가 저절로 사라지기까지의 시간(초). |

예전에는 `Resources/PixelRoad/report_config.json` 을 따로 뒀지만, 설정 파일이 둘로 갈라져 헷갈리므로 여기로 합쳤다.
**주소를 바꿀 때는 `MapConfig.asset`(인스펙터)의 `리포트API주소`를 고친다.** 위 표의 JSON 키는 에셋이 없을 때만 쓰인다.

요청·응답 형식은 다음과 같다(`Data/ReportDto.cs`).

```
POST {reportApiUrl}
Req  { "visitedLandmarks": [ { "landmarkId": 1, "visitCount": 2 } ] }
Res  { "analysis": "...", "recommendation": { "landmarkId": 24, "name": "...", "reason": "..." } }
```

## 에디터 시뮬레이션 (빌드 동작에는 영향 없음)

| 키 | 타입 | 기본값 | 설명 |
| --- | --- | --- | --- |
| `editorStartLatitude` | double | `37.579617` | 시뮬레이션 시작 위도. 앱 시작 시 지도 중심으로도 쓰인다. |
| `editorStartLongitude` | double | `126.977041` | 시뮬레이션 시작 경도. |
| `editorMoveSpeedMetersPerSecond` | float | `250` | 에디터에서 방향키 이동 속도(m/s). |
| `editorFastMoveMultiplier` | float | `4` | 가속 키를 눌렀을 때 속도 배수. |
