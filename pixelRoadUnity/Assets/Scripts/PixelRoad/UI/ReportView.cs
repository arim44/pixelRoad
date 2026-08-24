using System;
using System.Collections;
using System.Collections.Generic;
using PixelRoad.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelRoad.UI
{
    /// <summary>
    /// AI 탐험 리포트 화면. 카드 세 장(나의 탐험 기록 / AI 탐험 분석 / 다음 탐험 추천)과 토스트로 이뤄진다.
    ///
    /// 와이어프레임의 다섯 상태를 <see cref="ReportScreenState"/> 하나로 정리했다. 카드 자체는 프리팹에
    /// 미리 만들어 두고 여기서는 켜고 끄기만 하므로, 상태가 바뀌어도 오브젝트를 새로 만들지 않는다.
    ///
    /// 도감 패널과 마찬가지로 캔버스 직속에 두어 지도 위를 덮는다.
    /// </summary>
    public sealed class ReportView : MonoBehaviour
    {
        /// <summary>리포트 화면이 가질 수 있는 상태.</summary>
        public enum ReportScreenState
        {
            /// <summary>해금이 하나도 없어 보여 줄 기록이 없다.</summary>
            Empty = 0,

            /// <summary>서버(또는 임시 데이터) 응답을 기다리는 중.</summary>
            Analyzing = 1,

            /// <summary>분석 결과를 다 받아 카드 세 장을 보여 준다.</summary>
            Completed = 2,

            /// <summary>분석에 실패해 재시도 안내를 보여 준다.</summary>
            Failed = 3,
        }

        private const string AnalyzingToastMessage = "✨ 탐험 리포트 분석중...";
        private const string UpdatedToastMessage = "✨ 탐험 리포트 업데이트 완료";
        private const string AnalyzingPlaceholder = "흠... 당신의 탐험 기록을 살펴보는 중이에요";

        [Header("Root")]
        [SerializeField] private GameObject root;

        /// <summary>카드 세 장을 담는 스크롤 영역. 상태가 바뀔 때 맨 위로 되돌린다.</summary>
        [SerializeField] private ScrollRect scroll;

        [Header("탐험 기록 없음")]
        [SerializeField] private GameObject emptyCard;

        /// <summary>`→ 지도에서 탐험하기`. 지도 탭으로 보낸다.</summary>
        [SerializeField] private Button exploreButton;

        [Header("카드1 · 나의 탐험 기록")]
        [SerializeField] private GameObject recordCard;

        /// <summary>`현재까지 12곳을 탐험했어요!`의 숫자 자리.</summary>
        [SerializeField] private TMP_Text visitedCountText;

        /// <summary>
        /// 카테고리별 해금 수. <see cref="SpotCategory.DisplayOrder"/>와 같은 순서로 다섯 개를 연결한다.
        /// 순서가 어긋나면 엉뚱한 칸에 숫자가 들어가므로 프리팹에서 순서를 지켜야 한다.
        /// </summary>
        [SerializeField] private TMP_Text[] categoryCountTexts = new TMP_Text[5];

        [Header("카드2 · AI 탐험 분석")]
        [SerializeField] private GameObject analysisCard;
        [SerializeField] private TMP_Text analysisText;

        /// <summary>`AI 분석 완료 ✓` 배지. 분석이 끝났을 때만 켠다.</summary>
        [SerializeField] private GameObject analysisDoneBadge;

        /// <summary>분석 실패 시에만 켜는 재시도 버튼.</summary>
        [SerializeField] private Button retryButton;

        [Header("카드3 · 다음 탐험 추천")]
        [SerializeField] private GameObject recommendCard;
        [SerializeField] private TMP_Text recommendNameText;
        [SerializeField] private TMP_Text recommendReasonText;

        /// <summary>`→ 지도에서 보기`. 추천 랜드마크로 지도를 옮긴다.</summary>
        [SerializeField] private Button recommendMapButton;

        [Header("토스트")]
        [SerializeField] private GameObject toast;
        [SerializeField] private TMP_Text toastText;

        /// <summary>자동 숨김 타이머. 토스트가 겹쳐 뜨면 이전 것을 멈추고 다시 건다.</summary>
        private Coroutine toastRoutine;

        /// <summary>
        /// 아직 걸지 못한 자동 숨김 시간(초). 0이면 없다.
        ///
        /// 분석은 리포트 화면을 닫아 둔 채로도 끝난다. 이 컴포넌트는 화면과 같은 오브젝트에 붙어 있어
        /// 닫혀 있는 동안에는 코루틴을 돌릴 수 없으므로, 완료 토스트를 띄워 두고 시간만 여기 적어 둔다.
        /// 화면을 열 때 그때부터 3초를 센다. 사용자가 못 본 토스트가 조용히 사라지는 일이 없다.
        /// </summary>
        private float pendingToastSeconds;

        /// <summary>마지막으로 받은 추천. `지도에서 보기`가 어떤 랜드마크를 가리키는지 여기서 읽는다.</summary>
        private int recommendedLandmarkId;

        /// <summary>`→ 지도에서 탐험하기`를 눌렀을 때.</summary>
        public event Action ExploreRequested;

        /// <summary>추천 랜드마크의 `→ 지도에서 보기`를 눌렀을 때. 인자는 랜드마크 ID.</summary>
        public event Action<int> RecommendationRequested;

        /// <summary>분석 실패 후 재시도를 눌렀을 때.</summary>
        public event Action RetryRequested;

        public bool IsVisible => root != null && root.activeSelf;

        /// <summary>직렬화 참조가 하나라도 비어 있으면 즉시 예외를 던져 프리팹 설정 실수를 잡는다.</summary>
        public void ValidateReferences()
        {
            Require(root, nameof(root));
            Require(scroll, nameof(scroll));
            Require(emptyCard, nameof(emptyCard));
            Require(exploreButton, nameof(exploreButton));
            Require(recordCard, nameof(recordCard));
            Require(visitedCountText, nameof(visitedCountText));
            Require(analysisCard, nameof(analysisCard));
            Require(analysisText, nameof(analysisText));
            Require(analysisDoneBadge, nameof(analysisDoneBadge));
            Require(retryButton, nameof(retryButton));
            Require(recommendCard, nameof(recommendCard));
            Require(recommendNameText, nameof(recommendNameText));
            Require(recommendReasonText, nameof(recommendReasonText));
            Require(recommendMapButton, nameof(recommendMapButton));
            Require(toast, nameof(toast));
            Require(toastText, nameof(toastText));

            if (categoryCountTexts == null || categoryCountTexts.Length != SpotCategory.DisplayOrder.Length)
            {
                throw new InvalidOperationException(
                    "ReportPanel의 categoryCountTexts 는 카테고리 "
                    + SpotCategory.DisplayOrder.Length + "종과 같은 개수여야 합니다.");
            }

            for (int i = 0; i < categoryCountTexts.Length; i++)
            {
                Require(categoryCountTexts[i], "categoryCountTexts[" + i + "]");
            }
        }

        /// <summary>참조가 비었으면 어느 필드인지 이름을 담아 예외를 던진다.</summary>
        private void Require(UnityEngine.Object reference, string fieldName)
        {
            if (reference == null)
            {
                throw new InvalidOperationException(
                    "ReportPanel is missing the serialized reference '" + fieldName + "'.");
            }
        }

        /// <summary>버튼 이벤트를 연결하고 닫힌 상태로 시작한다. 프리팹 인스턴스마다 한 번만 부른다.</summary>
        public void Initialize()
        {
            exploreButton.onClick.AddListener(() => ExploreRequested?.Invoke());
            retryButton.onClick.AddListener(() => RetryRequested?.Invoke());
            recommendMapButton.onClick.AddListener(
                () => RecommendationRequested?.Invoke(recommendedLandmarkId));

            toast.SetActive(false);
            root.SetActive(false);
        }

        /// <summary>리포트 화면을 여닫는다. 열 때는 스크롤을 맨 위로 되돌린다.</summary>
        public void SetVisible(bool visible)
        {
            if (!visible)
            {
                HideToast();
                root.SetActive(false);
                return;
            }

            // 스크롤 위치와 코루틴은 오브젝트가 켜진 뒤에야 제대로 먹는다. 활성화를 먼저 한다.
            root.transform.SetAsLastSibling();
            root.SetActive(true);
            ResetScroll();

            if (pendingToastSeconds > 0f && toast.activeSelf)
            {
                StartToastTimer(pendingToastSeconds);
            }
        }

        /// <summary>
        /// 카드1의 탐험 기록을 갱신한다. 카테고리 집계는 서버가 아니라 여기서 한다.
        /// 해금 상태가 바뀔 때만 불리므로 매 프레임 도는 경로가 아니다.
        /// </summary>
        public void SetExplorationSummary(int unlockedCount, IList<SpotRuntimeState> spots)
        {
            visitedCountText.SetText("{0}", unlockedCount);

            string[] order = SpotCategory.DisplayOrder;
            for (int i = 0; i < categoryCountTexts.Length; i++)
            {
                int count = CountUnlockedInCategory(spots, order[i]);
                categoryCountTexts[i].SetText("{0}", count);
            }
        }

        /// <summary>화면 상태를 바꾼다. 카드 표시와 토스트를 한꺼번에 맞춘다.</summary>
        public void SetState(ReportScreenState state, ReportResponse response, float toastAutoHideSeconds)
        {
            bool hasRecord = state != ReportScreenState.Empty;
            emptyCard.SetActive(!hasRecord);
            recordCard.SetActive(hasRecord);
            analysisCard.SetActive(hasRecord);

            switch (state)
            {
                case ReportScreenState.Empty:
                    recommendCard.SetActive(false);
                    retryButton.gameObject.SetActive(false);
                    analysisDoneBadge.SetActive(false);
                    HideToast();
                    break;

                case ReportScreenState.Analyzing:
                    analysisText.text = AnalyzingPlaceholder;
                    analysisDoneBadge.SetActive(false);
                    retryButton.gameObject.SetActive(false);
                    recommendCard.SetActive(false);
                    ShowToast(AnalyzingToastMessage, 0f);
                    break;

                case ReportScreenState.Completed:
                    ApplyCompleted(response);
                    HideToast();
                    break;

                case ReportScreenState.Failed:
                    analysisText.text = "AI 분석에 실패했어요.\n잠시 후 다시 시도해 주세요.";
                    analysisDoneBadge.SetActive(false);
                    retryButton.gameObject.SetActive(true);
                    recommendCard.SetActive(false);
                    HideToast();
                    break;
            }

            ResetScroll();

            // 갱신 완료 토스트는 분석이 끝난 뒤에만, 그리고 자동 숨김 시간이 있을 때만 띄운다.
            if (state == ReportScreenState.Completed && toastAutoHideSeconds > 0f)
            {
                ShowToast(UpdatedToastMessage, toastAutoHideSeconds);
            }
        }

        /// <summary>분석 결과를 카드2·카드3에 채운다.</summary>
        private void ApplyCompleted(ReportResponse response)
        {
            analysisText.text = response != null && response.IsUsable
                ? response.analysis
                : string.Empty;
            analysisDoneBadge.SetActive(true);
            retryButton.gameObject.SetActive(false);

            bool hasRecommendation = response != null && response.HasRecommendation;
            recommendCard.SetActive(hasRecommendation);
            if (!hasRecommendation)
            {
                recommendedLandmarkId = 0;
                return;
            }

            recommendedLandmarkId = response.recommendation.landmarkId;
            recommendNameText.text = response.recommendation.name;
            recommendReasonText.text = response.recommendation.reason;
        }

        /// <summary>토스트를 띄운다. <paramref name="autoHideSeconds"/>가 0 이하면 직접 끌 때까지 남는다.</summary>
        private void ShowToast(string message, float autoHideSeconds)
        {
            StopToastTimer();
            toastText.text = message;
            toast.SetActive(true);
            pendingToastSeconds = autoHideSeconds;

            if (autoHideSeconds > 0f)
            {
                StartToastTimer(autoHideSeconds);
            }
        }

        /// <summary>토스트를 즉시 감춘다.</summary>
        private void HideToast()
        {
            StopToastTimer();
            pendingToastSeconds = 0f;
            toast.SetActive(false);
        }

        /// <summary>
        /// 자동 숨김 타이머를 건다. 화면이 닫혀 있으면 코루틴을 시작할 수 없으므로,
        /// 시간만 남겨 두고 <see cref="SetVisible"/>에서 다시 시도한다.
        /// </summary>
        private void StartToastTimer(float seconds)
        {
            StopToastTimer();
            if (!isActiveAndEnabled)
            {
                return;
            }

            toastRoutine = StartCoroutine(HideToastAfter(seconds));
        }

        /// <summary>돌고 있는 자동 숨김 타이머를 멈춘다.</summary>
        private void StopToastTimer()
        {
            if (toastRoutine == null)
            {
                return;
            }

            StopCoroutine(toastRoutine);
            toastRoutine = null;
        }

        /// <summary>정해진 시간이 지나면 토스트를 끈다.</summary>
        private IEnumerator HideToastAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            toastRoutine = null;
            pendingToastSeconds = 0f;
            toast.SetActive(false);
        }

        /// <summary>스크롤을 맨 위로 되돌린다.</summary>
        private void ResetScroll()
        {
            if (scroll != null)
            {
                scroll.verticalNormalizedPosition = 1f;
            }
        }

        /// <summary>해당 카테고리에서 해금한 개수를 센다. 표기가 영문으로 남아 있어도 같은 칸에 합친다.</summary>
        private static int CountUnlockedInCategory(IList<SpotRuntimeState> spots, string displayCategory)
        {
            if (spots == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < spots.Count; i++)
            {
                SpotRuntimeState state = spots[i];
                if (state.IsUnlocked
                    && SpotCategory.Normalize(state.Definition.Category) == displayCategory)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
