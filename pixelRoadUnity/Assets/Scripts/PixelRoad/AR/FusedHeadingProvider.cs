using UnityEngine;

namespace PixelRoad.AR
{
    /// <summary>
    /// AR 세션이 활성화되면 ARCore/ARKit이 기기 센서(나침반 포함)를 자체 추적용으로 점유해
    /// Input.compass 값이 멈추는 기기가 많다. 반면 AR 카메라의 회전(Transform)은 AR 추적이
    /// 살아있는 한 계속 갱신되므로, 이를 방향의 기준으로 쓰고 나침반 값이 실제로 갱신될 때마다
    /// (가능한 경우) 진북 보정 오프셋을 다시 잡아 두 값을 융합한다.
    /// </summary>
    public sealed class FusedHeadingProvider : IHeadingProvider
    {
        private const float CompassChangeEpsilonDegrees = 0.05f;

        private readonly Transform cameraTransform;
        private float lastCompassReading;
        private bool hasLastCompassReading;
        private float northOffsetDegrees;
        private bool hasNorthOffset;

        public float HeadingDegrees { get; private set; }
        public bool IsAvailable { get { return true; } }

        public FusedHeadingProvider(Transform cameraTransform)
        {
            this.cameraTransform = cameraTransform;
        }

        public void Start()
        {
            Input.compass.enabled = true;
        }

        public void Tick(float deltaTime)
        {
            float cameraYaw = cameraTransform.eulerAngles.y;

            if (Input.compass.enabled)
            {
                bool hasLocationFix = Input.location.status == LocationServiceStatus.Running;
                float compassReading = hasLocationFix ? Input.compass.trueHeading : Input.compass.magneticHeading;
                bool changed = !hasLastCompassReading
                    || Mathf.Abs(Mathf.DeltaAngle(lastCompassReading, compassReading)) > CompassChangeEpsilonDegrees;
                if (changed)
                {
                    northOffsetDegrees = Normalize360(compassReading - cameraYaw);
                    hasNorthOffset = true;
                    lastCompassReading = compassReading;
                    hasLastCompassReading = true;
                }
            }

            HeadingDegrees = hasNorthOffset ? Normalize360(cameraYaw + northOffsetDegrees) : Normalize360(cameraYaw);
        }

        public void Stop()
        {
            Input.compass.enabled = false;
        }

        private static float Normalize360(float degrees)
        {
            float a = degrees % 360f;
            return a < 0f ? a + 360f : a;
        }
    }
}
