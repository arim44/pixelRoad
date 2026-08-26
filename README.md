# 🗺️ PixelRoad

> **현실을 탐험하며 배우는 위치 기반 오픈소스 탐험 플랫폼**

GPS를 기반으로 실제 랜드마크를 방문하고, 지도·도감·AR·AI 탐험 리포트를 통해 역사와 문화 공간을 게임처럼 탐험할 수 있는 Unity 기반 모바일 애플리케이션입니다.

PixelRoad는 최종 사용자뿐 아니라 **자신의 지역·기관·콘텐츠에 맞는 탐험 애플리케이션을 제작하려는 제작자**를 위한 확장 가능한 오픈소스 구조를 지향합니다.

---

## ✨ 핵심 경험

```text
🗺️ 지도에서 탐험
        ↓
📍 실제 장소 방문
        ↓
🔓 랜드마크 발견 및 해금
        ↓
📖 도감에 수집
        ↓
🥽 AR로 현실 공간 탐험
        ↓
🤖 AI 탐험 리포트
        ↓
⭐ 다음 탐험 장소 추천
```

---

# 📖 프로젝트 소개

PixelRoad는 GPS 기반 위치 정보를 활용하여 사용자가 실제 랜드마크를 방문하고, 역사·문화 콘텐츠를 탐험과 수집의 방식으로 경험할 수 있도록 개발한 모바일 탐험 플랫폼입니다.

사용자는 지도에서 주변 랜드마크를 확인하고 실제 장소의 방문 반경에 진입하여 랜드마크를 해금합니다. 해금된 랜드마크는 도감에 기록되며, 상세 정보와 AR 탐험 기능을 통해 장소를 더욱 직관적으로 탐색할 수 있습니다.

또한 방문 기록을 기반으로 AI가 사용자의 탐험 내용을 분석하고 탐험 성향과 다음 탐험 장소를 추천합니다.

PixelRoad는 특정 지역이나 하나의 콘텐츠에만 종속되지 않습니다. 랜드마크 데이터와 이미지 리소스를 변경하여 지역 관광, 역사·문화유산 탐방, 현장학습, 캠퍼스 투어 등 다양한 목적의 탐험 애플리케이션으로 확장할 수 있도록 설계되었습니다.

---

# 🎯 개발 목적

* GPS 기반의 실제 장소 탐험 경험 제공
* 역사·문화 콘텐츠를 직접 방문하고 경험하는 학습 방식 제공
* 탐험 → 발견 → 수집으로 이어지는 게임형 학습 경험 구현
* AR을 활용한 현실 공간 기반 랜드마크 탐색
* AI를 통한 개인화된 탐험 분석 및 장소 추천
* 데이터 교체만으로 다양한 콘텐츠에 활용할 수 있는 오픈소스 플랫폼 구축

---

# 👥 누구를 위한 프로젝트인가?

PixelRoad는 두 가지 사용자를 고려하여 설계되었습니다.

## 📱 최종 사용자

PixelRoad 애플리케이션을 통해 실제 장소를 탐험하는 사용자입니다.

```text
지도 확인
   ↓
랜드마크 탐색
   ↓
실제 장소 방문
   ↓
GPS 방문 판정
   ↓
랜드마크 해금
   ↓
도감 수집
   ↓
AR 탐험
   ↓
AI 탐험 리포트
```

---

## 🛠️ 제작자

PixelRoad 소스코드를 기반으로 자신만의 탐험 애플리케이션을 제작하는 사용자입니다.

예를 들어 다음과 같은 콘텐츠를 구성할 수 있습니다.

* 🏛️ 지역 역사·문화유산 탐방
* 🗺️ 지역 관광 및 여행 코스
* 🎓 대학 캠퍼스 투어
* 🏫 학교 현장학습
* 🖼️ 박물관·문화시설 안내
* 🎉 지역 축제 탐험
* 🏢 기업·기관 공간 안내
* ☕ 카페·상점 탐방

제작자는 랜드마크 데이터와 리소스를 자신의 목적에 맞게 변경하여 PixelRoad 기반의 새로운 탐험 콘텐츠를 구성할 수 있습니다.

```text
PixelRoad 소스코드
        ↓
랜드마크 데이터 수정
        ↓
이미지 및 콘텐츠 추가
        ↓
Unity에서 앱 확인
        ↓
Android Build
        ↓
나만의 탐험 앱 제작
```

