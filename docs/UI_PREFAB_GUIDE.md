# UI 프리팹 · 씬 작업 가이드

고정 UI는 런타임 코드 생성이 아니라 아래 프리팹으로 관리한다. 레이아웃 수치는 Figma 와이어프레임
`main_지도`(1080 × 1920)를 기준으로 하며, Canvas Scaler 기준 해상도가 같아 Figma 픽셀값을 1:1로 옮긴다.

화면의 `ZoomIn`/`ZoomOut` 버튼은 제거했다. 지도 확대·축소는 모바일 핀치 또는 에디터/PC의 마우스 휠 입력으로만 동작한다.

| 프리팹 | 용도 |
|---|---|
| `Assets/Resources/PixelRoad/UI/PixelRoadUIRoot.prefab` | 지도(화면 전체), 그 위에 반투명하게 얹히는 랜드마크 배너·거리 표시·하단 GNB·OSM 출처, 그리고 도감 창 |
| `Assets/Resources/PixelRoad/UI/LoadingUIRoot.prefab` | 로딩 씬 오버레이(자체 Canvas, 씬 전환을 넘어 유지) |
| `Assets/Resources/PixelRoad/UI/LandmarkMarker.prefab` | 지도 위 동적 랜드마크 마커(아이콘). 이름은 마커에 붙이지 않고 선택 시 상단 배너에서만 보여 준다 |
| `Assets/Resources/PixelRoad/UI/LandmarkCodexCard.prefab` | 도감의 동적 랜드마크 카드 |
| `Assets/Resources/PixelRoad/UI/CodexFilterChip.prefab` | 도감 상단 카테고리 필터 칩 |

UI 작업자는 각 프리팹을 Prefab Mode로 열어 앵커, 크기, 색상, 폰트, 이미지와 자식 계층을 수정하면 된다. 런타임은 `PixelRoadUiBindings`, `LoadingUiBindings`, `GnbView`, `LandmarkMarkerView`, `LandmarkCardView`에 직렬화된 참조를 사용하므로 계층 이름을 바꿔도 참조가 유지되는 한 동작한다.

**프리팹이 유일한 원본이다.** 예전에는 `PixelRoadUiPrefabBuilder`(`Tools > Pixel Road > Rebuild UI Prefabs`)가 프리팹 5종을 코드로 다시 구웠지만, 손으로 고친 내용을 덮어써 프리팹과 코드가 어긋나는 사고가 반복돼 삭제했다. 이제 레이아웃 수정은 에디터(또는 Unity MCP)에서 프리팹 에셋을 직접 고치고 커밋한다. 구조 계약은 `UiPrefabTests`가 지킨다.

## 런타임이 만들지 않는 것

고정 UI는 **전부 씬에 배치된 프리팹 인스턴스**다. 런타임 코드는 UI GameObject를 만들지도, 프리팹을 찾아 `Instantiate` 하지도 않는다.

| 예전 (런타임 생성) | 지금 |
|---|---|
| `GetOrCreateCanvas()`가 Canvas·CanvasScaler·GraphicRaycaster 생성 | 프리팹 루트의 Canvas·CanvasScaler(1080×1920, match 0.5)·GraphicRaycaster |
| `EnsureUiInput()`이 EventSystem·InputModule 생성 | 프리팹의 `EventSystem` 자식(+ `InputSystemUIInputModule`) |
| `Texture2D`로 마름모·원 마커 스프라이트를 매 실행 생성 | `Icons/default.png`, `Icons/user.png`를 프리팹 `Image`에 미리 지정 |
| `Resources.Load` + `Instantiate` 로 UI 루트 생성 | `MapScene`에 배치된 `PixelRoadUIRoot` 인스턴스 |
| `FindFirstObjectByType<PixelRoadUiBindings>()` | `PixelRoadApp.uiBindings` 직렬화 참조 |
| `RuntimeInitializeOnLoadMethod`가 `PixelRoadApp` GameObject 생성 | `MapScene`에 배치된 `PixelRoadApp` 오브젝트 |
| `LoadingSceneController`가 오버레이 프리팹을 `Instantiate` | `Loading` 씬의 `LoadingBoot` 자식으로 배치된 `LoadingUIRoot` 인스턴스 |

