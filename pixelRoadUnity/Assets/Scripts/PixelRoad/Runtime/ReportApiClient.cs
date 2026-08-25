using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using PixelRoad.Data;
using UnityEngine;
using UnityEngine.Networking;

namespace PixelRoad.Runtime
{
    /// <summary>
    /// AI 탐험 리포트 API 호출기.
    ///
    /// <c>POST {서버}/api/ai/report</c> 로 방문 기록을 보내고 분석 결과를 받는다.
    /// 백엔드가 아직 없으므로 map_config의 reportApiUrl이 비어 있으면 임시 응답을 만들어 돌려준다.
    /// 임시 응답도 실제 응답과 같은 <see cref="ReportResponse"/> 타입이라, 서버가 붙어도 화면 코드는 그대로다.
    /// </summary>
    public static class ReportApiClient
    {
        /// <summary>
        /// 방문 기록을 보내고 분석 결과를 받아 콜백으로 넘긴다.
        /// 성공하면 <paramref name="onSuccess"/>, 실패하면 사용자에게 보여 줄 문구와 함께 <paramref name="onFailure"/>를 부른다.
        /// </summary>
        public static IEnumerator Request(
            MapConfig config,
            IReadOnlyList<VisitedLandmarkRecord> visits,
            IList<SpotRuntimeState> spots,
            Action<ReportResponse> onSuccess,
            Action<string> onFailure)
        {
            if (config == null)
            {
                onFailure?.Invoke("설정을 읽지 못했습니다");
                yield break;
            }

            if (visits == null || visits.Count == 0)
            {
                onFailure?.Invoke("탐험 기록이 없습니다");
                yield break;
            }

            if (!config.HasReportEndpoint)
            {
                yield return BuildMockResponse(config, spots, onSuccess);
                yield break;
            }

            string body = JsonUtility.ToJson(BuildRequest(visits));
            byte[] payload = Encoding.UTF8.GetBytes(body);

            using (UnityWebRequest request = new UnityWebRequest(config.reportApiUrl, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(payload);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = Mathf.Max(1, config.reportRequestTimeoutSeconds);

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("[PixelRoad] 리포트 요청 실패(" + request.responseCode + "): " + request.error);
                    onFailure?.Invoke("AI 분석에 실패했어요");
                    yield break;
                }

                ReportApiEnvelope envelope = null;
                string parseError = null;
                try
                {
                    envelope = JsonUtility.FromJson<ReportApiEnvelope>(request.downloadHandler.text);
                }
                catch (Exception exception)
                {
                    parseError = exception.Message;
                }

                if (parseError != null)
                {
                    Debug.LogWarning("[PixelRoad] 리포트 응답을 해석하지 못했습니다: " + parseError);
                    onFailure?.Invoke("AI 분석에 실패했어요");
                    yield break;
                }

                ReportResponse response = envelope != null && envelope.success ? envelope.data : null;
                if (response == null || !response.IsUsable)
                {
                    onFailure?.Invoke("AI 분석에 실패했어요");
                    yield break;
                }

                onSuccess?.Invoke(response);
            }
        }

        /// <summary>방문 기록을 요청 DTO로 옮긴다. 서버가 보는 필드는 landmarkId와 visitCount뿐이다.</summary>
        private static ReportRequest BuildRequest(IReadOnlyList<VisitedLandmarkRecord> visits)
        {
            VisitedLandmarkPayload[] payload = new VisitedLandmarkPayload[visits.Count];
            for (int i = 0; i < visits.Count; i++)
            {
                payload[i] = new VisitedLandmarkPayload
                {
                    landmarkId = visits[i].landmarkId,
                    visitCount = visits[i].visitCount,
                };
            }

            return new ReportRequest { visitedLandmarks = payload };
        }

        /// <summary>
        /// 서버가 없을 때 쓰는 임시 응답.
        /// 가장 많이 해금한 카테고리로 성향 문구를 만들고, 아직 잠긴 랜드마크 한 곳을 추천으로 고른다.
        /// 분석중 화면을 눈으로 확인할 수 있도록 설정된 만큼 일부러 뜸을 들인다.
        /// </summary>
        private static IEnumerator BuildMockResponse(
            MapConfig config,
            IList<SpotRuntimeState> spots,
            Action<ReportResponse> onSuccess)
        {
            if (config.reportMockDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(config.reportMockDelaySeconds);
            }

            string topCategory = FindTopUnlockedCategory(spots);
            SpotDefinition suggestion = FindLockedSuggestion(spots);

            ReportResponse response = new ReportResponse
            {
                analysis = string.IsNullOrEmpty(topCategory)
                    ? "아직 탐험 기록이 적어 성향을 읽는 중이에요."
                    : topCategory + " 장소를 중심으로 탐험하는 성향입니다.",
                recommendation = suggestion == null
                    ? null
                    : new ReportRecommendation
                    {
                        landmarkId = suggestion.LandmarkId,
                        name = suggestion.DisplayName,
                        reason = string.IsNullOrEmpty(topCategory)
                            ? "가까운 곳부터 둘러보면 탐험을 시작하기 좋아요."
                            : topCategory + " 장소를 주로 탐험하고 있어 이곳을 방문하면 탐험 범위를 넓힐 수 있습니다.",
                    },
            };

            onSuccess?.Invoke(response);
        }

        /// <summary>해금한 랜드마크가 가장 많은 카테고리 이름. 하나도 없으면 빈 문자열.</summary>
        private static string FindTopUnlockedCategory(IList<SpotRuntimeState> spots)
        {
            if (spots == null)
            {
                return string.Empty;
            }

            string[] order = SpotCategory.DisplayOrder;
            int[] counts = new int[order.Length];
            for (int i = 0; i < spots.Count; i++)
            {
                SpotRuntimeState state = spots[i];
                if (!state.IsUnlocked)
                {
                    continue;
                }

                int index = SpotCategory.IndexOf(SpotCategory.Normalize(state.Definition.Category));
                if (index >= 0)
                {
                    counts[index]++;
                }
            }

            int best = -1;
            int bestCount = 0;
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] > bestCount)
                {
                    bestCount = counts[i];
                    best = i;
                }
            }

            return best >= 0 ? order[best] : string.Empty;
        }

        /// <summary>아직 잠긴 랜드마크 중 첫 번째. 전부 해금했으면 null.</summary>
        private static SpotDefinition FindLockedSuggestion(IList<SpotRuntimeState> spots)
        {
            if (spots == null)
            {
                return null;
            }

            for (int i = 0; i < spots.Count; i++)
            {
                if (!spots[i].IsUnlocked)
                {
                    return spots[i].Definition;
                }
            }

            return null;
        }
    }
}