---

# 📍 주요 기능

## 1. 🗺️ 지도 및 랜드마크 탐색

사용자의 현재 위치와 주변 랜드마크를 지도에서 확인할 수 있습니다.

* 현재 위치 표시
* 랜드마크 위치 표시
* 랜드마크 선택
* 랜드마크 정보 확인
* 잠금 및 해금 상태 확인

---

## 2. 📍 GPS 기반 방문 판정

사용자와 랜드마크 사이의 거리를 계산하여 설정된 방문 반경 진입 여부를 확인합니다.

```text
현재 GPS 위치 확인
        ↓
랜드마크와 거리 계산
        ↓
방문 반경 진입 여부 확인
        ↓
랜드마크 발견
        ↓
도감 해금
```

---

## 3. 🔓 랜드마크 해금 및 카드

랜드마크의 방문 조건을 만족하면 해당 장소를 발견하고 해금합니다.

해금된 랜드마크는 카드 형태로 확인할 수 있으며, 이후 도감에 기록됩니다.

---

## 4. 📖 랜드마크 도감

방문한 랜드마크를 수집하고 확인할 수 있습니다.

도감에서는 다음 정보를 확인할 수 있습니다.

* 대표 이미지
* 랜드마크 이름
* 지역 및 카테고리
* 발견 여부
* 잠금 상태

방문하지 않은 랜드마크는 잠금 상태로 표시됩니다.

---

## 5. 🏛️ 랜드마크 상세 정보

해금된 랜드마크의 상세 정보를 제공합니다.

* 이름
* 한 줄 소개
* 상세 설명
* 역사·문화 정보
* 위치 정보
* AR 탐험 기능

---

## 6. 🥽 AR 랜드마크 탐험

현실 공간에서 랜드마크를 더욱 직관적으로 탐색할 수 있는 AR 기능을 제공합니다.

### 전체 모드

주변의 여러 랜드마크를 AR 화면에서 확인할 수 있습니다.

### 집중 모드

특정 랜드마크를 선택하여 방향과 위치를 확인하며 해당 장소를 탐색할 수 있습니다.

```text
랜드마크 선택
        ↓
AR 탐험 진입
        ↓
랜드마크 방향 확인
        ↓
목표 위치 탐색
        ↓
방문 반경 진입
        ↓
랜드마크 해금
```

---

## 7. 🤖 AI 탐험 리포트

사용자의 랜드마크 방문 기록을 기반으로 AI가 탐험 내용을 분석합니다.

```text
Unity 방문 기록
        ↓
NestJS API 요청
        ↓
랜드마크 데이터 분석
        ↓
AI Prompt 생성
        ↓
LLM 추론
        ↓
탐험 분석 결과 생성
        ↓
Unity 결과 화면 출력
```

AI 리포트에서는 다음과 같은 정보를 제공합니다.

* 방문 기록 기반 탐험 분석
* 사용자의 관심 분야에 대한 자연어 총평
* 방문 이력을 기반으로 한 추천 랜드마크
* 추천 장소의 추천 이유

---

# 🧩 시스템 아키텍처

```text
                         PixelRoad
                             │
              ┌──────────────┴──────────────┐
              │                             │
        Unity Mobile Client             NestJS Backend
              │                             │
       ┌──────┼─────────┐                   │
       │      │         │                   │
      GPS    Map       AR                  AI API
       │      │         │                   │
       └──────┼─────────┘                   │
              │                             │
        landmarks.json ─────────────→ AI Report API
              │                             │
       Landmark Discovery                   │
              │                             ↓
        Collection System             Hugging Face API
              │                             │
              └─────────── AI Request ─────┘
                            │
                            ↓
                      AI 탐험 리포트
```

---

# 📂 프로젝트 구조

```text
pixelRoad/
│
├── pixelroad-backend/
│   ├── src/
│   ├── package.json
│   └── ...
│
├── pixelRoadUnity/
│   ├── Assets/
│   ├── Packages/
│   └── ...
│
└── .github/
    └── workflows/
```

## 주요 구성

| 경로                   | 설명                                          |
| -------------------- | ------------------------------------------- |
| `pixelRoadUnity/`    | Unity 기반 모바일 클라이언트                          |
| `pixelroad-backend/` | NestJS 기반 API 및 AI 탐험 리포트 서버                |
| `.github/workflows/` | GitHub Actions 기반 Azure App Service 배포 워크플로 |

