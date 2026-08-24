# UI 프리팹 · 씬 작업 가이드

고정 UI는 런타임 코드 생성이 아니라 아래 프리팹으로 관리한다. 레이아웃 수치는 Figma 와이어프레임
`main_지도`(1080 × 1920)를 기준으로 하며, Canvas Scaler 기준 해상도가 같아 Figma 픽셀값을 1:1로 옮긴다.

화면의 `ZoomIn`/`ZoomOut` 버튼은 제거했다. 지도 확대·축소는 모바일 핀치 또는 에디터/PC의 마우스 휠 입력으로만 동작한다.
픽셀 필터도 제거했다. 토글 버튼과 함께 `map_config`의 `enablePixelFilter` / `pixelBlockSize`, 렌더러의 픽셀 모드 경로까지 전부 삭제했으므로, 다시 쓰려면 새로 구현해야 한다.

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
- **`LiveVectorMapRenderer`와 벡터 타일**: `MapViewport`에 `AddComponent` 하고, 보이는 타일마다 GameObject·Mesh·Material·RenderTexture를 만든다. 지도 타일 자체가 동적이라 프리팹화할 수 없으니 프리팹에 넣지 말고 런타임에 붙인다.

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

- **AI탐험리포트**: **항상 활성**이다. 해금이 0건이면 리포트 화면이 `탐험 기록이 없습니다` 상태로 열린다. 뱃지는 `ReportStateStore.HasUnreadUpdate`(분석이 갱신됐는데 아직 리포트 화면을 열지 않음)일 때만 켜지고, 탭을 여는 순간 `MarkUpdateSeen()`으로 꺼진다.
- **AR**: `PixelRoadApp.UpdateArAvailability()`가 위치 갱신마다(10 m 이상 움직였거나 선택이 바뀌었을 때만) 다시 판정한다. AR 허용 반경은 랜드마크마다 `ARConfig.arDisplayRadiusMeters + 그 랜드마크의 visitRadius`다.
  - 허용 반경 안에 랜드마크가 하나도 없으면 비활성.
  - 랜드마크를 선택한 상태라면 **그 랜드마크**가 허용 반경 밖일 때 비활성. 선택을 들고 AR로 넘어가면 대상이 없다고 판단해 AR이 곧바로 종료되기 때문이다.
  - 첫 위치를 받기 전에는 판단할 수 없으므로 잠가 둔다.
  - 잠긴 AR 탭을 누르면 지도 위에 `랜드마크 근처에서 사용할 수 있습니다`가 3초 동안 뜬다(`GnbView.TabBlocked` → `PixelRoadApp.HandleGnbTabBlocked`).

> **탭의 사용 가능 여부와 `Button.interactable`은 별개다.** 못 쓰는 탭도 클릭은 받아야 왜 못 쓰는지 알려 줄 수 있어서,
> `GnbView`는 `Button.interactable`을 항상 켜 두고 사용 가능 여부를 `tabEnabled`로만 관리한다(색으로 드러난다).
> 프리팹에서 GNB 버튼의 `Interactable` 체크를 끄면 클릭이 삼켜져 안내가 뜨지 않는다.

## 현재 위치 추적

- 지도는 기본적으로 **현재 위치를 중심에 두고 따라간다**. 위치 좌표가 실제로 바뀌었을 때만 `SetCenter`를 불러서, 같은 좌표로 매 프레임 타일·마커를 다시 계산하지 않는다.
- 지도를 드래그하면 추적이 풀리고, 우하단 `RecenterButton`이 나타난다. 버튼을 누르면 다시 추적하며 버튼은 숨는다.
- 핀치·휠 줌은 추적을 풀지 않는다. 확대해도 현재 위치를 계속 중앙에 둔다.
- 추적 상태는 `PixelRoadRuntimeView`가 들고 있다. `PixelRoadApp`은 위치만 넘긴다.

## 도감

와이어프레임 `도감_수집률 버전` / `도감_카드클릭` 기준이다. `CodexView`가 전체를 관리한다.