- 참조가 비어 있으면 **대신 만들어 주지 않고** 오류 로그를 남긴다. `PixelRoadApp.uiBindings`, `LoadingSceneController.ui`를 반드시 연결해야 한다. `Tools > Pixel Road > Setup Map Scene`과 `Rebuild Loading Scene`이 자동으로 배치·연결해 준다.
- 씬에 EventSystem이 따로 있으면 런타임이 **프리팹 쪽 EventSystem을 제거**한다. Unity는 활성 EventSystem이 둘이면 경고하고 한쪽 입력만 처리하기 때문이다.
- 화면 DPI에 맞춘 `EventSystem.pixelDragThreshold` 보정만 코드에 남아 있다. 오브젝트 생성이 아니라 값 조정이다.
- 마커 스프라이트는 `landmarks.json`의 `thumbnail` → `category` → `default` 순으로 찾고, **하나도 못 찾으면 프리팹에 지정된 스프라이트를 그대로 쓴다.** 그래서 프리팹의 `LandmarkMarker`, `LandmarkCodexCard`, `UserMarker`의 `Image.sprite`를 비우면 안 된다(비우면 흰 사각형이 된다). `UiPrefabTests`가 이를 검사한다.

### 아직 런타임에 만드는 것

- **랜드마크 마커·도감 카드**: `landmarks.json`의 개수만큼 `LandmarkMarker` / `LandmarkCodexCard` 프리팹을 `Instantiate` 한다. 데이터 개수가 정해져 있지 않아 씬에 미리 둘 수 없다.
- **`LiveVectorMapRenderer`와 벡터 타일**: `MapViewport`에 `AddComponent` 하고, 보이는 타일마다 GameObject·Mesh·Material·RenderTexture를 만든다. 지도 타일 자체가 동적이라 프리팹화할 수 없고, 이 클래스는 `#if UNITY_EDITOR || DEVELOPMENT_BUILD || PIXELROAD_LIVE_VECTOR_MAP`로 감싸져 릴리스 빌드에는 타입이 아예 없다. 프리팹에 넣으면 릴리스에서 Missing Script가 된다.

기본 한글 폰트는 `Assets/Resources/PixelRoad/Fonts/Galmuri11 SDF.asset`이다. 동적 TMP 폰트이므로 한글 데이터가 추가되어도 런타임에 글리프를 생성하며, 원본 `Galmuri11.ttf`와 함께 유지해야 한다.
(기획 기준 폰트는 Pretendard이지만 아직 프로젝트에 없다. 도입 시 TMP 폰트 에셋을 새로 굽고 프리팹의 `TMP_Text.font`를 교체한다.)

## 씬 구성

| 씬 | 역할 |
|---|---|
| `Assets/Scenes/Loading.unity` | 최초 진입. `Main Camera` + `LoadingBoot`(`LoadingSceneController`), 그 자식으로 `LoadingUIRoot` 프리팹 인스턴스 |
| `Assets/Scenes/MapScene.unity` | 지도 화면. `Main Camera` + `PixelRoadUIRoot` 프리팹 인스턴스 + `PixelRoadApp` |

씬 계층은 다음과 같다. 두 씬 모두 실행 중에 루트 오브젝트가 늘어나지 않는다.

```text
Loading.unity                 MapScene.unity
├─ Main Camera                ├─ Main Camera
└─ LoadingBoot                ├─ PixelRoadUIRoot   (Canvas · EventSystem · 전체 UI)
   └─ LoadingUIRoot           └─ PixelRoadApp      (uiBindings → PixelRoadUIRoot)
      (자체 Canvas)
```

`LoadingBoot`는 `DontDestroyOnLoad`로 지도 씬까지 살아남으며, 자식인 `LoadingUIRoot`도 함께 넘어간다. 그래서 오버레이는 반드시 `LoadingBoot`의 **자식**이어야 한다.

- Build Settings 순서는 **Loading → MapScene**이며 `Tools > Pixel Road > Register Build Scenes`로 맞춘다.
- 로딩 화면은 씬 로드가 끝난 뒤에도 **지도 첫 타일이 그려질 때까지** 유지된다. 지도 오버레이는 `DontDestroyOnLoad`로 살아남고, 준비 완료는 `AppReadySignal`로만 전달한다.
- 지도를 쓸 수 없는 구성이거나 20초 안에 타일이 오지 않으면 로딩 화면을 강제로 닫는다.
- `PixelRoadApp`은 `MapScene`에 직접 배치돼 있다. 씬 이름을 바꾸면 `LoadingSceneController.mapSceneName`을 함께 바꿔야 한다.

## 하단 GNB

`GnbView`가 탭 4개(지도 / 도감 / AI탐험리포트 / AR)를 관리한다. 상태는 셋이다.

| 상태 | 색 | 조건 |
|---|---|---|
| select | `#4E6AFF` | 현재 열려 있는 탭 |
| active | `#FFFFFF` | 쓸 수 있지만 현재 탭은 아님 |
| deserbled | `#8E8E8E` | 조건을 만족하지 못해 못 쓰는 탭 |

