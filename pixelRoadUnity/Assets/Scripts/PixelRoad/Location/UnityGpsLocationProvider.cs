using System.Collections;
using PixelRoad.Data;
using UnityEngine;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace PixelRoad.Location
{
    /// <summary>
    /// 실기기용 위치 공급자. 안드로이드 위치 권한과 Unity 위치 서비스를 다루고, 진행 상태를 문구로 남긴다.
    /// </summary>
    public sealed class UnityGpsLocationProvider : ILocationProvider
    {
        private readonly MapConfig config;
        private GeoLocation current;
        private string statusText = "GPS starting";

        public GeoLocation Current
        {
            get { return current; }
        }

        public string StatusText
        {
            get { return statusText; }
        }

        /// <summary>정확도·갱신 거리 등 위치 서비스 설정을 담은 지도 설정을 받아 둔다.</summary>
        public UnityGpsLocationProvider(MapConfig config)
        {
            this.config = config;
        }

        /// <summary>
        /// 위치 권한을 확인·요청하고 위치 서비스를 켠다. 실패하면 상태 문구만 남기고 조용히 끝낸다.
        /// </summary>
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

        /// <summary>위치 서비스가 돌고 있을 때만 최신 좌표를 읽어 온다.</summary>
        public void Tick(float deltaTime)
        {
            if (Input.location.status == LocationServiceStatus.Running)
            {
                UpdateCurrent();
            }
        }

        /// <summary>위치 서비스를 꺼서 배터리 소모를 줄인다.</summary>
        public void Stop()
        {
            if (Input.location.status == LocationServiceStatus.Running)
            {
                Input.location.Stop();
            }
        }

        /// <summary>Unity가 마지막으로 받은 측위 결과를 내부 좌표로 옮긴다.</summary>
        private void UpdateCurrent()
        {
            LocationInfo data = Input.location.lastData;
            current = new GeoLocation(data.latitude, data.longitude, data.horizontalAccuracy, true);
        }
    }
}