- **전체 화면**이지만 하단 GNB는 덮지 않는다. `CodexPanel`의 아래 오프셋이 `GnbHeight`(227)다.
- **상단 필터**: `landmarks.json`의 `category` 값으로 칩을 만든다. 데이터에 있는 값만 칩이 생기고, `전체`가 맨 앞이다. 표시 순서는 `SpotCategory.DisplayOrder`(역사 → 문화 → 교통 → 공공 → 테스트)를 따르며, 여기 없는 값은 뒤에 붙는다. 데이터에 남아 있는 영문 표기(`station`, `test` 등)는 `SpotCategory.Normalize()`가 같은 칩으로 합쳐 준다(마커 아이콘 키는 원본 `category`로 정해지므로 영향이 없다).
- **카드**: 좌상단 태그는 `collectionTitle`, 아래로 이미지 · 이름 · 한 줄 설명. 잠긴 항목은 이름·설명이 `???`이고 이미지 위에 노란 자물쇠가 뜬다.
- **하단 수집률**: 칸 20개를 프리팹에 미리 만들어 두고 런타임은 색만 바꾼다. 채워진 칸 수가 바뀔 때만 다시 칠한다.
- **카드 상세**: 카드를 누르면 `CardDetail`이 도감 위에 겹쳐 뜬다. 어두운 배경 아무 곳이나 누르면 닫힌다. `뒷면 보기 >`는 `shortDescription` ↔ `history`를 뒤집고, `360°` 버튼은 `view360Image`가 있는 랜드마크에서만 보인다.
- **잠긴 카드도 상세가 열린다.** 어디로 가야 해금되는지 알려면 앞면의 `→ 지도에서 보기`를 눌릴 수 있어야 하기 때문이다. 대신 내용은 그리드 카드와 같은 수준으로 가린다 — 이름만 남기고 설명은 `???`, 이미지는 대체 이미지에 잠금 틴트, `뒷면 보기 ›`와 `360°` 버튼은 감춘다. 가리는 일은 `CodexDetailView.Show(definition, sprite, unlocked)`가 맡는다.
  - 지도 배너의 `카드 보기`는 예전대로 해금된 랜드마크에서만 눌린다. 배너는 이미 지도 위라 `지도에서 보기`가 할 일이 없다.
- **카드 앞면의 `지도에서 보기`**: `CodexDetailView.mapButton` → `CodexView.DetailMapRequested` → `PixelRoadApp.FocusOnSpot(landmarkId)` → `PixelRoadRuntimeView.FocusOnSpot()`. 카드·도감·리포트를 모두 접고 지도 탭으로 돌아가 그 랜드마크를 중심에 두고 선택 상태로 만든다. 위치 추적은 끊기므로 우하단 재추적 버튼이 나타난다.

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

`CodexPanel` · `ReportPanel` · `CardDetail` · `QuitDialog` · `UnlockDialog`는 내용을 읽는 화면이라 불투명하게 둔다.
형제 순서(= 그리는 순서)는 `MapViewport → LandmarkBanner → RecenterButton → Gnb → MapAttribution → CodexPanel →
ReportPanel → CardDetail → QuitDialog → UnlockDialog`다(`EventSystem`이 index 0).

`CardDetail` · `QuitDialog` · `UnlockDialog` · `ReportPanel`은 열릴 때 `SetAsLastSibling()`을 부르므로 형제 순서는
초기값일 뿐이고 실제로는 마지막에 열린 창이 맨 위에 온다. `CodexPanel`과 `ReportPanel`은 서로 배타적으로 열린다.

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

## AI 리포트 · 해금 알림 화면

`PixelRoadUIRoot.prefab`에 만들어져 있고 참조도 모두 연결돼 있다. 기존 `CodexPanel` · `QuitDialog` · `CardDetail`과
같은 규칙을 따른다 — **컴포넌트는 껐다 켜는 오브젝트 자신에 붙고 `root`도 자기 자신을 가리키며, 프리팹에는 비활성으로 저장한다.**

### `UnlockDialog` — 해금 알림 (`UnlockDialogView`)

`QuitDialog`를 복제해 만들었으므로 색·폰트·스프라이트가 종료 확인 창과 같다. 캔버스 **맨 마지막 형제**라
지도·도감·리포트·카드 상세 어디에 있든 그 위를 덮는다. 열릴 때 `SetAsLastSibling()`도 한 번 더 부른다.

```text
UnlockDialog      [inactive]  ← UnlockDialogView + root(자기 자신)
├─ Dimmer                     ← dimmer   (뒤 화면 터치 차단)
└─ Panel                      (840 × 460, 화면 중앙)
   └─ Surface
      ├─ Title                ← titleText          ("랜드마크 발견!" 고정)
      ├─ Name                 ← landmarkNameText   (런타임이 "[전철역]" 형태로 채움)
      └─ ConfirmButton        ← confirmButton      (아래쪽 가로 꽉 채움)
         └─ Label             ("확인")
```

연속 해금은 큐에 쌓여 `확인`을 누를 때마다 하나씩 나온다. `확인`을 눌러도 상단 배너의 선택 상태는 그대로 남는다.

### `ReportPanel` — AI 탐험 리포트 (`ReportView`)

`CodexPanel`과 같은 자리·같은 크기(하단 `GnbHeight` 227px를 비움)다. 둘은 서로 배타적으로 열린다.
카드 네 장은 `Content`의 `VerticalLayoutGroup` + `ContentSizeFitter`로 쌓이고, 상태에 따라 켜고 끄기만 한다.

