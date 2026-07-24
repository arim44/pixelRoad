# 🗺️ PixelRoad

> GPS 기반(위치) 랜드마크 탐험 및 AI 교육 플랫폼

![License](https://img.shields.io/badge/License-MIT-green.svg)
![Unity](https://img.shields.io/badge/Unity-2022+-black.svg)
![Backend](https://img.shields.io/badge/NestJS-red.svg)
![AI](https://img.shields.io/badge/AI-Gemma-blue.svg)

---

## 📖 프로젝트 소개

**PixelRoad**는 GPS 기반 위치 정보를 활용하여 실제 랜드마크를 방문하고 역사·문화 정보를 게임처럼 학습할 수 있는 오픈소스 플랫폼입니다.

사용자는 실제 장소를 방문하여 랜드마크를 발견하고 도감을 수집하며, AI 탐험 리포트를 통해 자신의 탐험 성향과 추천 장소를 확인할 수 있습니다.

또한 JSON 기반 데이터 구조를 사용하여 누구나 자신만의 랜드마크 데이터를 추가하여 활용할 수 있도록 설계되었습니다.

---

## 🎯 개발 목적

- GPS 기반 현실 탐험 경험 제공
- 지역 역사 및 문화 콘텐츠 접근성 향상
- 게임 요소를 활용한 교육 플랫폼 제공
- AI를 활용한 탐험 성향 분석 및 추천
- 누구나 확장 가능한 오픈소스 플랫폼 구축

---

## ✨ 주요 기능

### 📍 GPS 방문 인증

사용자의 현재 위치를 기반으로 랜드마크 방문 여부를 자동으로 판정합니다.

---

### 🗺️ 지도 및 랜드마크 표시

현재 위치와 주변 랜드마크를 지도에서 확인할 수 있습니다.

---

### 📖 랜드마크 도감

방문한 랜드마크를 자동으로 수집하며 도감 형태로 관리합니다.

---

### 🏛️ 랜드마크 상세 정보

랜드마크의

- 한 줄 소개
- 상세 설명
- 역사 정보

를 제공합니다.

---

### 🤖 AI 탐험 리포트

방문 기록을 기반으로 AI가

- 탐험 성향 분석
- 탐험 칭호
- 추천 랜드마크
- 탐험 총평

을 생성합니다.

예시

```
🚆 철도 탐험가

철도와 교통 중심의 랜드마크를 주로 탐방하는 성향입니다.

추천

- 서울역
- 수원역
- 문화역서울284

총평

다양한 철도 문화유산을 경험하며 대한민국 교통의 역사를 자연스럽게 학습하고 있습니다.
```

---

## 🖼️ 시스템 구성

```text
Unity (Client)
        ▼
GPS 위치 확인
        ▼
랜드마크(JSON)
        ▼
방문 판정
        ▼
도감 저장
        ▼
AI 탐험 리포트 요청
        ▼
NestJS API
        ▼
Gemma (Hugging Face)
        ▼
AI 결과 반환
        ▼
Unity 화면 출력
```

---

## 📦 프로젝트 구조

```
PixelRoad

├── frontend
│   └── Unity Project
│
├── backend
│   └── NestJS
│
├── docs
│
└── README.md
```

---

## 🛠 기술 스택

### Frontend

- Unity
- C#

### Backend

- NestJS

### AI

- Hugging Face Inference API
- Google Gemma

### Data

- JSON

### Version Control

- Git
- GitHub

---

## 📂 JSON 구조

### landmarks.json

랜드마크 정보를 저장합니다.

```json
{
  "id": 1,
  "name": "경복궁",
  "category": "궁궐",
  "region": "서울",
  "latitude": 37.579617,
  "longitude": 126.977041
}
```

---

### visited_landmarks.json

사용자의 방문 기록을 저장합니다.

```json
{
  "landmarkId": 1,
  "visitedAt": "2025-08-15T15:30:00"
}
```

---

## 🚀 실행 방법

### Frontend

```bash
Unity Hub 실행

↓

프로젝트 열기

↓

Play
```

---

### Backend

```bash
npm install

npm run start:dev
```

---

## 📚 Wiki

프로젝트의 자세한 문서는 Wiki에서 확인할 수 있습니다.

- PROJECT-CONVENTION
- FRONTEND-CONVENTION
- BACKEND-CONVENTION
- JSON-SPECIFICATION
- API-SPECIFICATION
- AI-REPORT

---

## 📅 개발 로드맵

### MVP

- [x] GPS 위치 확인
- [x] 지도 및 랜드마크 표시
- [x] GPS 방문 인증
- [x] 랜드마크 도감
- [x] 랜드마크 상세 정보
- [x] AI 탐험 리포트

### 향후 확장

- [ ] 사용자 랜드마크 등록
- [ ] 업적 시스템
- [ ] 방문 통계
- [ ] 인기 랜드마크
- [ ] AI 질의응답
- [ ] AR 탐험

---

## 🤝 기여하기

1. Repository를 Fork합니다.
2. Feature 브랜치를 생성합니다.

```
feature/기능명
```

3. 기능을 개발합니다.
4. Commit Convention을 준수합니다.
5. Pull Request를 생성합니다.

---

## 📄 License

이 프로젝트는 **MIT License**를 따릅니다.

누구나 자유롭게 사용, 수정, 배포 및 상업적으로 이용할 수 있으며, 저작권 표시 및 라이선스 고지만 유지하면 됩니다.

---

## 👥 Team

**Compass Studio**

- Team Leader : 기획 / Backend / AI
- Developer : Unity Client
- Developer : Unity Client

---

## 🌟 프로젝트 비전

> **"현실을 탐험하며 배우는 가장 쉬운 방법."**

PixelRoad는 누구나 자신의 지역을 탐험하고, 역사와 문화를 게임처럼 즐기며 학습할 수 있는 오픈소스 플랫폼을 목표로 합니다.
