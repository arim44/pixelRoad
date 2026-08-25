using System;

namespace PixelRoad.Data
{
    /// <summary>
    /// POST {서버}/api/ai/report 요청 본문.
    /// <c>{ "visitedLandmarks": [ { "landmarkId": 1, "visitCount": 2 } ] }</c>
    /// </summary>
    [Serializable]
    public sealed class ReportRequest
    {
        public VisitedLandmarkPayload[] visitedLandmarks;
    }

    /// <summary>방문 랜드마크 한 건. 필드명이 곧 JSON 키라 이름을 바꾸면 안 된다.</summary>
    [Serializable]
    public sealed class VisitedLandmarkPayload
    {
        public int landmarkId;
        public int visitCount;
    }

    /// <summary>
    /// 200 OK 응답 본문 전체.
    /// <c>{ "success": true, "data": { "analysis": "...", "recommendation": {...} } }</c>
    ///
    /// 서버가 성공 여부로 한 번 감싸서 보내므로, 화면이 쓰는 실제 내용은 <see cref="data"/> 안에 있다.
    /// </summary>
    [Serializable]
    public sealed class ReportApiEnvelope
    {
        /// <summary>서버가 판단한 성공 여부. false면 data를 신뢰하지 않는다.</summary>
        public bool success;

        /// <summary>분석 결과 본문.</summary>
        public ReportResponse data;
    }

    /// <summary>
    /// 분석 결과 본문.
    /// <c>{ "analysis": "...", "recommendation": { "landmarkId": 24, "name": "...", "reason": "..." } }</c>
    /// </summary>
    [Serializable]
    public sealed class ReportResponse
    {
        /// <summary>AI가 분석한 탐험 성향.</summary>
        public string analysis;

        /// <summary>백엔드가 고른 다음 탐험 장소와 추천 이유.</summary>
        public ReportRecommendation recommendation;

        /// <summary>화면에 올려도 되는 응답인지. 분석 문구가 비면 실패로 본다.</summary>
        public bool IsUsable
        {
            get { return !string.IsNullOrWhiteSpace(analysis); }
        }

        /// <summary>추천 카드를 보여 줄 수 있는지. 추천은 없을 수도 있다고 보고 방어한다.</summary>
        public bool HasRecommendation
        {
            get { return recommendation != null && !string.IsNullOrWhiteSpace(recommendation.name); }
        }
    }

    /// <summary>추천 랜드마크 한 곳.</summary>
    [Serializable]
    public sealed class ReportRecommendation
    {
        public int landmarkId;
        public string name;
        public string reason;
    }
}
