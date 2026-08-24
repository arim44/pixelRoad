using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using PixelRoad.Data;
using PixelRoad.Geo;
using PixelRoad.Location;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.ARFoundation;

namespace PixelRoad.AR
{
    /// <summary>
    /// ARScene의 진입점. ARHandoff로 받은 랜드마크/현재 위치를 바탕으로 위치·나침반을 갱신하고,
    /// 매 프레임 랜드마크별 베어링·거리를 계산해 AROverlayView에 표시를 위임한다.
    /// </summary>
    public sealed class ARSceneController : MonoBehaviour
    {
        private const float NoLandmarksTimeoutSeconds = 5f;
        private const float ToastDisplaySeconds = 3f;
        private const float LoadingFadeOutSeconds = 0.25f;

        [SerializeField]
        private Camera arCamera;

        [SerializeField]
        private AROverlayUiBindings uiBindings;

        [SerializeField]
        private ARSession arSession;

        private ARConfig config;
        private AROverlayView view;
        private ILocationProvider locationProvider;
        private IHeadingProvider headingProvider;
        private GeoLocation currentLocation;
        private float smoothedHeading;
        private bool hasSmoothedHeading;
        private bool ready;
        private float noLandmarksTimer;
        private bool returningToMap;
        private Texture2D lastThumbnailTexture;
        private string lastCaptureUri;
        private Coroutine toastHideRoutine;
        private VisitRepository visitRepository;

        /// <summary>이번 AR 세션에서 이미 해금 처리(또는 원래 해금 상태)로 확인한 랜드마크 id.
        /// 매 프레임 같은 랜드마크를 다시 조회·기록하지 않으려고 둔다.</summary>
        private readonly HashSet<string> unlockedLandmarkIds = new HashSet<string>();

        private IEnumerator Start()
        {
            config = ARSceneLauncher.LoadConfig();
            view = new AROverlayView(uiBindings, config);
            view.CaptureRequested += OnCaptureRequested;
            view.ThumbnailClicked += OnThumbnailClicked;
            view.LandmarkClicked += OnLandmarkClicked;
            view.BackRequested += OnBackRequested;
            RestoreLastCapture();

            // AR 화면 UI가 다 세워진 지금 로딩 화면을 이어받아 페이드아웃시킨다. MapScene에서 미리
            // 끄면 씬이 바뀌기 전에 지도 화면이 잠깐 다시 보여 깜빡이는 것처럼 보이는 문제가 있었다.
            ARHandoff.PendingLoadingScreen?.FadeOutAndDestroy(LoadingFadeOutSeconds);
            ARHandoff.PendingLoadingScreen = null;
            currentLocation = ARHandoff.InitialLocation;

            // MapScene과 같은 파일(visited_landmarks.json)을 읽고 쓴다 - 별도 인스턴스여도 저장소가
            // 파일 기반이라 지도로 돌아가면 새로 만들어지는 PixelRoadApp의 VisitRepository가 그대로 읽는다.
            visitRepository = new VisitRepository();
            IReadOnlyList<ARLandmarkSnapshot> initialLandmarks = ARHandoff.Landmarks;
            for (int i = 0; i < initialLandmarks.Count; i++)
            {
                if (initialLandmarks[i].IsUnlocked)
                {
                    unlockedLandmarkIds.Add(initialLandmarks[i].Id);
                }
            }

#if UNITY_EDITOR
            // TrackedPoseDriver는 실기기 추적 데이터를 매 프레임 카메라에 그대로 덮어써서, 에디터에서
            // SimulatedHeadingProvider가 마우스/키로 돌려놓은 회전을 렌더 직전에 다시 지워 버린다
            // (실기기가 없으니 추적할 게 없는데도 동작해 버림). 에디터에서는 꺼서 시뮬레이션이 그대로 반영되게 한다.
            TrackedPoseDriver trackedPoseDriver = arCamera.GetComponent<TrackedPoseDriver>();
            if (trackedPoseDriver != null)
            {
                trackedPoseDriver.enabled = false;
            }

            locationProvider = new SimulatedLocationProvider(
                currentLocation.Latitude, currentLocation.Longitude, headingSource: arCamera.transform);
            headingProvider = new SimulatedHeadingProvider(arCamera.transform);
#else
            // UnityGpsLocationProvider는 지도 화면과 공유하는 MapConfig를 그대로 받는다(변경하지 않기 위해).
            // ARScene은 MapConfig에 의존하지 않으므로, ARConfig 값만 옮겨 담은 임시 인스턴스를 만들어 넘긴다.
            MapConfig gpsConfig = new MapConfig
            {
                desiredAccuracyMeters = config.desiredAccuracyMeters,
                locationUpdateDistanceMeters = config.locationUpdateDistanceMeters
            };
            locationProvider = new UnityGpsLocationProvider(gpsConfig);
            float? seedHeading = ARHandoff.HasInitialHeading ? (float?)ARHandoff.InitialHeadingDegrees : null;
            headingProvider = new FusedHeadingProvider(arCamera.transform, seedHeading);
#endif
            yield return StartCoroutine(locationProvider.Start());
            headingProvider.Start();
            ready = true;
        }

