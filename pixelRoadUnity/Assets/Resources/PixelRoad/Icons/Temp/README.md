# 거점 아이콘 폴더

지도 마커와 도감 이미지는 **서로 다른 필드**를 따른다.

- 지도 마커 → `landmarks.json`의 `category`
- 도감 카드·상세 이미지 → `landmarks.json`의 `thumbnail`

PNG 파일 이름은 대소문자를 가리지 않으며, 확장자는 JSON에 쓰지 않는다.

## 지도 마커 조회 순서 (fallback)

1. `Icons/{category}` — JSON `category` 필드 (예: `역사.png`)
2. `Icons/{category 영문 별칭}` — 한글 category의 영문 파일명 (예: `history.png`)
3. `Icons/default` — `map_config.json`의 `defaultSpotIconName`
4. 없으면 프리팹에 지정된 스프라이트가 그대로 남는다

영문 별칭은 `SpotIconLibrary.CategoryFileAliases`에 정의돼 있다.

| category | 영문 파일명 |
| --- | --- |
| 역사 | `history` |
| 문화 | `culture` |
| 교통 | `transport` |
| 공공 | `public` |
| 테스트 | `test` |

같은 category의 랜드마크는 지도에서 같은 아이콘으로 보인다. 이게 의도된 동작이다.

## 도감 이미지 조회 순서 (fallback)

1. `Icons/{thumbnail}` — JSON `thumbnail` 필드 (예: `station.png`)
2. `Icons/placeholder` — `map_config.json`의 `placeholderThumbnailName`

**아직 해금하지 않은 랜드마크는 썸네일 파일이 있어도 `placeholder`를 보여 준다.**
해금하는 순간 실제 썸네일로 바뀐다(`CodexView.UpdateCard`).

지도 마커와 달리 `category`나 `default`로 넘어가지 않는다. 도감은 랜드마크마다 다른 사진을
보여 주는 자리이므로, 사진이 없으면 카테고리 아이콘 대신 `placeholder`가 낫다.

사용자 위치 마커는 `map_config.json`의 `userIconName`(기본 `user`)을 같은 방식으로 찾는다.

## 현재 landmarks.json이 쓰는 이름

- `category`: `역사`, `문화`, `교통`, `공공`, `테스트`
- `thumbnail`: `origin`, `station`, `tomb`, `temple`, `museum`, `landmark`, `fort`,
  `palace`, `gate`, `shrine`, `hanok`, `church`, `tower`, `park`, `market`, `stadium`

실제 PNG가 있는 것은 `station` 뿐이다. 따라서 지금 상태에서는

- 지도 마커: 모든 카테고리가 `default.png`로 떨어진다 → 카테고리별로 구분하려면
  `역사.png`(또는 `history.png`) 같은 파일을 이 폴더에 넣어야 한다.
- 도감 이미지: `station`을 쓰는 17곳만 실제 이미지가 나오고 나머지는 `placeholder.png`가 나온다.

## 임포트 설정 권장값

- Texture Type: `Sprite (2D and UI)`
- Filter Mode: `Point (no filter)` — 픽셀 아트 톤 유지
- Compression: `None` 또는 `High Quality`
- 마커 아이콘 크기: 정사각형 64x64 또는 128x128 (표시 크기는 `map_config.json`의 `spotMarkerPixelSize`가 결정)
- 도감 썸네일 크기: 카드 이미지 자리 비율에 맞춰 가로형 권장 (`placeholder.png`는 624x480)

Texture Type이 `Default`인 PNG도 런타임에서 스프라이트로 감싸 처리하지만,
`Sprite`로 임포트하는 쪽이 메모리와 로딩 면에서 낫다.

## 잠김 상태 표현

- 도감 카드: 이미지가 `placeholder`로 바뀌고, 어둡게 틴트되며, 자물쇠 아이콘이 켜진다.
- 지도 마커: 카테고리 아이콘은 그대로 두고 색만 어둡게 틴트된다.
