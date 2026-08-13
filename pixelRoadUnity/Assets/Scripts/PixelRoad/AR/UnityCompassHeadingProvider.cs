using UnityEngine;

namespace PixelRoad.AR
{
    /// <summary>기기 나침반(Input.compass)을 감싼다. GPS 픽스 전에는 trueHeading이 유효하지 않아 magneticHeading으로 대체한다.</summary>
    public sealed class UnityCompassHeadingProvider : IHeadingProvider
    {
        public float HeadingDegrees { get; private set; }
        public bool IsAvailable { get; private set; }

        public void Start()
        {
            Input.compass.enabled = true;
        }

        public void Tick(float deltaTime)
        {
            IsAvailable = Input.compass.enabled;
            if (!IsAvailable)
            {
                return;
            }

            bool hasTrueHeading = Input.location.status == LocationServiceStatus.Running;
            HeadingDegrees = hasTrueHeading ? Input.compass.trueHeading : Input.compass.magneticHeading;
        }

        public void Stop()
        {
            Input.compass.enabled = false;
        }
    }
}