        private void Update()
        {
            if (!ready || view == null || returningToMap)
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
                view.ThumbnailClicked -= OnThumbnailClicked;
                view.LandmarkClicked -= OnLandmarkClicked;
                view.BackRequested -= OnBackRequested;
            }

            locationProvider?.Stop();
            headingProvider?.Stop();
            DestroyLastThumbnailTexture();
        }

        /// <summary>이전 세션에 찍어 둔 캡처가 있으면 복원해서 썸네일에 바로 보여준다.</summary>
        private void RestoreLastCapture()
        {
            if (AndroidGalleryExporter.TryLoadCachedCapture(out Texture2D cachedTexture, out string cachedUri))
            {
                lastCaptureUri = cachedUri;
                ShowThumbnail(cachedTexture);
            }
        }

        private void OnCaptureRequested()
        {
            StartCoroutine(CaptureScreenshotRoutine());
        }

        private void OnBackRequested()
        {
            ReturnToMapScene();
        }

        /// <summary>
        /// 로딩 화면을 띄우며 MapScene으로 돌아간다. 뒤로가기와 근처 랜드마크 없음 타임아웃이 함께 쓴다.
        /// returningToMap을 여기서 먼저 세워 둬야, ARHandoff.Clear()로 랜드마크 목록이 비워진 뒤에도
        /// (씬이 실제로 언로드되기 전까지) Update가 계속 돌면서 "근처에 랜드마크가 없다"고 오판하지 않는다.
        /// </summary>
        private void ReturnToMapScene()
        {
            // 뒤로가기를 연달아 누르거나 뒤로가기와 근처 랜드마크 없음 타임아웃이 겹치면 여기가 두 번
            // 호출될 수 있다 - 그러면 로딩 화면(LoadingScreenView.Create)이 두 번 만들어지고
            // ARHandoff.PendingLoadingScreen이 그중 하나만 가리키게 돼, 남은 하나가 고아로 남거나
            // 이미 파괴된 CanvasGroup을 다시 페이드아웃하려다 MissingReferenceException이 났다.
            if (returningToMap)
            {
                return;
            }

            returningToMap = true;
            ARHandoff.Clear();
            StartCoroutine(ARSceneLauncher.LoadMapScene());
        }

        private void OnThumbnailClicked()
        {
            // Application.OpenURL은 실패해도 예외를 던지지 않는 경우가 많아 "열기 실패 = 원본 삭제됨"으로
            // 단정할 수 없다. 그래서 열기 실패 시 캐시를 지우던 이전 로직은 제거했다 - 여기서는 토스트만 보여준다.
            if (!AndroidGalleryExporter.OpenInGallery(lastCaptureUri, out string errorMessage))
            {
                ShowToast(string.IsNullOrEmpty(errorMessage) ? "사진을 열 수 없습니다." : "사진을 열 수 없습니다: " + errorMessage);
            }
        }

