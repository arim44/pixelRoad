using System.Collections;
using UnityEngine;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace PixelRoad.Location
{
    public sealed class UnityGpsLocationProvider : ILocationProvider
    {
        private readonly float desiredAccuracyMeters;
        private readonly float locationUpdateDistanceMeters;
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

        public UnityGpsLocationProvider(float desiredAccuracyMeters, float locationUpdateDistanceMeters)
        {
            this.desiredAccuracyMeters = desiredAccuracyMeters;
            this.locationUpdateDistanceMeters = locationUpdateDistanceMeters;
        }

        public IEnumerator Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                Permission.RequestUserPermission(Permission.FineLocation);
                float permissionWait = 0f;
                while (!Permission.HasUserAuthorizedPermission(Permission.FineLocation) && permissionWait < 10f)
                {
                    permissionWait += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
#endif

            if (!Input.location.isEnabledByUser)
            {
                statusText = "Location permission disabled";
                yield break;
            }

            Input.location.Start(desiredAccuracyMeters, locationUpdateDistanceMeters);
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
