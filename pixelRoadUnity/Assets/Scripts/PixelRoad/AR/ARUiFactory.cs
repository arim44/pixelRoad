using UnityEngine;

namespace PixelRoad.AR
{
    /// <summary>
    /// AR 화면에서 데이터에 따라 매 프레임 바뀌는 픽셀아트 스프라이트(엣지 화살표, 집중 모드 나침반,
    /// 아이콘 없는 랜드마크의 대체 마커)를 코드로 생성하는 헬퍼.
    /// 정적인 UI 구조(버튼 위치, 텍스트 등)는 AROverlayUIRoot.prefab에 있고, 이 클래스는 그 프리팹의
    /// Image 컴포넌트에 입힐 텍스처만 만든다.
    /// </summary>
    internal static class ARUiFactory
    {
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
