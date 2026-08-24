using System.Collections;
using PixelRoad.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PixelRoad.Runtime
{
    /// <summary>
    /// 로딩 씬의 진입점.
    ///
    /// 흐름은 두 단계다.
    /// 1) 지도 씬을 비동기로 로드한다(진행률 0 ~ 60%).
    /// 2) 씬을 활성화한 뒤 지도 첫 타일이 그려질 때까지 로딩 화면을 유지한다(60 ~ 100%).
    ///
    /// 2단계를 위해 로딩 오버레이는 DontDestroyOnLoad로 씬 전환을 넘어 살아남고,
    /// 지도 준비 완료는 <see cref="AppReadySignal"/>로만 전달받는다(씬 간 직접 참조 없음).
    /// </summary>
    public sealed class LoadingSceneController : MonoBehaviour
    {
        /// <summary>씬 로드 단계가 차지하는 진행률 구간.</summary>
        private const float SceneLoadProgressShare = 0.6f;

        /// <summary>
        /// 로딩 씬에 배치된 LoadingUIRoot 프리팹 인스턴스의 참조.
        /// 이 컨트롤러의 자식으로 두어야 <see cref="Object.DontDestroyOnLoad"/>로 함께 살아남는다.
        /// </summary>
        [SerializeField]
        private LoadingUiBindings ui;

        [SerializeField]
        private string mapSceneName = "MapScene";

        /// <summary>지도 첫 타일을 기다리는 최대 시간(초). 초과하면 그냥 로딩을 닫는다.</summary>
        [SerializeField]
        private float mapReadyTimeoutSeconds = 20f;

        /// <summary>진행률 바가 목표치를 따라가는 속도(초당 비율).</summary>
        [SerializeField]
        private float progressFollowSpeed = 1.5f;

        [SerializeField]
        private float fadeOutSeconds = 0.25f;

        private float displayedProgress;
        private int displayedPercent = -1;

        /// <summary>지도 씬 로드와 지도 준비 대기를 순서대로 진행하고, 끝나면 로딩 화면을 걷어낸다.</summary>
        private IEnumerator Start()
        {
            Application.targetFrameRate = 60;
            DontDestroyOnLoad(gameObject);

            if (ui == null)
            {
                // 로딩 오버레이 없이도 앱은 떠야 하므로 지도 씬으로 바로 넘긴다.
                Debug.LogError(
                    "[PixelRoad] LoadingSceneController.ui 가 비어 있습니다. "
                    + "Loading 씬의 LoadingBoot 아래에 LoadingUIRoot 프리팹을 배치하고 참조를 연결하세요. "
                    + "(Tools > Pixel Road > Rebuild Loading Scene)");
                SceneManager.LoadScene(mapSceneName);
                yield break;
            }

            ui.ValidateReferences();
            SetProgressImmediate(0f);

            AsyncOperation operation = SceneManager.LoadSceneAsync(mapSceneName);
            if (operation == null)
            {
                Debug.LogError("[PixelRoad] 지도 씬을 찾을 수 없습니다: " + mapSceneName
                    + ". Build Settings에 등록되어 있는지 확인하세요.");
                yield break;
            }

            operation.allowSceneActivation = false;

            // 1단계: 씬 로드. Unity는 활성화 대기 중 progress를 0.9에서 멈춘다.
            while (operation.progress < 0.9f)
            {
                StepProgress(operation.progress / 0.9f * SceneLoadProgressShare);
                yield return null;
            }

            StepProgress(SceneLoadProgressShare);
            yield return null;
            operation.allowSceneActivation = true;
            while (!operation.isDone)
            {
                yield return null;
            }

            // 2단계: 지도 첫 타일 대기. 지도를 쓸 수 없는 구성이면 즉시 준비 완료로 들어온다.
            float waitedSeconds = 0f;
            while (!AppReadySignal.IsMapReady && waitedSeconds < mapReadyTimeoutSeconds)
            {
                waitedSeconds += Time.unscaledDeltaTime;
                float waitRatio = mapReadyTimeoutSeconds > 0f ? waitedSeconds / mapReadyTimeoutSeconds : 1f;
                // 대기 구간은 90%까지만 차오르게 해서 완료 전에 100%가 되지 않도록 한다.
                StepProgress(Mathf.Lerp(SceneLoadProgressShare, 0.9f, waitRatio));
                yield return null;
            }

            if (!AppReadySignal.IsMapReady)
            {
                Debug.LogWarning("[PixelRoad] 지도 준비 신호를 " + mapReadyTimeoutSeconds
                    + "초 안에 받지 못해 로딩 화면을 닫습니다.");
            }

            while (displayedProgress < 1f)
            {
                StepProgress(1f);
                yield return null;
            }

            yield return FadeOut();
            Destroy(gameObject);
        }

        /// <summary>표시 진행률을 목표치 쪽으로 한 프레임만큼 진행시킨다.</summary>
        private void StepProgress(float target)
        {
            displayedProgress = Mathf.MoveTowards(
                displayedProgress,
                Mathf.Clamp01(target),
                progressFollowSpeed * Time.unscaledDeltaTime);
            ApplyProgress();
        }

        /// <summary>표시 진행률을 보간 없이 즉시 맞춘다. 시작 시 0%를 그릴 때 쓴다.</summary>
        private void SetProgressImmediate(float value)
        {
            displayedProgress = Mathf.Clamp01(value);
            ApplyProgress();
        }

        /// <summary>현재 진행률을 진행 바와 퍼센트 문구에 반영한다.</summary>
        private void ApplyProgress()
        {
            ui.ProgressFill.fillAmount = displayedProgress;

            // 문자열 생성은 정수 퍼센트가 바뀔 때만. 매 프레임 포맷하면 GC가 는다.
            int percent = Mathf.RoundToInt(displayedProgress * 100f);
            if (percent == displayedPercent)
            {
                return;
            }

            displayedPercent = percent;
            ui.PercentText.SetText("{0:0}%", percent);
        }

        /// <summary>로딩 오버레이를 서서히 투명하게 만든다. 지도로 넘어가는 전환을 부드럽게 하려는 것이다.</summary>
        private IEnumerator FadeOut()
        {
            if (fadeOutSeconds <= 0f)
            {
                yield break;
            }

            CanvasGroup group = ui.CanvasGroup;
            float elapsed = 0f;
            while (elapsed < fadeOutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = 1f - Mathf.Clamp01(elapsed / fadeOutSeconds);
                yield return null;
            }

            group.alpha = 0f;
        }
    }
}
