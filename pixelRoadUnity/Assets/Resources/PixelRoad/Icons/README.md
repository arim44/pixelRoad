# 거점 아이콘 폴더

`spots.csv`의 `icon` 열 값과 **같은 이름**으로 PNG를 이 폴더에 넣으면 지도 마커와 도감 카드에 자동으로 적용된다.
파일 이름은 대소문자를 가리지 않으며, 확장자는 CSV에 쓰지 않는다.

## 조회 순서 (fallback)

1. `Icons/{icon}` — CSV `icon` 열 (예: `station.png`)
2. `Icons/{category}` — CSV `category` 열 (예: `history.png`)
3. `Icons/default` — `map_config.json`의 `defaultSpotIconName`
4. 없으면 코드로 생성한 마름모 도형 마커 (빨강 = 해금, 회색 = 잠김)

사용자 위치 마커는 `map_config.json`의 `userIconName`(기본 `user`)을 같은 방식으로 찾고,
없으면 원형 도형 마커로 대체된다.

## 현재 CSV가 쓰는 아이콘 이름

`origin`, `station`, `tomb`, `temple`, `museum`, `landmark`, `fort`

## 임포트 설정 권장값

- Texture Type: `Sprite (2D and UI)`
- Filter Mode: `Point (no filter)` — 픽셀 아트 톤 유지
- Compression: `None` 또는 `High Quality`
- 크기: 정사각형 64x64 또는 128x128 (표시 크기는 `map_config.json`의 `spotMarkerPixelSize`가 결정)

Texture Type이 `Default`인 PNG도 런타임에서 스프라이트로 감싸 처리하지만,
`Sprite`로 임포트하는 쪽이 메모리와 로딩 면에서 낫다.

## 잠김 상태 표현

아이콘이 있는 거점은 잠김일 때 어둡게 틴트되고, 해금되면 원색으로 돌아온다.
아이콘이 없는 거점은 도형 마커의 색이 회색 → 빨강으로 바뀐다.
