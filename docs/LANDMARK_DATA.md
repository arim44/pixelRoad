# 랜드마크와 방문 기록 데이터

## 랜드마크 원본

런타임 원본은 `Assets/Resources/PixelRoad/landmarks.json`이다. 파일 최상위는 배열이며, `map_config.json`의 `landmarksJsonResourcePath`로 Resources 경로를 지정한다.

| 필드 | 타입 | 설명 |
|---|---|---|
| `id` | number | 영구적으로 유지할 양의 정수 랜드마크 ID |
| `name` | string | 랜드마크 이름 |
| `category` | string | 궁궐, 박물관, 역, 공원 등 분류 |
| `collectionTitle` | string | 선사, 백제, 조선, 일제강점기, 현대 등 도감 묶음 |
| `address` | string | 전체 주소. 아직 확인되지 않은 항목은 빈 문자열 |
| `latitude` | number | WGS84 위도 |
| `longitude` | number | WGS84 경도 |
| `visitRadius` | number | 방문 인정 반경(m) |
| `thumbnail` | string | `Resources/PixelRoad/Icons`에서 찾을 이미지 키 |
| `shortDescription` | string | 지도 카드에 표시할 한 줄 설명 |
| `history` | string | 역사·유래·특징 설명 |
| `tags` | string[] | AI 추천 및 검색에 쓸 태그 |
| `view360Image` | string/null | 360도 이미지 키. 없으면 `null` |

현재 22개 CSV 행은 숫자 ID `1`~`22`로 한 번만 매핑했다. 앞으로 행 순서가 바뀌어도 기존 ID를 재사용하거나 다시 번호를 매기면 안 된다. `address`는 기존 CSV에 원본 값이 없어서 비워 두었으며, 실제 주소를 확인한 뒤 채워야 한다. `thumbnail` 키는 준비했지만 실제 PNG가 없는 항목은 런타임 기본 도형으로 표시된다.

## 로컬 방문 기록

방문 기록은 다음 경로에 저장한다.

```text
Path.Combine(Application.persistentDataPath, "visited_landmarks.json")
```

파일 예시:

```json
[
  {
    "landmarkId": 1,
    "visitCount": 2,
    "firstVisitedAt": "2026-08-01T10:20:00",
    "lastVisitedAt": "2026-08-03T15:40:00"
  }
]
```

저장 규칙:

- 첫 방문은 `visitCount`를 `1`로 만들고 최초·마지막 방문 시각을 함께 기록한다.
- 같은 기기 로컬 날짜의 재방문은 횟수를 늘리지 않는다.
- 마지막 방문일의 다음 날짜부터 다시 반경 안에 들어오면 횟수를 `1` 늘린다.
- 시각 문자열은 기획 예시에 맞춰 `yyyy-MM-ddTHH:mm:ss` 형식을 사용한다.
- 레코드는 `landmarkId` 순으로 저장한다.
- 정확한 GPS 좌표는 파일에 저장하지 않는다.

기존 개발 버전의 `PlayerPrefs` 키 `PixelRoad.UnlockedSpots`는 과거 방문 시각을 알 수 없기 때문에 자동 변환하지 않는다. 기존 키는 삭제하지 않지만 새 버전에서는 `visited_landmarks.json`만 사용한다.

## 관련 코드

- `LandmarkJsonParser`: 최상위 JSON 배열 파싱, ID·좌표 검증
- `VisitRepository`: 방문 파일 로드, 날짜별 횟수 계산, 임시 파일을 거친 저장
- `PixelRoadApp`: GPS 반경 판정 후 방문 기록 호출

