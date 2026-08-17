using System;
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
        private const float CaptureButtonSize = 88f;
        private const float CaptureButtonBottomMargin = 48f;
        private const float ThumbnailWidth = 160f;
        private const float ThumbnailHeight = 220f;
        private const float ThumbnailMargin = 16f;
        private const float ThumbnailFrameBorder = 6f;

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
        private readonly TMP_Text toastText;
        private readonly Button captureButton;
        private readonly Button backButton;
        private readonly Image thumbnailImage;

        /// <summary>촬영 버튼이 눌렸을 때 발생한다. 실제 캡처·저장은 코루틴이 필요해 ARSceneController가 처리한다.</summary>
        public event Action CaptureRequested;

        /// <summary>썸네일을 눌렀을 때 발생한다. 갤러리 앱을 여는 건 플랫폼 API가 필요해 ARSceneController가 처리한다.</summary>
        public event Action ThumbnailClicked;

        /// <summary>랜드마크 핀(아이콘)을 눌렀을 때 그 랜드마크의 id와 함께 발생한다.</summary>
        public event Action<string> LandmarkClicked;

        public AROverlayView(RectTransform overlayRoot, ARConfig config)
        {
            this.overlayRoot = overlayRoot;
            this.config = config;
            iconLibrary = new SpotIconLibrary(config.spotIconResourceFolder, config.defaultSpotIconName);
            arrowSprite = ARUiFactory.CreateTriangleSprite(config.edgeArrowPixelSize, TextColor);

            backButton = CreateBackButton();
            statusText = CreateStatusText();
            toastText = CreateToastText();
            captureButton = CreateCaptureButton();
            thumbnailImage = CreateCaptureThumbnail();
        }

        /// <summary>화면 위쪽에 짧은 안내 문구를 잠깐 보여준다(예: 갤러리 열기 실패). 자동으로 사라지지는 않고, 다시 호출하면 내용만 갱신된다.</summary>
        public void ShowToast(string message)
        {
            toastText.text = message;
            toastText.gameObject.SetActive(true);
        }

        public void HideToast()
        {
            toastText.gameObject.SetActive(false);
        }

        /// <summary>
        /// 캡처 순간에만 촬영 버튼·뒤로가기 버튼·이전 썸네일을 화면에서 감춰서, 찍힌 사진에 UI가 함께 찍히지 않게 한다.
        /// 썸네일은 감추기만 하고 다시 켜지는 않는다 - 캡처 직후 ShowCapturedThumbnail이 새 사진으로 바로 다시 띄우기 때문이다.
        /// </summary>
        public void SetCaptureUiVisible(bool visible)
        {
            captureButton.gameObject.SetActive(visible);
            backButton.gameObject.SetActive(visible);
            if (!visible)
            {
                thumbnailImage.transform.parent.gameObject.SetActive(false);
            }
        }

        /// <summary>방금 찍은 사진을 화면 오른쪽 아래에 작게 띄운다. 스프라이트 소유권은 호출측이 계속 갖고, 다 쓰면 텍스처를 직접 정리해야 한다.</summary>
        public void ShowCapturedThumbnail(Sprite sprite)
        {
            thumbnailImage.sprite = sprite;
            thumbnailImage.transform.parent.gameObject.SetActive(true);
        }

        /// <summary>썸네일을 감춘다 - 원본 사진이 갤러리에서 삭제된 것으로 확인됐을 때 등에 쓴다.</summary>
        public void HideCapturedThumbnail()
        {
            thumbnailImage.sprite = null;
            thumbnailImage.transform.parent.gameObject.SetActive(false);
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

            float halfWidth = overlayRoot.rect.width * 0.5f;
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
            icon.raycastTarget = true;
            icon.preserveAspect = true;
            icon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            icon.rectTransform.sizeDelta = new Vector2(config.iconPixelSize, config.iconPixelSize);
            icon.sprite = normalSprite;
            icon.color = landmark.IsUnlocked ? (Color)UnlockedIconTint : (Color)LockedIconTint;

            string landmarkId = landmark.Id;
            Button iconButton = icon.gameObject.AddComponent<Button>();
            iconButton.targetGraphic = icon;
            iconButton.onClick.AddListener(() => LandmarkClicked?.Invoke(landmarkId));

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

        private Button CreateBackButton()
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
            return button;
        }

        /// <summary>화면 아래쪽 가운데의 원형 촬영 버튼. 카메라 셔터 버튼 모양을 흉내낸 흰색 원이다.</summary>
        private Button CreateCaptureButton()
        {
            GameObject buttonObject = ARUiFactory.CreateObject("CaptureButton", overlayRoot);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(CaptureButtonSize, CaptureButtonSize);
            rect.anchoredPosition = new Vector2(0f, CaptureButtonBottomMargin);

            Image image = buttonObject.AddComponent<Image>();
            image.sprite = ARUiFactory.CreateCircleSprite((int)CaptureButtonSize, Color.white);
            image.color = Color.white;

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => CaptureRequested?.Invoke());
            return button;
        }

        /// <summary>
        /// 화면 오른쪽 아래의 작은 촬영 결과 미리보기. 테두리 프레임 안에 실제 사진을 채우고,
        /// 프레임 전체를 눌러 갤러리로 이동할 수 있게 Button을 붙인다.
        /// 반환하는 Image의 부모 GameObject가 곧 프레임이다(표시/숨김은 그 부모를 켜고 끈다).
        /// </summary>
        private Image CreateCaptureThumbnail()
        {
            GameObject frame = ARUiFactory.CreateObject("CaptureThumbnailFrame", overlayRoot);
            RectTransform frameRect = frame.GetComponent<RectTransform>();
            frameRect.anchorMin = new Vector2(1f, 0f);
            frameRect.anchorMax = new Vector2(1f, 0f);
            frameRect.pivot = new Vector2(1f, 0f);
            frameRect.sizeDelta = new Vector2(ThumbnailWidth, ThumbnailHeight);
            frameRect.anchoredPosition = new Vector2(-ThumbnailMargin, ThumbnailMargin);

            Image frameBackground = frame.AddComponent<Image>();
            frameBackground.color = new Color32(18, 17, 15, 220);

            Button frameButton = frame.AddComponent<Button>();
            frameButton.targetGraphic = frameBackground;
            frameButton.onClick.AddListener(() => ThumbnailClicked?.Invoke());

            RectTransform photoRect = ARUiFactory.CreateRect("Photo", frame.transform);
            photoRect.anchorMin = Vector2.zero;
            photoRect.anchorMax = Vector2.one;
            photoRect.offsetMin = new Vector2(ThumbnailFrameBorder, ThumbnailFrameBorder);
            photoRect.offsetMax = new Vector2(-ThumbnailFrameBorder, -ThumbnailFrameBorder);

            Image photo = photoRect.gameObject.AddComponent<Image>();
            photo.preserveAspect = true;
            photo.raycastTarget = false;

            frame.SetActive(false);
            return photo;
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

        /// <summary>화면 위쪽에 짧게 뜨는 안내 문구. 화면 정중앙(상태 메시지 자리)과 겹치지 않게 그 위쪽에 둔다.</summary>
        private TMP_Text CreateToastText()
        {
            TMP_Text text = ARUiFactory.CreateText(
                "Toast",
                overlayRoot,
                string.Empty,
                18,
                TextAlignmentOptions.Center,
                TextColor);
            text.raycastTarget = false;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.1f, 0.68f);
            rect.anchorMax = new Vector2(0.9f, 0.76f);
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
