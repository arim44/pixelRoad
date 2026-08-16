using UnityEngine;

namespace PixelRoad.Data
{
    /// <summary>
    /// AI 탐험 리포트를 마지막으로 요청했을 때의 해금 개수를 저장한다.
    ///
    /// 저장 대상이 정수 하나뿐이라 방문 기록(JSON 파일)과 달리 PlayerPrefs를 쓴다.
    /// 리포트 요청 기능은 아직 없으므로 현재는 항상 0이며,
    /// 백엔드 연동이 붙는 시점에 요청 성공 후 <see cref="SetLastReportedCount"/>를 호출하면 된다.
    /// </summary>
    public static class ReportStateStore
    {
        private const string LastReportedCountKey = "PixelRoad.LastReportedLandmarkCount";

        /// <summary>마지막으로 리포트를 요청했을 때 보낸 해금 랜드마크 개수.</summary>
        public static int LastReportedCount
        {
            get { return PlayerPrefs.GetInt(LastReportedCountKey, 0); }
        }

        /// <summary>리포트 요청에 성공했을 때 기준 개수를 갱신한다. 음수는 0으로 막는다.</summary>
        public static void SetLastReportedCount(int value)
        {
            PlayerPrefs.SetInt(LastReportedCountKey, Mathf.Max(0, value));
            PlayerPrefs.Save();
        }

        /// <summary>리포트 탭을 쓸 수 있는지. 해금이 하나도 없으면 비활성이다.</summary>
        public static bool IsReportAvailable(int unlockedCount)
        {
            return unlockedCount > 0;
        }

        /// <summary>
        /// 마지막 요청 이후 해금 개수가 달라졌는지. 알림 느낌표 표시 조건이다.
        /// </summary>
        public static bool HasPendingUpdate(int unlockedCount)
        {
            return unlockedCount > 0 && unlockedCount != LastReportedCount;
        }
    }
}
