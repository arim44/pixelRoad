using System;
using System.Collections.Generic;
using PixelRoad.Data;
using PixelRoad.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelRoad.AR
{
    /// <summary>
    /// ARScene의 오버레이 UI 갱신: 랜드마크별 아이콘/거리 라벨, 화면 밖 방향 화살표, 상태 메시지, 뒤로가기.
    /// UI 구조 자체는 AROverlayUIRoot.prefab(및 랜드마크 핀은 ARLandmarkPinView.prefab)이 갖고 있고,
    /// 이 클래스는 그 참조(AROverlayUiBindings)를 받아 값만 갱신한다 - MapScene의 PixelRoadRuntimeView와 같은 패턴이다.
    /// </summary>
    public sealed class AROverlayView
    {
        private const string NoLandmarksMessage = "근처에 랜드마크가 없습니다.\n잠시 후 지도로 돌아갑니다.";
        private const float BlinkFrequencyHz = 2f;
        private const float MinBlinkAlpha = 0.25f;

        private static readonly Color32 UnlockedIconTint = Color.white;
        private static readonly Color32 LockedIconTint = new Color32(124, 120, 112, 235);
        private static readonly Color32 TextColor = new Color32(246, 237, 217, 255);
        private static readonly Color32 FallbackIconColor = new Color32(208, 56, 48, 255);

        private readonly AROverlayUiBindings uiBindings;
        private readonly ARConfig config;
        private readonly SpotIconLibrary iconLibrary;
        private readonly Dictionary<string, LandmarkBinding> bindings = new Dictionary<string, LandmarkBinding>();
        private readonly Sprite arrowSprite;

        /// <summary>촬영 버튼이 눌렸을 때 발생한다. 실제 캡처·저장은 코루틴이 필요해 ARSceneController가 처리한다.</summary>
        public event Action CaptureRequested;

        /// <summary>썸네일을 눌렀을 때 발생한다. 갤러리 앱을 여는 건 플랫폼 API가 필요해 ARSceneController가 처리한다.</summary>
        public event Action ThumbnailClicked;

        /// <summary>랜드마크 핀(아이콘)을 눌렀을 때 그 랜드마크의 id와 함께 발생한다.</summary>
        public event Action<string> LandmarkClicked;

        /// <summary>뒤로가기 버튼을 눌렀을 때 발생한다. 씬 전환은 로딩 화면이 필요해 ARSceneController가 처리한다.</summary>
        public event Action BackRequested;

        public AROverlayView(AROverlayUiBindings uiBindings, ARConfig config)
        {
            this.uiBindings = uiBindings;
            this.config = config;
            uiBindings.ValidateReferences();

            // AR은 도감 썸네일을 그리지 않으므로 대체 이미지 이름은 비워 둔다.
            iconLibrary = new SpotIconLibrary(config.spotIconResourceFolder, config.defaultSpotIconName, string.Empty);

            // 프레임 테두리 화살표(엣지)와 나침반 화살표(집중 모드)는 랜드마크마다 다른 아이콘과 달리
            // 데이터 없이 고정된 픽셀아트라, 프리팹에 굽는 대신 지금처럼 코드로 계속 생성해 붙인다.
            arrowSprite = ARUiFactory.CreateTriangleSprite(config.edgeArrowPixelSize, TextColor);

            uiBindings.CaptureButtonImage.sprite = ARUiFactory.CreateCircleSprite(
                (int)uiBindings.CaptureButtonImage.rectTransform.sizeDelta.x, Color.white);
            uiBindings.FocusDirectionArrow.sprite = arrowSprite;

            uiBindings.BackButton.onClick.AddListener(() => BackRequested?.Invoke());
            uiBindings.CaptureButton.onClick.AddListener(() => CaptureRequested?.Invoke());
            uiBindings.ThumbnailButton.onClick.AddListener(() => ThumbnailClicked?.Invoke());
            uiBindings.UnlockDialog.Initialize();

            uiBindings.StatusText.gameObject.SetActive(false);
            uiBindings.ToastText.gameObject.SetActive(false);
            uiBindings.ThumbnailFrame.SetActive(false);
            uiBindings.FocusDirectionArrow.gameObject.SetActive(false);
        }

        /// <summary>
        /// 랜드마크를 처음 해금했을 때 MapScene과 같은 `랜드마크 발견!` 창을 띄운다.
        /// UnlockDialogView가 큐를 들고 있어 연속 해금도 알아서 하나씩 이어 보여준다.
        /// </summary>
        public void ShowUnlockDialog(SpotDefinition definition)
        {
            uiBindings.UnlockDialog.Enqueue(definition);
        }

        /// <summary>화면 위쪽에 짧은 안내 문구를 잠깐 보여준다(예: 갤러리 열기 실패). 자동으로 사라지지는 않고, 다시 호출하면 내용만 갱신된다.</summary>
        public void ShowToast(string message)
        {
            uiBindings.ToastText.text = message;
            uiBindings.ToastText.gameObject.SetActive(true);
        }

        public void HideToast()
        {
            uiBindings.ToastText.gameObject.SetActive(false);
        }

        /// <summary>
        /// 캡처 순간에만 촬영 버튼·뒤로가기 버튼·이전 썸네일을 화면에서 감춰서, 찍힌 사진에 UI가 함께 찍히지 않게 한다.
        /// 썸네일은 감추기만 하고 다시 켜지는 않는다 - 캡처 직후 ShowCapturedThumbnail이 새 사진으로 바로 다시 띄우기 때문이다.
        /// </summary>
        public void SetCaptureUiVisible(bool visible)
        {
            uiBindings.CaptureButton.gameObject.SetActive(visible);
            uiBindings.BackButton.gameObject.SetActive(visible);
            if (!visible)
            {
                uiBindings.ThumbnailFrame.SetActive(false);
            }
        }

        /// <summary>방금 찍은 사진을 화면 오른쪽 아래에 작게 띄운다. 스프라이트 소유권은 호출측이 계속 갖고, 다 쓰면 텍스처를 직접 정리해야 한다.</summary>
        public void ShowCapturedThumbnail(Sprite sprite)
        {
            uiBindings.ThumbnailImage.sprite = sprite;
            uiBindings.ThumbnailFrame.SetActive(true);
        }

        /// <summary>썸네일을 감춘다 - 원본 사진이 갤러리에서 삭제된 것으로 확인됐을 때 등에 쓴다.</summary>
        public void HideCapturedThumbnail()
        {
            uiBindings.ThumbnailImage.sprite = null;
            uiBindings.ThumbnailFrame.SetActive(false);
        }

        /// <summary>ARCore 미지원 등 랜드마크 갱신을 멈추고 안내만 보여줘야 할 때 쓴다. null/빈 문자열이면 숨긴다.</summary>
        public void SetStatusMessage(string message)
        {
            bool visible = !string.IsNullOrEmpty(message);
            if (visible)
            {
                uiBindings.StatusText.text = message;
                uiBindings.StatusText.color = TextColor;
            }

            uiBindings.StatusText.gameObject.SetActive(visible);
        }

        /// <summary>
        /// 반경 내 랜드마크가 하나도 없을 때 화면 중앙에 깜빡이는 경고 문구를 표시한다.
        /// elapsedSeconds는 경고가 뜬 뒤 흐른 시간(초)으로, 깜빡임 위상을 계산하는 데만 쓴다.
        /// </summary>
        public void SetNoLandmarksWarning(bool visible, float elapsedSeconds)
        {
            if (!visible)
            {
                uiBindings.StatusText.gameObject.SetActive(false);
                return;
            }

            uiBindings.StatusText.text = NoLandmarksMessage;
            uiBindings.StatusText.gameObject.SetActive(true);
            float phase = elapsedSeconds * BlinkFrequencyHz * Mathf.PI * 2f;
            float alpha = MinBlinkAlpha + (1f - MinBlinkAlpha) * (0.5f + 0.5f * Mathf.Sin(phase));
            Color color = TextColor;
            color.a = alpha;
            uiBindings.StatusText.color = color;
        }

        /// <summary>anchoredY는 카메라를 위/아래로 기울인 정도(피치)에 따라 계산된 세로 위치다.</summary>
        public void ShowOnScreen(ARLandmarkSnapshot landmark, float anchoredX, float anchoredY, double distanceMeters)
        {
            LandmarkBinding binding = GetOrCreateBinding(landmark);
            binding.Root.gameObject.SetActive(true);
            binding.Icon.sprite = binding.NormalSprite;
            binding.Icon.rectTransform.sizeDelta = new Vector2(config.iconPixelSize, config.iconPixelSize);
            binding.Icon.rectTransform.localEulerAngles = Vector3.zero;
            binding.Root.anchoredPosition = new Vector2(anchoredX, anchoredY);
            binding.DistanceLabel.text = FormatDistance(distanceMeters);
        }

        /// <summary>
        /// 화면 가장자리 방향 화살표를 표시한다. baseAnchoredY는 카메라 피치에 따른 기본 세로 위치이고,
        /// sameSideSlotIndex는 이번 프레임에 같은 쪽(좌/우)에 표시되는 화살표들 사이에서 이 랜드마크의
        /// 순번(0부터)이다 - baseAnchoredY를 기준으로 위/아래로 떨어뜨려 쌓아 여러 개가 겹치지 않게 한다.
        /// </summary>
        public void ShowAtEdge(ARLandmarkSnapshot landmark, bool rightSide, float baseAnchoredY, double distanceMeters, int sameSideSlotIndex)
        {
            LandmarkBinding binding = GetOrCreateBinding(landmark);
            binding.Root.gameObject.SetActive(true);
            binding.Icon.sprite = arrowSprite;
            binding.Icon.rectTransform.sizeDelta = new Vector2(config.edgeArrowPixelSize, config.edgeArrowPixelSize);
            binding.Icon.rectTransform.localEulerAngles = new Vector3(0f, 0f, rightSide ? -90f : 90f);

            float halfWidth = uiBindings.LandmarkRoot.rect.width * 0.5f;
            float x = Mathf.Max(0f, halfWidth - config.edgeMarginPixels);
            float y = baseAnchoredY + StackedOffsetY(sameSideSlotIndex, config.edgeStackSpacingPixels);
            binding.Root.anchoredPosition = new Vector2(rightSide ? x : -x, y);
            binding.DistanceLabel.text = FormatDistance(distanceMeters);
        }

        /// <summary>
        /// 슬롯 0은 중앙(0), 이후로는 중앙 기준 위/아래로 번갈아 벌어진다: 1=-간격, 2=+간격, 3=-2*간격, 4=+2*간격 ...
        /// </summary>
        private static float StackedOffsetY(int slotIndex, float spacing)
        {
            if (slotIndex <= 0)
            {
                return 0f;
            }

            int magnitude = (slotIndex + 1) / 2;
            float sign = (slotIndex % 2 == 1) ? -1f : 1f;
            return sign * magnitude * spacing;
        }

        public void Hide(string landmarkId)
        {
            if (bindings.TryGetValue(landmarkId, out LandmarkBinding binding))
            {
                binding.Root.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 방금 해금된 랜드마크의 핀 색을 해금 틴트로 바꾼다. 핀이 아직 만들어지지 않았으면(화면에 한 번도
        /// 안 보였으면) 조용히 무시한다 - 다음에 ShowOnScreen/ShowAtEdge로 처음 만들어질 때 landmark.IsUnlocked
        /// 스냅샷 값으로 이미 해금 틴트가 적용된다.
        /// </summary>
        public void SetLandmarkUnlocked(string landmarkId)
        {
            if (bindings.TryGetValue(landmarkId, out LandmarkBinding binding))
            {
                binding.Icon.color = UnlockedIconTint;
            }
        }

        /// <summary>
        /// 집중 모드일 때 화면 중앙보다 살짝 아래에서 선택된 랜드마크 방향을 가리키는 나침반 화살표를 보여준다.
        /// deltaDegrees는 카메라 정면 기준 랜드마크 방위각(오른쪽이 양수)으로, 화면 안/밖 여부와 상관없이
        /// 항상 실제 각도로 회전해 자연스럽게 그 방향을 가리킨다.
        /// </summary>
        public void ShowFocusDirection(float deltaDegrees)
        {
            uiBindings.FocusDirectionArrow.gameObject.SetActive(true);
            uiBindings.FocusDirectionArrow.rectTransform.localEulerAngles = new Vector3(0f, 0f, -deltaDegrees);
        }

        public void HideFocusDirection()
        {
            uiBindings.FocusDirectionArrow.gameObject.SetActive(false);
        }

        /// <summary>랜드마크 핀 프리팹을 Instantiate해서 바인딩을 만든다. id별로 한 번만 만들고 이후에는 재사용한다.</summary>
        private LandmarkBinding GetOrCreateBinding(ARLandmarkSnapshot landmark)
        {
            if (bindings.TryGetValue(landmark.Id, out LandmarkBinding existing))
            {
                return existing;
            }

            ARLandmarkPinView pin = UnityEngine.Object.Instantiate(uiBindings.LandmarkPinPrefab, uiBindings.LandmarkRoot);
            pin.name = "Landmark_" + landmark.Id;

            Sprite normalSprite = ResolveIconSprite(landmark);
            pin.Icon.sprite = normalSprite;
            pin.Icon.color = landmark.IsUnlocked ? (Color)UnlockedIconTint : (Color)LockedIconTint;

            string landmarkId = landmark.Id;
            pin.Button.onClick.AddListener(() => LandmarkClicked?.Invoke(landmarkId));

            // 거리 라벨 위치는 아이콘 크기(iconPixelSize) 기준으로 한 번만 잡는다 - 엣지 화살표 모드로
            // 바뀌어도(아이콘이 더 작은 edgeArrowPixelSize로 바뀌어도) 라벨 자리는 그대로 유지된다.
            pin.DistanceLabel.rectTransform.anchoredPosition = new Vector2(0f, -(config.iconPixelSize * 0.5f) - 4f);

            LandmarkBinding binding = new LandmarkBinding(
                (RectTransform)pin.transform, pin.Icon, pin.DistanceLabel, normalSprite);
            bindings[landmark.Id] = binding;
            return binding;
        }

        private Sprite ResolveIconSprite(ARLandmarkSnapshot landmark)
        {
            // 지도와 같은 키를 그대로 쓴다. 같은 category면 지도와 AR에 같은 아이콘이 뜬다.
            Sprite sprite = iconLibrary.Load(landmark.IconKey);
            return sprite != null
                ? sprite
                : ARUiFactory.CreateDiamondSprite(config.iconPixelSize, FallbackIconColor);
        }

        private static string FormatDistance(double distanceMeters)
        {
            return string.Format("{0:0}m", distanceMeters);
        }

        private sealed class LandmarkBinding
        {
            public readonly RectTransform Root;
            public readonly Image Icon;
            public readonly TMP_Text DistanceLabel;
            public readonly Sprite NormalSprite;

            public LandmarkBinding(RectTransform root, Image icon, TMP_Text distanceLabel, Sprite normalSprite)
            {
                Root = root;
                Icon = icon;
                DistanceLabel = distanceLabel;
                NormalSprite = normalSprite;
            }
        }
    }
}
