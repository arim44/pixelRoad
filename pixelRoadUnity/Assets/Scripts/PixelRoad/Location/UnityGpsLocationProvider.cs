using System.Collections;
using PixelRoad.Data;
using UnityEngine;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace PixelRoad.Location
{
    public sealed class UnityGpsLocationProvider : ILocationProvider
    {
        private readonly MapConfig config;
        private GeoLocationSample current;
        private string statusText = "GPS starting";

        public GeoLocationSample Current
        {
            get { return current; }
        }

        public string StatusText
        {
            get { return statusText; }
        }

        public UnityGpsLocationProvider(MapConfig config)
        {
            this.config = config;
        }

        public IEnumerator Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            bool hasFineLocation = Permission.HasUserAuthorizedPermission(Permission.FineLocation);
            bool hasCoarseLocation = Permission.HasUserAuthorizedPermission(Permission.CoarseLocation);
            if (!hasFineLocation)
            {
                // Android 12+에서는 정밀 위치를 요청할 때 대략적 위치도 같은 요청에
                // 포함해야 한다. 사용자가 대략적 위치만 허용해도 GPS 시작은 가능하다.
                Permission.RequestUserPermissions(new[]
                {
                    Permission.CoarseLocation,
                    Permission.FineLocation
                });
                float permissionWait = 0f;
                while (!Permission.HasUserAuthorizedPermission(Permission.FineLocation)
                       && !Permission.HasUserAuthorizedPermission(Permission.CoarseLocation)
                       && permissionWait < 10f)
                {
                    permissionWait += Time.unscaledDeltaTime;
                    yield return null;
                }

                hasFineLocation = Permission.HasUserAuthorizedPermission(Permission.FineLocation);
                hasCoarseLocation = Permission.HasUserAuthorizedPermission(Permission.CoarseLocation);
            }

            if (!hasFineLocation && !hasCoarseLocation)
            {
                statusText = "Location permission denied";
                yield break;
            }
#endif

            if (!Input.location.isEnabledByUser)
            {
                statusText = "Location permission disabled";
                yield break;
            }

            Input.location.Start(config.desiredAccuracyMeters, config.locationUpdateDistanceMeters);
            int maxWaitSeconds = 20;
            while (Input.location.status == LocationServiceStatus.Initializing && maxWaitSeconds > 0)
            {
                statusText = "GPS initializing";
                yield return new WaitForSeconds(1f);
                maxWaitSeconds--;
            }

            if (Input.location.status == LocationServiceStatus.Failed)
            {
                statusText = "GPS failed";
                yield break;
            }

            statusText = "GPS ready";
            UpdateCurrent();
        }

        public void Tick(float deltaTime)
        {
            if (Input.location.status == LocationServiceStatus.Running)
            {
                UpdateCurrent();
            }
        }

        public void Stop()
        {
            if (Input.location.status == LocationServiceStatus.Running)
            {
                Input.location.Stop();
            }
        }

        private void UpdateCurrent()
        {
            LocationInfo data = Input.location.lastData;
            current = new GeoLocationSample(data.latitude, data.longitude, data.horizontalAccuracy, true);
        }
    }
}
