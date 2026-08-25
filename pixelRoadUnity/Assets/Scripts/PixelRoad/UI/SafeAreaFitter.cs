using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PixelRoad.UI
{
    /// <summary>
    /// 이 RectTransform의 anchor를 Screen.safeArea에 맞춰 계속 갱신한다. 노치·펀치홀·홈 인디케이터처럼
    /// 화면에서 잘려나가거나 가려지는 영역을 피해 실제 UI 콘텐츠를 배치하려고 쓴다.
    ///
    /// Canvas 바로 아래 이 컴포넌트가 붙은 "SafeArea" RectTransform 하나를 두고, 기존 UI 전체를
    /// 그 자식으로 옮기는 방식으로 적용한다(EventSystem처럼 화면에 그려지지 않는 것은 그대로 둔다).
    ///
    /// 에디터 Game 뷰의 Screen.safeArea는 보통 노치가 없어 화면 전체 그대로 나온다. 실기기 느낌을
    /// 미리 보려고 Space를 누르면, 실제 값 대신 평균적인 실기기 노치/홈 인디케이터 비율을 흉내 낸
    /// 값을 적용하고 그 경계를 debugOutline으로 표시한다. 다시 누르면 원래 Screen.safeArea로 되돌아간다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        [SerializeField] private GameObject debugOutline;

        // 실기기 평균치 근사값(화면 세로 길이 대비 비율). 위쪽은 노치/다이나믹 아일랜드+상태바,
        // 아래쪽은 홈 인디케이터/제스처 바 몫이다. 좌우는 포트레이트에서는 보통 0이라 넣지 않는다.
        private const float SimulatedTopInsetRatio = 0.055f;
        private const float SimulatedBottomInsetRatio = 0.035f;

        private RectTransform rectTransform;
        private Rect lastSafeArea;
        private int lastScreenWidth;
        private int lastScreenHeight;
        private ScreenOrientation lastOrientation;
        private bool simulateDeviceSafeArea;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            Apply();
        }

        private void Update()
        {
            bool spacePressed = WasSpacePressedThisFrame();
            if (spacePressed)
            {
                simulateDeviceSafeArea = !simulateDeviceSafeArea;
                if (debugOutline != null)
                {
                    debugOutline.SetActive(simulateDeviceSafeArea);
                }
            }

            bool screenChanged = Screen.width != lastScreenWidth
                || Screen.height != lastScreenHeight
                || Screen.orientation != lastOrientation;
            bool realSafeAreaChanged = !simulateDeviceSafeArea && Screen.safeArea != lastSafeArea;

            if (spacePressed || screenChanged || realSafeAreaChanged)
            {
                Apply();
            }
        }

        private static bool WasSpacePressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Space);
#endif
        }

        private void Apply()
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            lastOrientation = Screen.orientation;

            if (lastScreenWidth <= 0 || lastScreenHeight <= 0)
            {
                return;
            }

            Rect safeArea = simulateDeviceSafeArea
                ? SimulatedSafeArea(lastScreenWidth, lastScreenHeight)
                : Screen.safeArea;
            lastSafeArea = safeArea;

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= lastScreenWidth;
            anchorMin.y /= lastScreenHeight;
            anchorMax.x /= lastScreenWidth;
            anchorMax.y /= lastScreenHeight;

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        /// <summary>실기기 평균치를 흉내 낸 safeArea. 위는 노치/상태바만큼, 아래는 홈 인디케이터만큼 뺀다.</summary>
        private static Rect SimulatedSafeArea(int screenWidth, int screenHeight)
        {
            float topInset = screenHeight * SimulatedTopInsetRatio;
            float bottomInset = screenHeight * SimulatedBottomInsetRatio;
            return new Rect(0f, bottomInset, screenWidth, screenHeight - topInset - bottomInset);
        }
    }
}
