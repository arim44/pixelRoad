using System.Collections;
using System.Collections.Generic;
using PixelRoad.Data;
using PixelRoad.Geo;
using PixelRoad.Location;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace PixelRoad.AR
{
    /// <summary>
    /// ARScene의 진입점. ArHandoff로 받은 랜드마크/현재 위치를 바탕으로 위치·나침반을 갱신하고,
    /// 매 프레임 랜드마크별 베어링·거리를 계산해 ArOverlayView에 표시를 위임한다.
    /// </summary>
    public sealed class ArSceneController : MonoBehaviour
    {
        [SerializeField]
        private Camera arCamera;

        [SerializeField]
        private RectTransform overlayRoot;

        [SerializeField]
        private ARSession arSession;

        private ARConfig config;
        private ArOverlayView view;
        private ILocationProvider locationProvider;
        private IHeadingProvider headingProvider;
        private GeoLocationSample currentLocation;
        private float smoothedHeading;
        private bool hasSmoothedHeading;
        private bool ready;

        private IEnumerator Start()
        {
            config = ArSceneLauncher.LoadConfig();
            view = new ArOverlayView(overlayRoot, config);

            if (!ArHandoff.HasData)
            {
                view.SetStatusMessage("표시할 랜드마크 정보가 없습니다. 지도 화면에서 다시 시도해 주세요.");
                yield break;
            }

            currentLocation = ArHandoff.InitialLocation;

#if UNITY_EDITOR
            locationProvider = new SimulatedLocationProvider(currentLocation.Latitude, currentLocation.Longitude);
            headingProvider = new SimulatedHeadingProvider();
#else
            locationProvider = new UnityGpsLocationProvider(config.desiredAccuracyMeters, config.locationUpdateDistanceMeters);
            headingProvider = new FusedHeadingProvider(arCamera.transform);
#endif
            yield return StartCoroutine(locationProvider.Start());
            headingProvider.Start();
            ready = true;
        }

        private void Update()
        {
            if (!ready || view == null)
            {
                return;
            }

            if (!IsSessionTrackingOrStarting())
            {
                view.SetStatusMessage(BuildSessionStatusMessage());
                return;
            }

            view.SetStatusMessage(null);

            locationProvider.Tick(Time.deltaTime);
            headingProvider.Tick(Time.deltaTime);
            currentLocation = locationProvider.Current;
            if (!currentLocation.IsValid)
            {
                return;
            }

            UpdateSmoothedHeading(headingProvider.HeadingDegrees);
            UpdateLandmarks();
        }

        private void OnDestroy()
        {
            locationProvider?.Stop();
            headingProvider?.Stop();
        }

        private void UpdateSmoothedHeading(float rawHeading)
        {
            if (!hasSmoothedHeading)
            {
                smoothedHeading = rawHeading;
                hasSmoothedHeading = true;
                return;
            }

            float lerpFactor = 1f - Mathf.Pow(1f - Mathf.Clamp01(config.headingSmoothingFactor), Time.deltaTime * 60f);
            smoothedHeading = Mathf.LerpAngle(smoothedHeading, rawHeading, lerpFactor);
        }

        private void UpdateLandmarks()
        {
            float horizontalFov = ArCompassMath.HorizontalFovDegrees(arCamera);
            float halfCanvasWidth = overlayRoot.rect.width * 0.5f;

            IReadOnlyList<ArLandmarkSnapshot> landmarks = ArHandoff.Landmarks;
            for (int i = 0; i < landmarks.Count; i++)
            {
                ArLandmarkSnapshot landmark = landmarks[i];
                double distance = GeoProjection.DistanceMeters(
                    currentLocation.Latitude,
                    currentLocation.Longitude,
                    landmark.Latitude,
                    landmark.Longitude);
                if (distance > config.arDisplayRadiusMeters)
                {
                    view.Hide(landmark.Id);
                    continue;
                }

                double bearing = GeoProjection.BearingDegrees(
                    currentLocation.Latitude,
                    currentLocation.Longitude,
                    landmark.Latitude,
                    landmark.Longitude);
                float delta = ArCompassMath.NormalizeAngle((float)bearing - smoothedHeading);

                if (Mathf.Abs(delta) <= horizontalFov * 0.5f)
                {
                    float screenX = ArCompassMath.DeltaToScreenX(delta, horizontalFov, halfCanvasWidth);
                    view.ShowOnScreen(landmark, screenX, distance);
                }
                else
                {
                    view.ShowAtEdge(landmark, delta > 0f, distance);
                }
            }
        }

        private bool IsSessionTrackingOrStarting()
        {
            if (arSession == null)
            {
                return true;
            }

            return ARSession.state == ARSessionState.SessionTracking
                || ARSession.state == ARSessionState.SessionInitializing;
        }

        private static string BuildSessionStatusMessage()
        {
            switch (ARSession.state)
            {
                case ARSessionState.Unsupported:
                    return "이 기기는 AR을 지원하지 않습니다.";
                case ARSessionState.NeedsInstall:
                    return "AR 기능을 사용하려면 ARCore를 설치해야 합니다.";
                case ARSessionState.Installing:
                    return "ARCore를 설치하는 중…";
                case ARSessionState.None:
                case ARSessionState.CheckingAvailability:
                case ARSessionState.Ready:
                    return "AR 세션을 준비하는 중…";
                default:
                    return "AR 세션을 준비하는 중…";
            }
        }
    }
}
