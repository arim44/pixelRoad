using System.Collections;
using PixelRoad.UI;
using TMPro;
using UnityEngine;

namespace PixelRoad.AR
{
    /// <summary>
    /// MapScene -> ARScene 전환 중 표시되는 로딩 화면.
    /// 부팅 때 쓰는 LoadingUIRoot.prefab을 그대로 재사용해 로고·진행바·퍼센트 텍스트까지
    /// 부팅 로딩 화면과 똑같은 모습으로 보여준다. Canvas는 sortingOrder를 높여 지도 UI 위에 항상 그려진다.
    ///
    /// DontDestroyOnLoad로 씬 전환을 넘어 살아남고, ARScene이 자기 UI를 다 세운 뒤(ARSceneController.Start)
    /// 페이드아웃시켜 없앤다. MapScene에 있는 동안 미리 페이드아웃하면 씬이 바뀌기 전에 지도 화면이
    /// 잠깐 다시 드러나 깜빡이는 것처럼 보이는 문제가 있었다.
    /// </summary>
    public sealed class LoadingScreenView
    {
        private const string PrefabResourcePath = "PixelRoad/UI/LoadingUIRoot";
        private const int SortingOrder = 1000;
        private const float FadeInSeconds = 0.25f;

        // 부팅 로딩 화면(LoadingSceneController)과 같은 방식으로 실제 진행률이 아니라 표시 진행률을
        // 서서히 따라가게 한다 - AR 씬은 작아서 실제 progress가 한두 프레임 만에 100%로 뛰어버리는데,
        // 그대로 쓰면 진행바가 애니메이션 없이 항상 100%로만 보인다. 부팅보다 빠르게 따라가게 잡아서
        // AR 전환이 불필요하게 오래 걸리는 것처럼 느껴지지 않게 한다.
        private const float ProgressFollowSpeed = 8f;

        private readonly LoadingUiBindings bindings;
        private readonly FadeRunner fadeRunner;
        private float displayedProgress;
        private int displayedPercent = -1;

        private LoadingScreenView(LoadingUiBindings bindings, FadeRunner fadeRunner)
        {
            this.bindings = bindings;
            this.fadeRunner = fadeRunner;
        }

        public static LoadingScreenView Create()
        {
            GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
            if (prefab == null)
            {
                Debug.LogError("[PixelRoad] 로딩 화면 프리팹을 찾지 못했습니다: " + PrefabResourcePath);
                return null;
            }

            GameObject instance = Object.Instantiate(prefab);
            instance.name = "ARLoadingUIRoot";
            Object.DontDestroyOnLoad(instance);

            LoadingUiBindings bindings = instance.GetComponent<LoadingUiBindings>();
            bindings.ValidateReferences();
            bindings.Canvas.sortingOrder = SortingOrder;
            bindings.CanvasGroup.alpha = 0f;

            FadeRunner fadeRunner = instance.AddComponent<FadeRunner>();
            fadeRunner.FadeIn(bindings.CanvasGroup, FadeInSeconds);

            LoadingScreenView view = new LoadingScreenView(bindings, fadeRunner);
            view.SetProgress(0f);
            return view;
        }

        // 프레임당 반영폭 상한 - 씬 로드 전후로 프레임이 튀면 한 스텝에 목표까지 뛰어버려 애니메이션
        // 없이 값이 바뀐 것처럼 보일 수 있어, FadeRunner와 같은 이유로 상한을 둔다.
        private const float MaxProgressStepSeconds = 0.05f;

        /// <summary>목표 진행률(0~1)을 준다. 실제로 보여주는 진행률은 이 목표를 향해 서서히 따라간다.</summary>
        public void SetProgress(float targetProgress01)
        {
            float target = Mathf.Clamp01(targetProgress01);
            float step = ProgressFollowSpeed * Mathf.Min(Time.unscaledDeltaTime, MaxProgressStepSeconds);
            displayedProgress = Mathf.MoveTowards(displayedProgress, target, step);
            bindings.ProgressFill.fillAmount = displayedProgress;

            // 문자열 생성은 정수 퍼센트가 바뀔 때만 한다.
            int percent = Mathf.RoundToInt(displayedProgress * 100f);
            if (percent == displayedPercent)
            {
                return;
            }

            displayedPercent = percent;
            TMP_Text percentText = bindings.PercentText;
            percentText.SetText("{0:0}%", percent);
        }

        /// <summary>새 씬이 이미 화면에 드러난 뒤 호출한다 - durationSeconds에 걸쳐 서서히 투명해지다 스스로 없어진다.</summary>
        public void FadeOutAndDestroy(float durationSeconds)
        {
            fadeRunner.FadeOut(bindings.CanvasGroup, durationSeconds);
        }

        /// <summary>
        /// LoadingScreenView는 평범한 C# 클래스라 코루틴을 직접 돌릴 수 없어, DontDestroyOnLoad된
        /// 로딩 오브젝트에 붙는 이 작은 MonoBehaviour가 대신 페이드 인/아웃 코루틴을 돌린다.
        /// </summary>
        private sealed class FadeRunner : MonoBehaviour
        {
            private Coroutine activeFade;

            public void FadeIn(CanvasGroup canvasGroup, float durationSeconds)
            {
                StartFade(Fade(canvasGroup, 0f, 1f, durationSeconds, destroyAfter: false));
            }

            public void FadeOut(CanvasGroup canvasGroup, float durationSeconds)
            {
                StartFade(Fade(canvasGroup, canvasGroup.alpha, 0f, durationSeconds, destroyAfter: true));
            }

            /// <summary>씬 전환이 빠르면 페이드아웃이 페이드인 도중 시작될 수 있다 - 진행 중이던 걸 멈추고 새로 시작해 둘이 다투지 않게 한다.</summary>
            private void StartFade(IEnumerator routine)
            {
                if (activeFade != null)
                {
                    StopCoroutine(activeFade);
                }

                activeFade = StartCoroutine(routine);
            }

            /// <summary>
            /// 프레임당 반영폭 상한. 페이드아웃은 씬 전환 직후(무거운 프레임) 시작되는데, 그 프레임의
            /// deltaTime을 그대로 쓰면 한 스텝만에 목표 시간을 넘어 애니메이션이 안 보이는 것처럼 끝나
            /// 버린다. 상한을 둬서 최소 몇 프레임에 걸쳐서는 항상 서서히 변하게 한다.
            /// </summary>
            private const float MaxStepSeconds = 0.05f;

            private IEnumerator Fade(CanvasGroup canvasGroup, float from, float to, float durationSeconds, bool destroyAfter)
            {
                float elapsed = 0f;
                while (elapsed < durationSeconds)
                {
                    elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxStepSeconds);
                    canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / durationSeconds));
                    yield return null;
                }

                canvasGroup.alpha = to;
                if (destroyAfter)
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
