using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelRoad.AR
{
    /// <summary>
    /// AR 화면의 절차적 UI 생성에 쓰는 공용 헬퍼.
    /// PixelRoadRuntimeView가 자체적으로 갖고 있는 것과 같은 패턴(코드로 RectTransform/Image/TMP_Text 생성)을
    /// AR 관련 신규 파일(LoadingScreenView, AROverlayView)끼리 공유하기 위한 것으로, 기존 UI 코드는 건드리지 않는다.
    /// </summary>
    internal static class ARUiFactory
    {
        private const string PixelFontResourcePath = "PixelRoad/Fonts/Galmuri11";

        private static TMP_FontAsset pixelFont;
        private static bool pixelFontLoadAttempted;

        public static GameObject CreateObject(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            return CreateObject(name, parent).GetComponent<RectTransform>();
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static TMP_Text CreateText(
            string name,
            Transform parent,
            string value,
            int size,
            TextAlignmentOptions alignment,
            Color32 color)
        {
            TextMeshProUGUI text = CreateObject(name, parent).AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.color = color;
            TMP_FontAsset font = GetPixelFont();
            if (font != null)
            {
                text.font = font;
            }

            return text;
        }

        /// <summary>
        /// 지도 UI(PixelRoadRuntimeView)와 같은 한글 지원 픽셀 폰트를 쓴다.
        /// 기본 TMP 폰트(LiberationSans SDF)는 한글 글리프가 없어 텍스트가 깨져 보인다.
        /// </summary>
        private static TMP_FontAsset GetPixelFont()
        {
            if (pixelFontLoadAttempted)
            {
                return pixelFont;
            }

            pixelFontLoadAttempted = true;
            Font font = Resources.Load<Font>(PixelFontResourcePath);
            if (font == null)
            {
                return null;
            }

            try
            {
                pixelFont = TMP_FontAsset.CreateFontAsset(font);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[PixelRoad] Failed to create Galmuri TMP font asset for AR UI. " + exception.Message);
                pixelFont = null;
            }

            return pixelFont;
        }

        public static Button CreateButton(
            string name,
            Transform parent,
            string label,
            Vector2 size,
            Color32 backgroundColor,
            Color32 labelColor)
        {
            Button button = CreateObject(name, parent).AddComponent<Button>();
            Image image = button.gameObject.AddComponent<Image>();
            image.color = backgroundColor;
            button.targetGraphic = image;
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            if (!string.IsNullOrEmpty(label))
            {
                TMP_Text text = CreateText("Label", button.transform, label, 18, TextAlignmentOptions.Center, labelColor);
                Stretch(text.rectTransform);
            }

            return button;
        }

        /// <summary>아이콘 PNG가 없을 때 쓰는 코드 생성 다이아몬드 마커. 맵 마커와 같은 모양 언어를 쓴다.</summary>
        public static Sprite CreateDiamondSprite(int size, Color32 color)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 border = new Color32(20, 18, 16, 255);
            float center = (size - 1) * 0.5f;
            float radius = size * 0.42f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float manhattan = Mathf.Abs(x - center) + Mathf.Abs(y - center);
                    bool inside = manhattan <= radius;
                    bool isBorder = Mathf.Abs(manhattan - radius) < 1.4f;
                    texture.SetPixel(x, y, inside ? (isBorder ? border : color) : clear);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>카메라 셔터 버튼 등에 쓰는 원형 스프라이트. 테두리를 살짝 어둡게 둘러 입체감을 준다.</summary>
        public static Sprite CreateCircleSprite(int size, Color32 color)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 border = new Color32(20, 18, 16, 255);
            float center = (size - 1) * 0.5f;
            float radius = size * 0.46f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    if (distance <= radius)
                    {
                        texture.SetPixel(x, y, distance > radius - 3f ? border : color);
                    }
                    else
                    {
                        texture.SetPixel(x, y, clear);
                    }
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>화면 가장자리 방향 화살표에 쓰는 위쪽을 향한 삼각형 스프라이트.</summary>
        public static Sprite CreateTriangleSprite(int size, Color32 color)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 border = new Color32(20, 18, 16, 255);
            float apexY = size - 1f;
            float halfBase = size * 0.42f;
            float centerX = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                float t = y / Mathf.Max(1f, apexY);
                float rowHalfWidth = halfBase * (1f - t);
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - centerX);
                    bool inside = dx <= rowHalfWidth;
                    bool isBorder = inside && (rowHalfWidth - dx < 1.4f || y == 0);
                    texture.SetPixel(x, y, inside ? (isBorder ? border : color) : clear);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