- **AI탐험리포트**: 해금 랜드마크가 1개 이상이면 활성. 마지막 리포트 요청 시점(`ReportStateStore.LastReportedCount`, PlayerPrefs)과 현재 해금 개수가 다르면 느낌표 뱃지를 켠다. 리포트 요청 기능이 붙으면 요청 성공 후 `ReportStateStore.SetLastReportedCount(...)`를 호출하면 뱃지가 꺼진다.
- **AR**: 현재는 항상 비활성이다. AR 반경 판정이 붙으면 `GnbView.SetInteractable(GnbTab.Ar, ...)`로 켜고, 탭 동작은 `PixelRoadApp.HandleGnbTabSelected`의 `GnbTab.Ar` 분기(`view.OnClickARBtn()`)에 연결한다.

## 현재 위치 추적

- 지도는 기본적으로 **현재 위치를 중심에 두고 따라간다**. 위치 좌표가 실제로 바뀌었을 때만 `SetCenter`를 불러서, 같은 좌표로 매 프레임 타일·마커를 다시 계산하지 않는다.
- 지도를 드래그하면 추적이 풀리고, 우하단 `RecenterButton`이 나타난다. 버튼을 누르면 다시 추적하며 버튼은 숨는다.
- 핀치·휠 줌은 추적을 풀지 않는다. 확대해도 현재 위치를 계속 중앙에 둔다.
- 추적 상태는 `PixelRoadRuntimeView`가 들고 있다. `PixelRoadApp`은 위치만 넘긴다.

## 도감

와이어프레임 `도감_수집률 버전` / `도감_카드클릭` 기준이다. `CodexView`가 전체를 관리한다.

- **전체 화면**이지만 하단 GNB는 덮지 않는다. `CodexPanel`의 아래 오프셋이 `GnbHeight`(227)다.
- **상단 필터**: `landmarks.json`의 `category` 값으로 칩을 만든다. 데이터에 있는 값만 칩이 생기고, `전체`가 맨 앞이다. 표시 순서는 `CodexView.CategoryOrder`(역사 → 문화 → 교통 → 공공 → 테스트)를 따르며, 여기 없는 값은 뒤에 붙는다.
- **카드**: 좌상단 태그는 `collectionTitle`, 아래로 이미지 · 이름 · 한 줄 설명. 잠긴 항목은 이름·설명이 `???`이고 이미지 위에 노란 자물쇠가 뜬다.
- **하단 수집률**: 칸 20개를 프리팹에 미리 만들어 두고 런타임은 색만 바꾼다. 채워진 칸 수가 바뀔 때만 다시 칠한다.
- **카드 상세**: 카드를 누르면 `CardDetail`이 도감 위에 겹쳐 뜬다. 어두운 배경 아무 곳이나 누르면 닫힌다. `뒷면 보기 >`는 `shortDescription` ↔ `history`를 뒤집고, `360°` 버튼은 `view360Image`가 있는 랜드마크에서만 보인다. 잠긴 카드는 상세가 열리지 않는다.

## 랜드마크 배너와 선택

- 상단 배너(`LandmarkBanner`)는 **선택된 랜드마크가 있을 때만** 보인다.
- 지도 빈 곳을 탭하면 `PixelRoadMapInput.Tapped` → `PixelRoadRuntimeView.DeselectSpot()`으로 선택이 풀린다. 팬 드래그와 구분하기 위해 이동량이 탭 허용 반경 안일 때만 발생한다.
- `카드 보기` 버튼은 해금된 랜드마크에서만 활성이며, 누르면 도감 카드와 같은 상세 팝업(`CardDetail`)이 지도 위에 뜬다.

## 화면 겹침 규칙

지도(`MapViewport`)가 화면 전체를 채우고, 그 위에 UI가 **반투명**으로 얹힌다. 헤더 바는 없앴다.

| 오브젝트 | 알파 | 비고 |
|---|---|---|
| `LandmarkBanner` (테두리 / `Surface`) | 235 | 화면 상단에서 24, 60 px 떨어뜨린다 |
| `RecenterButton` | 220 | 우하단, GNB 위 |
| `Gnb` 배경 | 210 | 지도가 비쳐 보인다 |
| `MapAttribution` | 150 | **오른쪽 맨 아래**(-12, 12). GNB 다음 형제라 GNB 위에 그려진다 |

`CodexPanel` · `CardDetail` · `QuitDialog`는 내용을 읽는 화면이라 불투명하게 둔다.
형제 순서(= 그리는 순서)는 `MapViewport → LandmarkBanner → RecenterButton → Gnb → MapAttribution → CodexPanel → CardDetail → QuitDialog`다.

## 주의 사항

