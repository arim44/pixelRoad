using UnityEngine;

namespace PixelRoad.Data
{
    /// <summary>
    /// AI 탐험 리포트의 로컬 상태.
    ///
    /// 세 가지를 기억한다.
    /// - 마지막으로 <b>요청을 보낸</b> 해금 개수: 같은 개수면 서버를 다시 부르지 않는다.
    /// - 마지막으로 받은 <b>응답 원문</b>: 요청을 건너뛸 때 이 값을 그대로 보여 준다.
    /// - <b>안 읽은 갱신</b> 여부: GNB 알림 뱃지를 켜는 조건이고, 리포트 화면을 열면 꺼진다.
    ///
    /// 저장할 게 정수 둘과 짧은 JSON 하나뿐이라 파일 대신 PlayerPrefs를 쓴다.
    /// 방문 기록 자체는 <see cref="VisitRepository"/>가 파일로 들고 있다.
    /// </summary>
    public static class ReportStateStore
    {
        private const string LastReportedCountKey = "PixelRoad.LastReportedLandmarkCount";
        private const string UnreadUpdateKey = "PixelRoad.ReportUnreadUpdate";
        private const string CachedReportKey = "PixelRoad.ReportCache";

        /// <summary>마지막으로 서버에 요청을 보냈을 때의 해금 개수. 요청한 적이 없으면 0.</summary>
        public static int LastReportedCount
        {
            get { return PlayerPrefs.GetInt(LastReportedCountKey, 0); }
        }

        /// <summary>분석이 갱신됐는데 아직 리포트 화면을 열지 않았는지. GNB 뱃지 조건이다.</summary>
        public static bool HasUnreadUpdate
        {
            get { return PlayerPrefs.GetInt(UnreadUpdateKey, 0) != 0; }
        }

        /// <summary>
        /// 지금 서버에 요청해야 하는지.
        /// 해금이 하나도 없으면 보낼 게 없고, 지난번과 개수가 같으면 결과도 같다고 보고 캐시를 쓴다.
        /// </summary>
        public static bool NeedsRequest(int unlockedCount)
        {
            return unlockedCount > 0 && unlockedCount != LastReportedCount;
        }

        /// <summary>캐시해 둔 응답을 읽는다. 없거나 깨졌으면 null.</summary>
        public static ReportResponse LoadCachedReport()
        {
            string json = PlayerPrefs.GetString(CachedReportKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                ReportResponse cached = JsonUtility.FromJson<ReportResponse>(json);
                return cached != null && cached.IsUsable ? cached : null;
            }
            catch (System.Exception exception)
            {
                // 캐시가 깨졌다고 앱이 멈출 이유는 없다. 비우고 새로 받게 둔다.
                Debug.LogWarning("[PixelRoad] 리포트 캐시를 읽지 못했습니다: " + exception.Message);
                ClearCachedReport();
                return null;
            }
        }

        /// <summary>
        /// 새로 받은 응답을 저장한다. 다음부터는 해금 개수가 늘기 전까지 이 값을 그대로 보여 준다.
        /// 첫 분석이 아니면 안 읽은 갱신으로 표시해 GNB 뱃지를 켠다.
        /// </summary>
        public static void SaveReport(int unlockedCount, ReportResponse response)
        {
            if (response == null || !response.IsUsable)
            {
                return;
            }

            bool isFirstReport = LastReportedCount <= 0;
            PlayerPrefs.SetString(CachedReportKey, JsonUtility.ToJson(response));
            PlayerPrefs.SetInt(LastReportedCountKey, Mathf.Max(0, unlockedCount));
            PlayerPrefs.SetInt(UnreadUpdateKey, isFirstReport ? 0 : 1);
            PlayerPrefs.Save();
        }

        /// <summary>리포트 화면을 열었을 때. 알림 뱃지를 끈다.</summary>
        public static void MarkUpdateSeen()
        {
            if (!HasUnreadUpdate)
            {
                return;
            }

            PlayerPrefs.SetInt(UnreadUpdateKey, 0);
            PlayerPrefs.Save();
        }

        /// <summary>캐시를 비운다. 다음 갱신 때 반드시 서버를 다시 부르게 된다.</summary>
        public static void ClearCachedReport()
        {
            PlayerPrefs.DeleteKey(CachedReportKey);
            PlayerPrefs.SetInt(LastReportedCountKey, 0);
            PlayerPrefs.SetInt(UnreadUpdateKey, 0);
            PlayerPrefs.Save();
        }
    }
}
