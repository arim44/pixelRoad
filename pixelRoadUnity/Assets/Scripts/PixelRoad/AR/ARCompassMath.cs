using UnityEngine;

namespace PixelRoad.AR
{
    /// <summary>베어링·헤딩 각도를 화면 좌표로 매핑하는 순수 계산 헬퍼.</summary>
    public static class ARCompassMath
    {
        /// <summary>각도를 (-180, 180] 범위로 정규화한다. 0/360도 경계를 넘어가는 델타 계산에 쓴다.</summary>
        public static float NormalizeAngle(float angleDegrees)
        {
            float a = angleDegrees % 360f;
            if (a > 180f)
            {
                a -= 360f;
            }

            if (a <= -180f)
            {
                a += 360f;
            }

            return a;
        }

        /// <summary>Camera.fieldOfView(수직 FOV)와 현재 화면비로부터 수평 FOV를 유도한다.</summary>
        public static float HorizontalFovDegrees(Camera camera)
        {
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            float verticalFovRad = camera.fieldOfView * Mathf.Deg2Rad;
            float horizontalFovRad = 2f * Mathf.Atan(Mathf.Tan(verticalFovRad * 0.5f) * aspect);
            return horizontalFovRad * Mathf.Rad2Deg;
        }

        /// <summary>헤딩 대비 베어링 델타(도, -180~180)를 캔버스 절반 폭 기준 x좌표로 선형 매핑한다.</summary>
        public static float DeltaToScreenX(float deltaDegrees, float horizontalFovDegrees, float halfCanvasWidth)
        {
            float normalized = Mathf.Clamp(deltaDegrees / (horizontalFovDegrees * 0.5f), -1f, 1f);
            return normalized * halfCanvasWidth;
        }
    }
}