```text
ReportPanel       [inactive]      ← ReportView + root(자기 자신), 배경 #292319
├─ Title                          ("AI 탐험 리포트")
├─ Scroll                         ← scroll (ScrollRect, 세로만)
│  └─ Viewport                    (RectMask2D)
│     └─ Content                  (VerticalLayoutGroup + ContentSizeFitter)
│        ├─ EmptyCard             ← emptyCard          (탐험 기록 없음)
│        │  ├─ Message            ("탐험 기록이 없습니다")
│        │  └─ ExploreButton      ← exploreButton      ("→ 지도에서 탐험하기")
│        ├─ RecordCard            ← recordCard         (카드1 · 나의 탐험 기록)
│        │  ├─ HeaderBand > Label ("나의 탐험 기록")
│        │  ├─ VisitedPrefix      ("현재까지" 고정)
│        │  ├─ VisitedCount       ← visitedCountText   (숫자만)
│        │  ├─ VisitedSuffix      ("곳을 탐험했어요!" 고정)
│        │  └─ CategoryRow        (HorizontalLayoutGroup)
│        │     ├─ Cell_역사   > Count  ← categoryCountTexts[0]
│        │     ├─ Cell_문화   > Count  ← categoryCountTexts[1]
│        │     ├─ Cell_교통   > Count  ← categoryCountTexts[2]
│        │     ├─ Cell_공공   > Count  ← categoryCountTexts[3]
│        │     └─ Cell_테스트 > Count  ← categoryCountTexts[4]
│        ├─ AnalysisCard          ← analysisCard       (카드2 · AI 탐험 분석)
│        │  ├─ HeaderBand > Label ("AI 탐험 분석")
│        │  ├─ NpcPortrait        (NPC 픽셀 캐릭터 자리 — 지금은 placeholder)
│        │  ├─ Quote              ("흠... 당신의 탐험 기록을 살펴봤어요" 고정)
│        │  ├─ AnalysisText       ← analysisText
│        │  ├─ DoneBadge          ← analysisDoneBadge  ("AI 분석 완료 ✓")
│        │  └─ RetryButton        ← retryButton        (분석 실패했을 때만 켜짐)
│        └─ RecommendCard         ← recommendCard      (카드3 · 다음 탐험 추천)
│           ├─ HeaderBand > Label ("다음 탐험 추천")
│           ├─ NpcPortrait
│           ├─ Quote              ("다음은 이곳을 탐험해 보는 건 어떨까요?" 고정)
│           ├─ Name               ← recommendNameText
│           ├─ Reason             ← recommendReasonText
│           └─ MapButton          ← recommendMapButton ("→ 지도에서 보기")
└─ Toast          [inactive]      ← toast
   └─ Label                       ← toastText
```

- `categoryCountTexts`는 **배열 순서가 곧 카테고리**다(`SpotCategory.DisplayOrder`). 순서가 어긋나면 엉뚱한 칸에
  숫자가 들어가고, 개수가 5개가 아니면 `ValidateReferences()`가 막는다.
- `visitedCountText`에는 숫자만 들어간다. `현재까지` / `곳을 탐험했어요!` 는 좌우 고정 텍스트다.
- 토스트는 스크롤 밖(`ReportPanel` 직속)에 둔다. 분석중에는 계속 떠 있고, 갱신 완료 토스트는 3초 뒤 저절로 꺼진다
  (시간은 `map_config`의 `reportToastAutoHideSeconds`).
- 분석은 리포트 화면을 닫아 둔 채로도 끝난다. 그때는 코루틴을 못 돌리므로 완료 토스트를 띄워만 두고,
  화면을 여는 순간부터 3초를 센다. 사용자가 못 본 토스트가 조용히 사라지지 않는다.

### `CardDetail/Panel/Front/MapButton` — 카드 앞면의 지도 버튼

`FlipButton`을 복제해 같은 줄 왼쪽(40, -1036 / 330 × 56)에 두었다. `CodexDetailView.mapButton`에 연결돼 있다.

```text
CardDetail > Panel > Front
├─ ImageFrame / CollectionBadge / Name / Description
├─ MapButton                  ← mapButton   ("→ 지도에서 보기")
└─ FlipButton                 ← flipButton  ("뒷면 보기 ›")
```

### 아직 남은 에셋

| 자리 | 지금 | 필요한 것 |
|---|---|---|
| `AnalysisCard/NpcPortrait`, `RecommendCard/NpcPortrait` | `Icons/placeholder.png` | NPC 픽셀 캐릭터 |
| 카드 `HeaderBand` | 글자만 | 밴드 아이콘 3종(캐리어 / 돋보기 / 나침반 등) |

동작에는 지장이 없으므로 에셋이 준비되면 `Image.sprite`만 갈아 끼우면 된다.

## 메뉴

| 메뉴 | 동작 |
|---|---|
| `Tools > Pixel Road > Setup Map Scene` | `MapScene.unity`에 `PixelRoadUIRoot` 인스턴스와 `PixelRoadApp`을 배치하고 참조를 연결한다. 중복 Canvas·EventSystem은 제거하고 카메라 등 나머지는 그대로 둔다 |
| `Tools > Pixel Road > Rebuild Loading Scene` | `Assets/Scenes/Loading.unity`를 **덮어써서** 다시 만들고 Build Settings에 등록한다. 현재 열린 씬은 건드리지 않는다 |
| `Tools > Pixel Road > Register Build Scenes` | Build Settings를 Loading → MapScene 순서로 맞춘다 |

프리팹을 다시 굽는 메뉴는 없다. UI 레이아웃은 프리팹 에셋을 직접 고친다(위 [프리팹](#ui-프리팹--씬-작업-가이드) 항목 참고).
