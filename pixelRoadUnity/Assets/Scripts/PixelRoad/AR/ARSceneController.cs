using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using PixelRoad.Data;
using PixelRoad.Geo;
using PixelRoad.Location;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

namespace PixelRoad.AR
{
    /// <summary>
    /// ARScene의 진입점. ARHandoff로 받은 랜드마크/현재 위치를 바탕으로 위치·나침반을 갱신하고,
    /// 매 프레임 랜드마크별 베어링·거리를 계산해 AROverlayView에 표시를 위임한다.
    /// </summary>
    public sealed class ARSceneController : MonoBehaviour
    {
        private const string MapSceneName = "MapScene";
        private const float NoLandmarksTimeoutSeconds = 5f;

        [SerializeField]
        private Camera arCamera;

        [SerializeField]
        private RectTransform overlayRoot;

        [SerializeField]
        private ARSession arSession;

        private ARConfig config;
        private AROverlayView view;
        private ILocationProvider locationProvider;
        private IHeadingProvider headingProvider;
        private GeoLocationSample currentLocation;
        private float smoothedHeading;
        private bool hasSmoothedHeading;
        private bool ready;
        private float noLandmarksTimer;
        private bool returningToMap;

        private IEnumerator Start()
        {
            config = ARSceneLauncher.LoadConfig();
            view = new AROverlayView(overlayRoot, config);
            view.CaptureRequested += OnCaptureRequested;
            currentLocation = ARHandoff.InitialLocation;

#if UNITY_EDITOR
            locationProvider = new SimulatedLocationProvider(currentLocation.Latitude, currentLocation.Longitude);
            headingProvider = new SimulatedHeadingProvider();
#else
            // UnityGpsLocationProvider는 지도 화면과 공유하는 MapConfig를 그대로 받는다(변경하지 않기 위해).
            // ARScene은 MapConfig에 의존하지 않으므로, ARConfig 값만 옮겨 담은 임시 인스턴스를 만들어 넘긴다.
            MapConfig gpsConfig = new MapConfig
            {
                desiredAccuracyMeters = config.desiredAccuracyMeters,
                locationUpdateDistanceMeters = config.locationUpdateDistanceMeters
            };
            locationProvider = new UnityGpsLocationProvider(gpsConfig);
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
            if (view != null)
            {
                view.CaptureRequested -= OnCaptureRequested;
            }

            locationProvider?.Stop();
            headingProvider?.Stop();
        }

        private void OnCaptureRequested()
        {
            StartCoroutine(CaptureScreenshotRoutine());
        }

        /// <summary>
        /// 촬영 버튼만 감춘 채 현재 프레임(카메라 패스스루 + 랜드마크 오버레이)을 캡처해 갤러리에 저장한다.
        /// </summary>
        private IEnumerator CaptureScreenshotRoutine()
        {
            view.SetCaptureButtonVisible(false);
            yield return new WaitForEndOfFrame();

            Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            view.SetCaptureButtonVisible(true);

            byte[] pngBytes = screenshot.EncodeToPNG();
            Destroy(screenshot);

            string fileName = "PixelRoad_AR_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".png";
            AndroidGalleryExporter.SaveScreenshot(pngBytes, fileName, config.screenshotGalleryFolder);
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
            float horizontalFov = ARCompassMath.HorizontalFovDegrees(arCamera);
            float halfCanvasWidth = overlayRoot.rect.width * 0.5f;
            int leftEdgeCount = 0;
            int rightEdgeCount = 0;
            int visibleCount = 0;

            IReadOnlyList<ARLandmarkSnapshot> landmarks = ARHandoff.Landmarks;
            for (int i = 0; i < landmarks.Count; i++)
            {
                ARLandmarkSnapshot landmark = landmarks[i];
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

                visibleCount++;
                double bearing = GeoProjection.BearingDegrees(
                    currentLocation.Latitude,
                    currentLocation.Longitude,
                    landmark.Latitude,
                    landmark.Longitude);
                float delta = ARCompassMath.NormalizeAngle((float)bearing - smoothedHeading);

                if (Mathf.Abs(delta) <= horizontalFov * 0.5f)
                {
                    float screenX = ARCompassMath.DeltaToScreenX(delta, horizontalFov, halfCanvasWidth);
                    view.ShowOnScreen(landmark, screenX, distance);
                }
                else
                {
                    bool rightSide = delta > 0f;
                    int slotIndex = rightSide ? rightEdgeCount++ : leftEdgeCount++;
                    view.ShowAtEdge(landmark, rightSide, distance, slotIndex);
                }
            }

            HandleNoLandmarksState(visibleCount);
        }

        /// <summary>
        /// 표시 반경 안에 랜드마크가 하나도 없는 상태가 이어지면 경고 문구를 깜빡이며 보여주다가
        /// NoLandmarksTimeoutSeconds가 지나면 지도 화면으로 돌아간다.
        /// </summary>
        private void HandleNoLandmarksState(int visibleLandmarkCount)
        {
            if (visibleLandmarkCount > 0)
            {
                noLandmarksTimer = 0f;
                view.SetNoLandmarksWarning(false, 0f);
                return;
            }

            noLandmarksTimer += Time.deltaTime;
            view.SetNoLandmarksWarning(true, noLandmarksTimer);

            if (!returningToMap && noLandmarksTimer >= NoLandmarksTimeoutSeconds)
            {
                returningToMap = true;
                ARHandoff.Clear();
                SceneManager.LoadScene(MapSceneName);
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