        /// <summary>
        /// 랜드마크 핀을 누르면 GlobalValue.SelectedSpot에 담아 집중 모드(다른 핀은 숨김)로 들어간다.
        /// 이미 선택된 핀을 다시 누르면 선택을 풀어 집중 모드를 빠져나온다.
        /// GlobalValue는 지도 화면과 공유하는 전역 선택 상태라, 지도로 돌아가면 그 랜드마크가 그대로 선택돼 있다.
        /// </summary>
        private void OnLandmarkClicked(string landmarkId)
        {
            if (GlobalValue.SelectedSpot != null && GlobalValue.SelectedSpot.Definition.Id == landmarkId)
            {
                GlobalValue.SelectedSpot = null;
                return;
            }

            GlobalValue.SelectedSpot = FindSpotById(landmarkId);
        }

        /// <summary>
        /// 랜드마크 방문 반경 안에 처음 들어왔을 때 호출한다. VisitRepository에 기록하고(오늘 이미
        /// 기록됐으면 false), 성공하면 실제 SpotRuntimeState를 해금 처리한 뒤 핀 아이콘 색도 갱신한다.
        /// </summary>
        private void TryUnlockLandmark(ARLandmarkSnapshot landmark)
        {
            SpotRuntimeState spotState = FindSpotById(landmark.Id);
            if (spotState == null || !visitRepository.RecordVisit(spotState.Definition.LandmarkId, DateTime.Now))
            {
                return;
            }

            spotState.Unlock();
            unlockedLandmarkIds.Add(landmark.Id);
            view.SetLandmarkUnlocked(landmark.Id);

            // MapScene과 똑같은 "랜드마크 발견!" 창을 그대로 재사용한다 - 화면마다 다른 알림 방식을 두지 않는다.
            view.ShowUnlockDialog(spotState.Definition);
        }

        private static SpotRuntimeState FindSpotById(string landmarkId)
        {
            IReadOnlyList<SpotRuntimeState> spots = ARHandoff.Spots;
            for (int i = 0; i < spots.Count; i++)
            {
                if (spots[i].Definition.Id == landmarkId)
                {
                    return spots[i];
                }
            }

            return null;
        }

        private void ShowToast(string message)
        {
            if (toastHideRoutine != null)
            {
                StopCoroutine(toastHideRoutine);
            }

            view.ShowToast(message);
            toastHideRoutine = StartCoroutine(HideToastAfterDelay());
        }

        private IEnumerator HideToastAfterDelay()
        {
            yield return new WaitForSeconds(ToastDisplaySeconds);
            view.HideToast();
            toastHideRoutine = null;
        }

        /// <summary>
        /// 촬영 버튼과 뒤로가기 버튼만 감춘 채 현재 프레임(카메라 패스스루 + 랜드마크 오버레이)을 캡처해 갤러리에 저장하고,
        /// 다음 실행에서도 복원할 수 있도록 로컬에 캐시한 뒤, 화면 오른쪽 아래에 미리보기 썸네일을 띄운다.
        /// </summary>
        private IEnumerator CaptureScreenshotRoutine()
        {
            view.SetCaptureUiVisible(false);
            yield return new WaitForEndOfFrame();

            Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            view.SetCaptureUiVisible(true);

            byte[] pngBytes = screenshot.EncodeToPNG();
            string fileName = "PixelRoad_AR_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".png";
            string uriString = AndroidGalleryExporter.SaveScreenshot(pngBytes, fileName, config.screenshotGalleryFolder);

            lastCaptureUri = uriString;
            AndroidGalleryExporter.CacheLastCapture(pngBytes, uriString);

            ShowThumbnail(screenshot);
        }

