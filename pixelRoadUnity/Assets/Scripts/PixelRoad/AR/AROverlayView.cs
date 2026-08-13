using System.Collections.Generic;
using PixelRoad.Data;
using PixelRoad.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PixelRoad.AR
{
    /// <summary>ARScene의 절차적 오버레이 UI: 랜드마크별 아이콘/거리 라벨, 화면 밖 방향 화살표, 상태 메시지, 뒤로가기.</summary>
    public sealed class AROverlayView
    {
        private const string MapSceneName = "MapScene";
        private const string NoLandmarksMessage = "근처에 랜드마크가 없습니다.\n잠시 후 지도로 돌아갑니다.";
        private const float BlinkFrequencyHz = 2f;
        private const float MinBlinkAlpha = 0.25f;

        private static readonly Color32 UnlockedIconTint = Color.white;
        private static readonly Color32 LockedIconTint = new Color32(124, 120, 112, 235);
        private static readonly Color32 TextColor = new Color32(246, 237, 217, 255);
        private static readonly Color32 FallbackIconColor = new Color32(208, 56, 48, 255);

        private readonly RectTransform overlayRoot;
        private readonly ARConfig config;
        private readonly SpotIconLibrary iconLibrary;
        private readonly Dictionary<string, LandmarkBinding> bindings = new Dictionary<string, LandmarkBinding>();
        private readonly Sprite arrowSprite;
        private readonly TMP_Text statusText;

        public AROverlayView(RectTransform overlayRoot, ARConfig config)
        {
            this.overlayRoot = overlayRoot;
            this.config = config;
            iconLibrary = new SpotIconLibrary(config.spotIconResourceFolder, config.defaultSpotIconName);
            arrowSprite = ARUiFactory.CreateTriangleSprite(config.edgeArrowPixelSize, TextColor);

            CreateBackButton();
            statusText = CreateStatusText();
        }

        /// <summary>ARCore 미지원 등 랜드마크 갱신을 멈추고 안내만 보여줘야 할 때 쓴다. null/빈 문자열이면 숨긴다.</summary>
        public void SetStatusMessage(string message)
        {
            bool visible = !string.IsNullOrEmpty(message);
            if (visible)
            {
                statusText.text = message;
                statusText.color = TextColor;
            }

            statusText.gameObject.SetActive(visible);
        }

        /// <summary>
        /// 반경 내 랜드마크가 하나도 없을 때 화면 중앙에 깜빡이는 경고 문구를 표시한다.
        /// elapsedSeconds는 경고가 뜬 뒤 흐른 시간(초)으로, 깜빡임 위상을 계산하는 데만 쓴다.
        /// </summary>
        public void SetNoLandmarksWarning(bool visible, float elapsedSeconds)
        {
            if (!visible)
            {
                statusText.gameObject.SetActive(false);
                return;
            }

            statusText.text = NoLandmarksMessage;
            statusText.gameObject.SetActive(true);
            float phase = elapsedSeconds * BlinkFrequencyHz * Mathf.PI * 2f;
            float alpha = MinBlinkAlpha + (1f - MinBlinkAlpha) * (0.5f + 0.5f * Mathf.Sin(phase));
            Color color = TextColor;
            color.a = alpha;
            statusText.color = color;
        }

        public void ShowOnScreen(ARLandmarkSnapshot landmark, float anchoredX, double distanceMeters)
        {
            LandmarkBinding binding = GetOrCreateBinding(landmark);
            binding.Root.gameObject.SetActive(true);
            binding.Icon.sprite = binding.NormalSprite;
            binding.Icon.rectTransform.sizeDelta = new Vector2(config.iconPixelSize, config.iconPixelSize);
            binding.Icon.rectTransform.localEulerAngles = Vector3.zero;
            binding.Root.anchoredPosition = new Vector2(anchoredX, 0f);
            binding.DistanceLabel.text = FormatDistance(distanceMeters);
        }

        /// <summary>
        /// 화면 가장자리 방향 화살표를 표시한다. sameSideSlotIndex는 이번 프레임에 같은 쪽(좌/우)에
        /// 표시되는 화살표들 사이에서 이 랜드마크의 순번(0부터)이다 - 여러 개가 겹치지 않도록 세로로 쌓는 데 쓴다.
        /// </summary>
        public void ShowAtEdge(ARLandmarkSnapshot landmark, bool rightSide, double distanceMeters, int sameSideSlotIndex)
        {
            LandmarkBinding binding = GetOrCreateBinding(landmark);
            binding.Root.gameObject.SetActive(true);
            binding.Icon.sprite = arrowSprite;
            binding.Icon.rectTransform.sizeDelta = new Vector2(config.edgeArrowPixelSize, config.edgeArrowPixelSize);
            binding.Icon.rectTransform.localEulerAngles = new Vector3(0f, 0f, rightSide ? -90f : 90f);

            float halfWidth = overlayRoot.rect.width * 0.5f;
            float x = Mathf.Max(0f, halfWidth - config.edgeMarginPixels);
            float y = StackedOffsetY(sameSideSlotIndex, config.edgeStackSpacingPixels);
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

        private LandmarkBinding GetOrCreateBinding(ARLandmarkSnapshot landmark)
        {
            if (bindings.TryGetValue(landmark.Id, out LandmarkBinding existing))
            {
                return existing;
            }

            RectTransform root = ARUiFactory.CreateRect("Landmark_" + landmark.Id, overlayRoot);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = Vector2.zero;

            Sprite normalSprite = ResolveIconSprite(landmark);

            Image icon = ARUiFactory.CreateObject("Icon", root).AddComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            icon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            icon.rectTransform.sizeDelta = new Vector2(config.iconPixelSize, config.iconPixelSize);
            icon.sprite = normalSprite;
            icon.color = landmark.IsUnlocked ? (Color)UnlockedIconTint : (Color)LockedIconTint;

            TMP_Text label = ARUiFactory.CreateText(
                "Distance",
                root,
                string.Empty,
                18,
                TextAlignmentOptions.Center,
                TextColor);
            label.raycastTarget = false;
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0.5f, 0f);
            labelRect.anchorMax = new Vector2(0.5f, 0f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.sizeDelta = new Vector2(160f, 32f);
            labelRect.anchoredPosition = new Vector2(0f, -(config.iconPixelSize * 0.5f) - 4f);

            LandmarkBinding binding = new LandmarkBinding(root, icon, label, normalSprite);
            bindings[landmark.Id] = binding;
            return binding;
        }

        private Sprite ResolveIconSprite(ARLandmarkSnapshot landmark)
        {
            Sprite sprite = iconLibrary.Resolve(landmark.IconKey, landmark.Category);
            return sprite != null
                ? sprite
                : ARUiFactory.CreateDiamondSprite(config.iconPixelSize, FallbackIconColor);
        }

        private void CreateBackButton()
        {
            Button button = ARUiFactory.CreateButton(
                "BackButton",
                overlayRoot,
                "지도로",
                new Vector2(112f, 52f),
                new Color32(239, 228, 199, 255),
                new Color32(18, 17, 15, 255));
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(16f, -16f);
            button.onClick.AddListener(() =>
            {
                ARHandoff.Clear();
                SceneManager.LoadScene(MapSceneName);
            });
        }

        private TMP_Text CreateStatusText()
        {
            TMP_Text text = ARUiFactory.CreateText(
                "StatusMessage",
                overlayRoot,
                string.Empty,
                22,
                TextAlignmentOptions.Center,
                TextColor);
            text.raycastTarget = false;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.1f, 0.4f);
            rect.anchorMax = new Vector2(0.9f, 0.6f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            text.gameObject.SetActive(false);
            return text;
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