---

# 🗂️ 콘텐츠 데이터 구조

PixelRoad는 랜드마크 정보를 JSON 기반으로 관리합니다.

이를 통해 제작자는 데이터와 리소스를 변경하여 새로운 탐험 콘텐츠를 구성할 수 있습니다.

## `landmarks.json`

```json
{
  "id": 1,
  "name": "경복궁",
  "category": "궁궐",
  "collectionTitle": "조선",
  "address": "서울특별시 종로구 사직로 161",
  "latitude": 37.579617,
  "longitude": 126.977041,
  "visitRadius": 50,
  "thumbnail": "gyeongbokgung",
  "shortDescription": "조선의 첫 번째 법궁입니다.",
  "history": "조선 왕조의 건국과 함께 세워진 궁궐입니다.",
  "tags": ["조선", "궁궐", "왕실"],
  "view360Image": null
}
```

### 제작자가 수정할 수 있는 주요 항목

* `name` : 랜드마크 이름
* `category` : 콘텐츠 분류
* `collectionTitle` : 도감 컬렉션
* `address` : 주소
* `latitude` : 위도
* `longitude` : 경도
* `visitRadius` : 방문 인정 반경
* `thumbnail` : 대표 이미지
* `shortDescription` : 간단한 설명
* `history` : 상세 콘텐츠
* `tags` : 검색 및 AI 분석에 활용할 태그

따라서 소스코드를 크게 수정하지 않고도 랜드마크 데이터를 변경하여 새로운 지역과 콘텐츠에 활용할 수 있습니다.

---

# 📖 방문 기록

사용자의 방문 기록은 기기별 로컬 저장소에 관리됩니다.

예시:

```json
{
  "landmarkId": 1,
  "visitCount": 2,
  "firstVisitedAt": "2026-08-01T10:20:00",
  "lastVisitedAt": "2026-08-03T15:40:00"
}
```

방문 기록은 AI 탐험 리포트 생성 시 활용됩니다.

---

# 🛠️ 기술 스택

| 분야                | 기술                         |
| ----------------- | -------------------------- |
| Mobile Client     | Unity, C#                  |
| GPS / Location    | Unity Location Service     |
| Map               | OpenStreetMap 기반 지도        |
| AR                | Unity AR Foundation        |
| Backend           | NestJS 11                  |
| Runtime           | Node.js                    |
| API Documentation | Swagger                    |
| AI                | Hugging Face Inference API |
| Data              | JSON                       |
| Version Control   | Git, GitHub                |
| CI/CD             | GitHub Actions             |
| Deployment        | Azure App Service          |

---

# 🚀 시작하기

## 1. Unity Client 실행

```text
Unity Hub 실행
      ↓
pixelRoadUnity 프로젝트 열기
      ↓
Unity Editor에서 실행
```

제작자는 Unity Editor에서 랜드마크 데이터와 리소스를 수정한 뒤 Android 빌드를 통해 자신만의 탐험 애플리케이션을 제작할 수 있습니다.

---

## 2. Backend 실행

```bash
cd pixelroad-backend
npm install
npm run start:dev
```

기본적으로 환경변수가 필요한 기능은 별도의 환경 설정이 필요합니다.

```env
PORT=3000
```

AI 기능과 외부 서비스 연동에 필요한 환경변수는 실제 키를 GitHub에 직접 업로드하지 않고 환경변수 또는 배포 환경의 애플리케이션 설정으로 관리해야 합니다.

---

# ☁️ Backend Deployment

PixelRoad Backend는 GitHub Actions를 통해 Azure App Service에 배포할 수 있도록 구성되어 있습니다.

```text
GitHub Push
     ↓
GitHub Actions
     ↓
Build
     ↓
Artifact 생성
     ↓
Azure Web Apps Deploy
     ↓
Azure App Service
```

워크플로 파일은 다음 경로에 위치합니다.

```text
.github/workflows/
```

---

# 🧑‍💻 제작자를 위한 활용 방법

PixelRoad는 단순히 하나의 탐험 앱으로 사용하는 것뿐 아니라, 다른 제작자가 자신의 콘텐츠를 적용할 수 있도록 설계되었습니다.

## 새로운 탐험 콘텐츠 만들기