        /// <summary>썸네일에 텍스처를 띄운다. 자동으로 사라지지 않고, 다음 캡처가 있을 때까지(또는 씬 종료까지) 계속 보인다.</summary>
        private void ShowThumbnail(Texture2D texture)
        {
            DestroyLastThumbnailTexture();

            lastThumbnailTexture = texture;
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));
            view.ShowCapturedThumbnail(sprite);
        }

        private void DestroyLastThumbnailTexture()
        {
            if (lastThumbnailTexture != null)
            {
                Destroy(lastThumbnailTexture);
                lastThumbnailTexture = null;
            }
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
            float verticalFov = arCamera.fieldOfView;
            float halfCanvasWidth = uiBindings.LandmarkRoot.rect.width * 0.5f;
            float halfCanvasHeight = uiBindings.LandmarkRoot.rect.height * 0.5f;

            // 랜드마크는 고도 정보가 없어 전부 "지평선" 높이에 있다고 가정한다.
            // 카메라를 위/아래로 기울인 만큼(피치) 지평선이 화면에서 반대로 움직이는 것과 같은 효과를 준다.
            float pitchDegrees = ARCompassMath.NormalizeAngle(arCamera.transform.eulerAngles.x);
            float screenY = ARCompassMath.DeltaToScreenOffset(pitchDegrees, verticalFov, halfCanvasHeight);

            int leftEdgeCount = 0;
            int rightEdgeCount = 0;
            int visibleCount = 0;

            // 집중 모드: 랜드마크 핀을 눌러 GlobalValue.SelectedSpot이 채워져 있으면 그 랜드마크만 보여주고
            // 나머지는 숨긴다. 화면 중앙 아래 나침반 화살표도 이때만 그 방향을 가리키며 보인다.
            string focusedLandmarkId = GlobalValue.SelectedSpot?.Definition.Id;
            bool focusDirectionShown = false;

            IReadOnlyList<ARLandmarkSnapshot> landmarks = ARHandoff.Landmarks;
            for (int i = 0; i < landmarks.Count; i++)
            {
                ARLandmarkSnapshot landmark = landmarks[i];
                double distance = GeoProjection.DistanceMeters(
                    currentLocation.Latitude,
                    currentLocation.Longitude,
                    landmark.Latitude,
                    landmark.Longitude);

                // 해금 판정은 화면 표시 여부(집중 모드로 숨겨져 있는지, 표시 반경 밖인지)와 무관하게
                // 항상 확인한다 - 다른 랜드마크에 집중한 채로 걸어가도 실제 반경에 들어오면 해금돼야 한다.
                if (!unlockedLandmarkIds.Contains(landmark.Id) && distance <= landmark.RadiusMeters)
                {
                    TryUnlockLandmark(landmark);
                }

                if (focusedLandmarkId != null && landmark.Id != focusedLandmarkId)
                {
                    view.Hide(landmark.Id);
                    continue;
                }

                // 방문(해금) 반경 안에 들어와 있으면 표시 반경을 살짝 벗어나도 계속 보여준다 -
                // 그렇지 않으면 랜드마크 코앞까지 걸어가는 도중에 핀이 먼저 꺼져 버릴 수 있다.
                if (distance > config.arDisplayRadiusMeters + landmark.RadiusMeters)
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

                if (focusedLandmarkId != null)
                {
                    view.ShowFocusDirection(delta);
                    focusDirectionShown = true;
                }

                if (Mathf.Abs(delta) <= horizontalFov * 0.5f)
                {
                    float screenX = ARCompassMath.DeltaToScreenOffset(delta, horizontalFov, halfCanvasWidth);
                    view.ShowOnScreen(landmark, screenX, screenY, distance);
                }
                else
                {
                    bool rightSide = delta > 0f;
                    int slotIndex = rightSide ? rightEdgeCount++ : leftEdgeCount++;
                    view.ShowAtEdge(landmark, rightSide, screenY, distance, slotIndex);
                }
            }

            if (!focusDirectionShown)
            {
                view.HideFocusDirection();
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
                ReturnToMapScene();
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