- `PixelRoadUiBindings` / `GnbView` / `LoadingUiBindings`의 Inspector 참조를 비우지 않는다. 비면 런타임에 `ValidateReferences()`가 예외를 던진다. `PixelRoadUiBindings`에는 `canvas` / `canvasScaler` / `graphicRaycaster` / `eventSystem` 참조도 포함된다.
- `PixelRoadUIRoot` 루트에서 `Canvas`, `CanvasScaler`, `GraphicRaycaster`를 제거하지 않는다. 런타임에 대체 Canvas를 만들지 않으므로 화면이 통째로 사라진다.
- `LandmarkMarker`에는 `Button`을 추가하지 않는다. 모바일 드래그와 탭을 구분하기 위해 `MapMarkerTapTarget`을 사용한다.
- `MapViewport`의 `PixelRoadMapInput`, `RectMask2D`, 배경 `Image`를 유지한다. 배경 `Image`의 Raycast Target을 끄면 빈 곳 탭으로 선택 해제가 되지 않는다.
- `LiveVectorMap`은 `RawImage`, `MapMarkerOverlay`는 전체 Stretch 상태를 유지한다.
- `DistanceIndicator` 아래 `DistanceLine`의 피벗은 `(0, 0.5)`여야 한다. 현재 위치에서 랜드마크 방향으로 늘리는 데 쓴다. `Image`에는 `solid` 스프라이트가 물려 있어야 실선으로 그려진다.
- `LandmarkCodexCard` 루트의 `Button`과 `LandmarkCardView` 참조를 유지한다.
- 동적 객체 이름 `Spot_{id}`, `Codex_{id}`는 테스트와 디버깅에 사용한다.

## 아이콘

`Assets/Resources/PixelRoad/Icons/` 의 PNG는 Figma에서 내보낸 것이며, `PixelRoadIconImporter`가 자동으로 Sprite로 임포트한다.

| 파일 | 용도 |
|---|---|
| `gnb_map / gnb_codex / gnb_report / gnb_ar` | GNB 탭 아이콘 |
| `lock` / `unlock` | 잠금·해금 표시. 배너는 상태에 따라 런타임이 스프라이트를 바꾸고(틴트 없음, 흰색 고정), 도감 카드는 잠김일 때만 `lock`을 띄운다 |
| `arrow_right` | `카드 보기` 버튼 화살표 |
| `station` | 역 랜드마크 마커 |
| `user` | 현재 위치 마커(64×64 파란 원) |
| `recenter` | 우하단 현재 위치 재추적 버튼 |
| `default` | 카테고리 아이콘을 못 찾은 랜드마크의 기본 마커 |
| `placeholder` | 도감에서 썸네일이 없거나 잠긴 랜드마크에 쓰는 대체 이미지 |
| `solid` | 단색 흰색 8×8. 로딩 진행률 바처럼 `Image.Type.Filled`를 쓰는 곳에 물린다. 스프라이트가 없으면 Unity가 `fillAmount`를 무시하고 사각형 전체를 그린다 |

지도 마커 아이콘은 `landmarks.json`의 `category` → 영문 별칭(`역사`→`history` 등) → `map_config.defaultSpotIconName` 순으로 찾는다. 셋 다 없으면 프리팹에 지정된 스프라이트가 그대로 남는다.

도감 카드·상세의 이미지는 마커와 별개로 `landmarks.json`의 `thumbnail`만 따른다. 썸네일 파일이 없거나 아직 해금하지 않은 랜드마크는 `map_config.placeholderThumbnailName`(기본 `placeholder`) 이미지로 대체된다.

`default.png`와 `solid.png`는 예전 프리팹 빌더가 한 번 구워 둔 에셋이다. 지금은 생성 코드가 없으니 지우지 말고, 교체할 때는 같은 이름으로 덮어쓴다.

## 메뉴

| 메뉴 | 동작 |
|---|---|
| `Tools > Pixel Road > Setup Map Scene` | `MapScene.unity`에 `PixelRoadUIRoot` 인스턴스와 `PixelRoadApp`을 배치하고 참조를 연결한다. 중복 Canvas·EventSystem은 제거하고 카메라 등 나머지는 그대로 둔다 |
| `Tools > Pixel Road > Rebuild Loading Scene` | `Assets/Scenes/Loading.unity`를 **덮어써서** 다시 만들고 Build Settings에 등록한다. 현재 열린 씬은 건드리지 않는다 |
| `Tools > Pixel Road > Register Build Scenes` | Build Settings를 Loading → MapScene 순서로 맞춘다 |

프리팹을 다시 굽는 메뉴는 없다. UI 레이아웃은 프리팹 에셋을 직접 고친다(위 [프리팹](#ui-프리팹--씬-작업-가이드) 항목 참고).
