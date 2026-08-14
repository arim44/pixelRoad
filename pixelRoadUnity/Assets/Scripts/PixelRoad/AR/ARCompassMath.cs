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

        /// <summary>
        /// 각도 델타(도)를 시야각(FOV) 대비 화면 절반 크기 기준 좌표로 선형 매핑한다.
        /// 가로(헤딩 대비 베어링 → x)와 세로(카메라 피치 대비 랜드마크 고도 → y) 매핑에 공용으로 쓴다.
        /// </summary>
        public static float DeltaToScreenOffset(float deltaDegrees, float fovDegrees, float halfCanvasExtent)
        {
            float normalized = Mathf.Clamp(deltaDegrees / (fovDegrees * 0.5f), -1f, 1f);
            return normalized * halfCanvasExtent;
        }
    }
}