### Step 1. 프로젝트 가져오기

```bash
git clone <repository-url>
```

### Step 2. 랜드마크 데이터 수정

`pixelRoadUnity/Assets/Resources/PixelRoad/landmarks.json`에서 원하는 장소를 추가하거나 수정합니다.

### Step 3. 이미지 리소스 추가

랜드마크 데이터에서 사용하는 대표 이미지와 필요한 리소스를 추가합니다.

### Step 4. 방문 조건 설정

각 랜드마크에 위치와 방문 반경을 설정합니다.

```json
{
  "latitude": 37.579617,
  "longitude": 126.977041,
  "visitRadius": 50
}
```

### Step 5. Unity에서 테스트

Unity Editor에서 지도, GPS, 랜드마크 표시 및 방문 판정 흐름을 확인합니다.

### Step 6. Android 빌드

Unity Editor에서 Android 빌드 기능을 실행하여 제작한 콘텐츠를 모바일 애플리케이션으로 빌드합니다.

```text
랜드마크 데이터 구성
        ↓
Unity 실행 및 테스트
        ↓
Android Build
        ↓
APK 생성
        ↓
모바일 탐험 앱 배포
```

---

# 🗺️ 활용 가능 분야

PixelRoad의 구조는 데이터와 콘텐츠를 교체하여 다음과 같은 분야에 활용할 수 있습니다.

* 지역 관광
* 역사·문화유산 교육
* 학교 현장체험학습
* 대학 캠퍼스 투어
* 박물관 및 문화시설 안내
* 지역 축제
* 기업·기관 공간 안내
* 상점 및 지역 상권 탐방

---

# 🗺️ 개발 로드맵

## 현재 구현

* [x] GPS 위치 확인
* [x] 지도 및 랜드마크 표시
* [x] GPS 기반 방문 판정
* [x] 랜드마크 해금
* [x] 랜드마크 카드
* [x] 랜드마크 도감
* [x] 랜드마크 상세 정보
* [x] 지도와 도감 연동
* [x] AR 전체 모드
* [x] AR 집중 모드
* [x] AI 탐험 리포트
* [x] NestJS API 구축
* [x] Azure App Service Backend 배포

## 향후 확장

* [ ] 사용자 랜드마크 등록
* [ ] 360도 이미지 탐험
* [ ] 업적 시스템
* [ ] 방문 통계
* [ ] 인기 랜드마크
* [ ] 탐험 코스 추천
* [ ] AI 기능 고도화
* [ ] 지역별 콘텐츠 패키지 제공

---

# 🤝 기여하기

PixelRoad는 누구나 확장하고 개선할 수 있는 오픈소스 프로젝트를 지향합니다.

1. Repository를 Fork합니다.
2. 새로운 Branch를 생성합니다.

```text
feature/기능명
```

3. 기능을 개발합니다.
4. 프로젝트의 코드 및 Commit Convention을 준수합니다.
5. Pull Request를 생성합니다.

콘텐츠 데이터 확장, 새로운 지역 지원, 기능 개선 등의 기여를 환영합니다.

---

# 📄 License

이 프로젝트는 **MIT License**를 따릅니다.

누구나 자유롭게 사용, 수정, 배포 및 상업적으로 이용할 수 있습니다. 단, 저작권 표시 및 라이선스 고지를 유지해야 합니다.

자세한 내용은 `LICENSE` 파일을 참고하세요.

---

# 👥 Team

## Compass Studio

| 역할              | 담당                                  |
| --------------- | ----------------------------------- |
| Team Leader     | 기획 · PM · Backend · AI · 문서 및 배포    |
| Unity Lead      | Unity Client · GPS · 지도 · 랜드마크 · 도감 |
| Unity Developer | Unity Client · AR · UI/UX           |

---

# 🌟 Project Vision

> **"현실을 직접 탐험하고, 발견하며, 배우는 가장 쉬운 방법."**

PixelRoad는 실제 장소를 직접 방문하는 경험과 GPS·AR·AI 기술을 결합하여, 탐험과 학습이 자연스럽게 이어지는 오픈소스 플랫폼을 목표로 합니다.

또한 제작자가 자신의 지역과 목적에 맞는 콘텐츠를 구성하고 새로운 탐험 애플리케이션을 제작할 수 있도록 지속적으로 확장 가능한 구조를 지향합니다.
