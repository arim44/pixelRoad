# 랜드마크와 방문 기록 데이터

## 랜드마크 원본

런타임 원본은 `Assets/Resources/PixelRoad/landmarks.json`이다. 파일 최상위는 배열이며, `map_config.json`의 `landmarksJsonResourcePath`로 Resources 경로를 지정한다.

| 필드 | 타입 | 설명 |
|---|---|---|
| `id` | number | 영구적으로 유지할 양의 정수 랜드마크 ID |
| `name` | string | 랜드마크 이름 |
| `category` | string | 성격 분류. 도감 상단 필터 칩이 이 값으로 만들어지므로 한국어로 적는다(현재 `역사` / `문화` / `교통` / `공공` / `테스트`) |
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

최초 22개 CSV 행은 숫자 ID `1`~`22`로 한 번만 매핑했다. 앞으로 행 순서가 바뀌어도 기존 ID를 재사용하거나 다시 번호를 매기면 안 된다. `address`는 기존 CSV에 원본 값이 없어서 비워 두었으며, 실제 주소를 확인한 뒤 채워야 한다. `thumbnail` 키는 준비했지만 실제 PNG가 없는 항목은 런타임 기본 도형으로 표시된다.

## 수록 권역

현재 랜드마크는 83개이며 다음 권역으로 나뉜다. ID는 추가 순서대로 이어 붙인다.

| ID 범위 | 권역 | 개수 | 비고 |
|---|---|---|---|
| 1~18 | 강남·서초권 | 18 | 최초 CSV. `1`은 테스트 기준점 |
| 19~20 | 문정동권 | 2 | 가든파이브·법조타운 |
| 21~22 | 부평역권 | 2 | 최초 CSV의 인천 항목 |
| 23~62 | 서울 주요 랜드마크 | 40 | 궁궐·성문·박물관·공원·시장·초고층 등 |
| 63~70 | 오류동역권 | 8 | 구로 서남권 |
| 71~77 | 부평역권 | 7 | 21~22와 같은 권역 |
| 78~83 | 평택역권 | 6 | 경기 남부 |

23번 이후 좌표는 OpenStreetMap(Overpass API·Nominatim)에서 이름으로 조회한 값을 그대로 썼다. `visitRadius`는 대상 규모에 맞춰 50~200m로 정했고, 서로 다른 랜드마크의 반경이 겹치지 않도록 조정했다.

권역이 넓어지면서 `map_config.json`의 `bounds`도 평택(위도 약 36.99)과 부평 서측(경도 약 126.70)을 포함하도록 넓혔다. `bounds`는 지도 표시 범위가 아니라 해금 판정용 공간 인덱스의 기준 위도를 잡는 값이므로, 랜드마크를 더 먼 지역에 추가하면 이 값도 함께 넓혀야 한다.

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

