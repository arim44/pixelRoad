using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelRoad.AR
{
    /// <summary>
    /// MapScene -> ARScene 전환 중 표시되는 절차적 로딩 화면.
    /// 자체 Canvas(높은 sortingOrder)를 만들어 지도 UI 위에 항상 그려지며,
    /// 씬 전환이 활성화되면 MapScene의 다른 오브젝트와 함께 자동으로 파괴된다.
    /// </summary>
    public sealed class LoadingScreenView
    {
        private const int SortingOrder = 1000;

        private readonly Image progressFill;

        private LoadingScreenView(Image progressFill)
        {
            this.progressFill = progressFill;
        }

        public static LoadingScreenView Create()
        {
            GameObject canvasObject = new GameObject(
                "ARLoadingCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            RectTransform background = ARUiFactory.CreateRect("Background", canvasObject.transform);
            ARUiFactory.Stretch(background);
            Image backgroundImage = background.gameObject.AddComponent<Image>();
            backgroundImage.color = new Color32(15, 14, 12, 230);

            ARUiFactory.CreateText(
                "Message",
                background,
                "AR 화면 불러오는 중…",
                26,
                TextAlignmentOptions.Center,
                new Color32(246, 237, 217, 255));
            RectTransform messageRect = background.Find("Message").GetComponent<RectTransform>();
            messageRect.anchorMin = new Vector2(0.15f, 0.54f);
            messageRect.anchorMax = new Vector2(0.85f, 0.64f);
            messageRect.offsetMin = Vector2.zero;
            messageRect.offsetMax = Vector2.zero;

            RectTransform barBackground = ARUiFactory.CreateRect("ProgressBarBackground", background);
            barBackground.anchorMin = new Vector2(0.15f, 0.48f);
            barBackground.anchorMax = new Vector2(0.85f, 0.52f);
            barBackground.offsetMin = Vector2.zero;
            barBackground.offsetMax = Vector2.zero;
            Image barBackgroundImage = barBackground.gameObject.AddComponent<Image>();
            barBackgroundImage.color = new Color32(70, 67, 61, 255);

            RectTransform barFill = ARUiFactory.CreateRect("ProgressBarFill", barBackground);
            ARUiFactory.Stretch(barFill);
            Image barFillImage = barFill.gameObject.AddComponent<Image>();
            barFillImage.color = new Color32(208, 56, 48, 255);
            barFillImage.type = Image.Type.Filled;
            barFillImage.fillMethod = Image.FillMethod.Horizontal;
            barFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            barFillImage.fillAmount = 0f;

            return new LoadingScreenView(barFillImage);
        }

        public void SetProgress(float progress01)
        {
            progressFill.fillAmount = Mathf.Clamp01(progress01);
        }
    }
}
