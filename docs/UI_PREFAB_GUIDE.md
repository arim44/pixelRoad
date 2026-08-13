# UI 프리팹 작업 가이드

고정 UI를 런타임 코드 생성 방식에서 다음 세 프리팹으로 옮겼다.

화면의 `ZoomIn`/`ZoomOut` 버튼은 제거했다. 지도 확대·축소는 모바일 핀치 또는 에디터/PC의 마우스 휠 입력으로만 동작한다.

| 프리팹 | 용도 |
|---|---|
| `Assets/Resources/PixelRoad/UI/PixelRoadUIRoot.prefab` | 지도, 상단 바, 상태, 상세 패널, 도감 창 전체 |
| `Assets/Resources/PixelRoad/UI/LandmarkMarker.prefab` | 지도 위 동적 랜드마크 마커 |
| `Assets/Resources/PixelRoad/UI/LandmarkCodexCard.prefab` | 도감의 동적 랜드마크 카드 |

UI 작업자는 각 프리팹을 Prefab Mode로 열어 앵커, 크기, 색상, 폰트, 이미지와 자식 계층을 수정하면 된다. 런타임은 `PixelRoadUiBindings`, `LandmarkMarkerView`, `LandmarkCardView`에 직렬화된 참조를 사용하므로 계층 이름을 바꿔도 참조가 유지되는 한 동작한다.

기본 한글 폰트는 `Assets/Resources/PixelRoad/Fonts/Galmuri11 SDF.asset`이다. 동적 TMP 폰트이므로 한글 데이터가 추가되어도 런타임에 글리프를 생성하며, 원본 `Galmuri11.ttf`와 함께 유지해야 한다.

주의 사항:

- `PixelRoadUiBindings`의 Inspector 참조를 비우지 않는다.
- `LandmarkMarker`에는 `Button`을 추가하지 않는다. 모바일 드래그와 탭을 구분하기 위해 `MapMarkerTapTarget`을 사용한다.
- `MapViewport`의 `PixelRoadMapInput`, `RectMask2D`, 배경 `Image`를 유지한다.
- `LiveVectorMap`은 `RawImage`, `MapMarkerOverlay`는 전체 Stretch 상태를 유지한다.
- `LandmarkCodexCard` 루트의 `Button`과 `LandmarkCardView` 참조를 유지한다.
- 동적 객체 이름 `Spot_{id}`, `Codex_{id}`는 테스트와 디버깅에 사용한다.

`Tools > Pixel Road > Rebuild UI Prefabs`는 프리팹이 유실됐을 때 기본본을 다시 만드는 복구용 메뉴다. 이 메뉴는 세 프리팹을 기본 레이아웃으로 덮어쓰므로 UI 작업 중에는 실행하지 않는다.
