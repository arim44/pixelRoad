using System;
using System.Collections.Generic;
using PixelRoad.Data;
using PixelRoad.Geo;
using PixelRoad.Location;
using PixelRoad.Mapping;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PixelRoad.UI
{
    /// <summary>
    /// 지도 화면의 UI 전체를 묶어 다루는 뷰. 씬에 배치된 UI 프리팹 참조를 받아
    /// 마커·배너·도감·GNB를 갱신하고, 지도 입력과 라이브 지도 렌더러를 이어 준다.
    /// </summary>
    public sealed class PixelRoadRuntimeView
    {
        private const string PixelModePreferenceKey = "PixelRoad.MapPixelMode";

        /// <summary>유저 마커를 화면 가장자리에서 유지할 여유(px).</summary>
        private const float UserMarkerViewportPadding = 24f;

        /// <summary>터치 판정 크기를 계산할 때 쓰는 화면 DPI 대비 여유(인치). 약 1.5mm.</summary>
        private const float DragThresholdInches = 0.06f;

        /// <summary>현재 위치와 선택된 랜드마크를 잇는 실선의 두께(px).</summary>
        private const float DistanceLineThickness = 4f;

        /// <summary>아직 위치를 못 받은 상태를 나타내는 배너 거리 캐시 값.</summary>
        private const int WaitingForLocationDistance = int.MinValue + 1;

        /// <summary>배너 자물쇠 아이콘 이름. 잠김/해금에 서로 다른 아이콘을 쓴다.</summary>
        private const string LockedIconName = "lock";
        private const string UnlockedIconName = "unlock";

        /// <summary>잠긴 랜드마크 아이콘에 씌우는 틴트. 해금되면 흰색(원색)으로 돌아온다.</summary>
        private static readonly Color32 LockedIconTint = new Color32(124, 120, 112, 235);

        private static readonly Color32 CardButtonEnabledColor = new Color32(27, 139, 122, 255);
        private static readonly Color32 CardButtonDisabledColor = new Color32(142, 142, 142, 255);

        private readonly MapConfig config;
        private readonly Dictionary<string, MarkerBinding> markers = new Dictionary<string, MarkerBinding>();
        private readonly SpotIconLibrary iconLibrary;
        private readonly PixelRoadMapInput mapInput;
        private readonly int spotMarkerSize;
        private readonly int userMarkerSize;
        private readonly int markerTapMinimumSize;
        private readonly RectTransform viewport;
        private readonly RawImage liveMapImage;
        private readonly RectTransform markerRoot;
        private readonly ILiveMapController liveMapRenderer;
        private readonly Image userMarker;
        private readonly CodexView codex;
        private readonly TMP_Text mapNoticeText;
        private readonly GameObject landmarkBanner;
        private readonly GameObject distanceIndicator;
        private readonly RectTransform distanceLine;
        private readonly TMP_Text distanceText;
        private readonly GnbView gnb;
        private readonly QuitDialogView quitDialog;

        // GNB 배경은 지도 위에서만 반투명이다. 색은 프리팹 값을 그대로 읽어 두 벌로 캐시한다.
        private readonly Image gnbBackground;
        private readonly Color32 gnbMapColor;
        private readonly Color32 gnbOpaqueColor;

        // 자물쇠 아이콘은 배너를 갱신할 때마다 조회하지 않고 생성 시 한 번만 캐시한다.
        private readonly Sprite lockedIcon;
        private readonly Sprite unlockedIcon;
        private readonly PixelRoadUiBindings uiBindings;
        private bool pixelFilterEnabled;
        private bool mapRendered;
        private GeoLocation lastUserLocation;

        public event Action ARRequested;

        /// <summary>배너에 마지막으로 그린 남은거리(m). 같은 값이면 TMP를 다시 건드리지 않는다.</summary>
        private int lastBannerDistanceMeters = int.MinValue;

        /// <summary>지도가 현재 위치를 따라가는 중인지. 드래그하면 풀리고 재추적 버튼으로 다시 켠다.</summary>
        private bool followUser = true;

        /// <summary>마지막으로 지도 중심을 맞춘 좌표. 같은 좌표로 매 프레임 SetCenter 하지 않으려고 둔다.</summary>
        private double followedLatitude;
        private double followedLongitude;
        private bool hasFollowedOnce;

        /// <summary>지도 첫 타일이 실제로 그려졌을 때 한 번 발생한다.</summary>
        public event Action MapRendered;

        /// <summary>쓸 수 있는 GNB 탭을 눌렀을 때 발생한다.</summary>
        public event Action<GnbTab> GnbTabSelected;

        /// <summary>종료 확인 창에서 `확인`을 눌렀을 때 발생한다. 실제 종료는 앱이 결정한다.</summary>
        public event Action QuitConfirmed;

        /// <summary>
        /// 설정값과 프리팹 참조를 받아 UI 초기 상태를 잡고 지도 렌더러·입력·버튼 연결을 끝낸다.
        /// 생성 후에는 마커 추가와 상태 갱신만 하면 되도록 여기서 한 번에 준비한다.
        /// </summary>
        private PixelRoadRuntimeView(MapConfig config, PixelRoadUiBindings bindings)
        {
            if (bindings == null)
            {
                throw new ArgumentNullException(nameof(bindings));
            }

            this.config = config;
            spotMarkerSize = Mathf.Clamp(config.spotMarkerPixelSize, 8, 256);
            userMarkerSize = Mathf.Clamp(config.userMarkerPixelSize, 8, 256);
            markerTapMinimumSize = Mathf.Clamp(config.markerTapMinimumPixelSize, spotMarkerSize, 512);
            iconLibrary = new SpotIconLibrary(config.spotIconResourceFolder, config.defaultSpotIconName);
            pixelFilterEnabled = PlayerPrefs.HasKey(PixelModePreferenceKey)
                ? PlayerPrefs.GetInt(PixelModePreferenceKey, 0) != 0
                : config.enablePixelFilter;

            // UI는 씬에 배치된 PixelRoadUIRoot 프리팹 인스턴스가 전부다.
            // Canvas·EventSystem·마커 스프라이트를 포함해 여기서 만드는 오브젝트는 하나도 없다.
            uiBindings = bindings;
            uiBindings.ValidateReferences();
            ConfigureUiInput();
            viewport = uiBindings.MapViewport;
            liveMapImage = uiBindings.LiveMapImage;
            markerRoot = uiBindings.MarkerRoot;
            userMarker = uiBindings.UserMarker;
            mapNoticeText = uiBindings.MapNoticeText;
            landmarkBanner = uiBindings.LandmarkBanner;
            distanceIndicator = uiBindings.DistanceIndicator;
            distanceLine = uiBindings.DistanceLine;
            distanceText = uiBindings.DistanceText;
            gnb = uiBindings.Gnb;
            gnbBackground = gnb.GetComponent<Image>();
            if (gnbBackground != null)
            {
                gnbMapColor = gnbBackground.color;
                gnbOpaqueColor = new Color32(gnbMapColor.r, gnbMapColor.g, gnbMapColor.b, 255);
            }

            quitDialog = uiBindings.QuitDialog;
            lockedIcon = iconLibrary.Load(LockedIconName);
            unlockedIcon = iconLibrary.Load(UnlockedIconName);
            codex = uiBindings.CodexView;
            mapInput = uiBindings.MapInput;

            // 선택 상태는 GlobalValue에 있다. 도메인 리로드를 끄고 플레이할 때 이전 판의 값이 남지 않도록 비운다.
            GlobalValue.Clear();

            codex.Initialize();
            quitDialog.Initialize(HideQuitDialog, () => QuitConfirmed?.Invoke());
            uiBindings.RecenterButton.onClick.AddListener(StartFollowingUser);
            uiBindings.BannerCardButton.onClick.AddListener(ShowSelectedSpotDetail);
            mapInput.Dragged += Pan;
            mapInput.Zoomed += ZoomAt;
            mapInput.Tapped += DeselectSpot;
            liveMapImage.enabled = false;
            mapNoticeText.gameObject.SetActive(false);
            landmarkBanner.SetActive(false);
            distanceIndicator.SetActive(false);

            liveMapRenderer = CreateLiveMapRenderer(out string liveMapUnavailableReason);
            if (liveMapRenderer != null)
            {
                liveMapRenderer.SetPixelMode(pixelFilterEnabled);
            }

            // 아이콘을 못 찾으면 프리팹에 박아 둔 스프라이트를 그대로 둔다(런타임 생성 없음).
            Sprite userIcon = iconLibrary.Load(config.userIconName);
            if (userIcon != null)
            {
                userMarker.sprite = userIcon;
            }

            userMarker.rectTransform.sizeDelta = new Vector2(userMarkerSize, userMarkerSize);
            userMarker.preserveAspect = true;
            userMarker.raycastTarget = false;
            userMarker.gameObject.SetActive(false);

            gnb.Initialize();
            gnb.SetCurrent(GnbTab.Map);
            gnb.SetInteractable(GnbTab.Report, false);
            gnb.TabSelected += tab => GnbTabSelected?.Invoke(tab);
    
            uiBindings.AttributionText.text = string.IsNullOrWhiteSpace(config.mapAttribution)
                ? "© OpenStreetMap contributors"
                : config.mapAttribution;
            if (!string.IsNullOrWhiteSpace(config.mapAttributionUrl))
            {
                uiBindings.AttributionButton.onClick.AddListener(
                    () => Application.OpenURL(config.mapAttributionUrl));
            }
            else
            {
                uiBindings.AttributionButton.interactable = false;
            }

            ReportLiveMapAvailability(liveMapUnavailableReason);
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>지도 첫 타일이 이미 그려졌는지.</summary>
        public bool IsMapRendered
        {
            get { return mapRendered; }
        }

        /// <summary>씬에 배치된 UI 프리팹 인스턴스를 받아 뷰를 만든다.</summary>
        public static PixelRoadRuntimeView Create(MapConfig config, PixelRoadUiBindings bindings)
        {
            return new PixelRoadRuntimeView(config, bindings);
        }

        /// <summary>라이브 벡터 지도가 초기화되어 마커를 배치할 수 있는 상태인지.</summary>
        public bool IsMapAvailable
        {
            get { return liveMapRenderer != null; }
        }

        /// <summary>
        /// 라이브 벡터 지도 렌더러를 만든다.
        /// 정적 지도 폴백이 없으므로, 실패하면 이유를 돌려주고 화면에 안내를 띄운다.
        /// </summary>
        private ILiveMapController CreateLiveMapRenderer(out string unavailableReason)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || PIXELROAD_LIVE_VECTOR_MAP
            if (!ShouldEnableLiveVectorMap(config))
            {
                unavailableReason = config != null && !config.enableLiveVectorMap
                    ? "map_config.json 의 enableLiveVectorMap 이 false 입니다."
                    : "릴리스 빌드에서는 map_config.json 의 allowLiveVectorMapInRelease 가 true 여야 합니다.";
                return null;
            }

            LiveVectorMapRenderer renderer = viewport.gameObject.AddComponent<LiveVectorMapRenderer>();
            renderer.ViewChanged += UpdateLiveMarkerPositions;
            renderer.FirstTileReady += OnFirstLiveTileReady;
            if (renderer.Initialize(
                    config,
                    viewport,
                    liveMapImage,
                    config.editorStartLatitude,
                    config.editorStartLongitude))
            {
                unavailableReason = null;
                return renderer;
            }

            unavailableReason = renderer.LastError;
            renderer.ViewChanged -= UpdateLiveMarkerPositions;
            renderer.FirstTileReady -= OnFirstLiveTileReady;
            UnityEngine.Object.Destroy(renderer);
            return null;
#else
            unavailableReason =
                "이 빌드에는 라이브 지도 요청 코드가 포함되지 않았습니다. "
                + "PIXELROAD_LIVE_VECTOR_MAP 스크립팅 심볼이 필요합니다.";
            return null;
#endif
        }

        /// <summary>지도 준비 상태를 화면 안내 문구로 알린다. 사용할 수 없으면 이유를 함께 로그로 남긴다.</summary>
        private void ReportLiveMapAvailability(string unavailableReason)
        {
            if (liveMapRenderer != null)
            {
                SetMapNotice("지도를 불러오는 중…");
                return;
            }

            string reason = string.IsNullOrWhiteSpace(unavailableReason)
                ? "원인을 확인할 수 없습니다."
                : unavailableReason;
            SetMapNotice("지도를 표시할 수 없습니다.\n" + reason);
            Debug.LogError("[PixelRoad] 라이브 벡터 지도를 사용할 수 없습니다: " + reason);
        }

        /// <summary>지도 위 안내 문구를 바꾼다. 빈 문자열이나 null을 넘기면 문구를 감춘다.</summary>
        private void SetMapNotice(string message)
        {
            bool visible = !string.IsNullOrEmpty(message);
            if (visible)
            {
                mapNoticeText.text = message;
            }

            mapNoticeText.gameObject.SetActive(visible);
        }

        /// <summary>
        /// 랜드마크 하나를 지도 마커와 도감 카드로 함께 등록한다.
        /// 마커 탭과 카드 탭 모두 같은 콜백으로 이어진다.
        /// </summary>
        public void AddSpotMarker(SpotRuntimeState state, Action<SpotRuntimeState> onClick)
        {
            Sprite icon = iconLibrary.Resolve(state.Definition.IconKey, state.Definition.Category);
            LandmarkMarkerView markerView = UnityEngine.Object.Instantiate(
                uiBindings.LandmarkMarkerPrefab,
                markerRoot,
                false);
            markerView.gameObject.name = "Spot_" + state.Definition.Id;
            Image marker = markerView.Icon;
            if (icon != null)
            {
                marker.sprite = icon;
            }

            marker.preserveAspect = true;
            marker.rectTransform.sizeDelta = new Vector2(spotMarkerSize, spotMarkerSize);
            if (liveMapRenderer != null)
            {
                marker.rectTransform.anchoredPosition = liveMapRenderer.LatLonToViewportLocal(
                    state.Definition.Latitude,
                    state.Definition.Longitude);
            }
            else
            {
                // 지도가 없으면 좌표를 계산할 수 없으므로 마커를 숨긴다. 도감은 그대로 쓸 수 있다.
                marker.gameObject.SetActive(false);
            }

            // 아이콘 그림보다 넓은 터치 판정. raycastPadding은 음수가 확장이다.
            float padding = Mathf.Max(0f, (markerTapMinimumSize - spotMarkerSize) * 0.5f);
            marker.raycastPadding = new Vector4(-padding, -padding, -padding, -padding);

            // Button 대신 MapMarkerTapTarget을 쓰는 이유는 해당 클래스 주석 참고.
            // 요약: 상위 뷰포트가 pointerDrag를 가져가면 모바일에서 Button.onClick이 취소된다.
            MapMarkerTapTarget tapTarget = markerView.TapTarget;
            tapTarget.Initialize(mapInput);
            tapTarget.Tapped += () => onClick?.Invoke(state);

            markers[state.Definition.Id] = new MarkerBinding(
                marker,
                tapTarget,
                state.Definition.Latitude,
                state.Definition.Longitude);

            codex.AddCard(state, icon, onClick);
            UpdateSpotState(state);
        }

        /// <summary>
        /// 새 현재 위치를 반영해 추적·유저 마커·배너 남은거리·거리 표시를 한꺼번에 갱신한다.
        /// 배너 거리는 선택된 랜드마크가 있을 때만 다시 계산한다.
        /// </summary>
        public void UpdateUserLocation(GeoLocation location)
        {
            lastUserLocation = location;
            FollowUserIfNeeded();
            UpdateBannerDistance();
            if (!location.IsValid || liveMapRenderer == null)
            {
                userMarker.gameObject.SetActive(false);
                UpdateDistanceIndicator();
                return;
            }

            bool isVisible = liveMapRenderer.IsInViewport(
                location.Latitude,
                location.Longitude,
                UserMarkerViewportPadding);
            userMarker.gameObject.SetActive(isVisible);
            if (isVisible)
            {
                userMarker.rectTransform.anchoredPosition = liveMapRenderer.LatLonToViewportLocal(
                    location.Latitude,
                    location.Longitude);
            }

            UpdateDistanceIndicator();
        }

        /// <summary>지정한 좌표로 지도 중심을 옮긴다. 지도가 없으면 아무 일도 하지 않는다.</summary>
        public void CenterOnLocation(double latitude, double longitude)
        {
            if (liveMapRenderer != null)
            {
                liveMapRenderer.SetCenter(latitude, longitude);
            }
        }

        /// <summary>현재 위치 추적을 다시 켜고 즉시 중심을 맞춘다. 우하단 버튼이 호출한다.</summary>
        public void StartFollowingUser()
        {
            followUser = true;
            hasFollowedOnce = false;
            uiBindings.RecenterButton.gameObject.SetActive(false);
            if (lastUserLocation.IsValid)
            {
                FollowUserIfNeeded();
            }
        }

        /// <summary>사용자가 지도를 직접 움직였으므로 추적을 멈추고 재추적 버튼을 띄운다.</summary>
        private void StopFollowingUser()
        {
            if (!followUser)
            {
                return;
            }

            followUser = false;
            uiBindings.RecenterButton.gameObject.SetActive(true);
        }

        /// <summary>
        /// 추적 중이면 지도 중심을 현재 위치로 옮긴다.
        /// 좌표가 실제로 바뀌었을 때만 SetCenter를 불러서, 매 프레임 타일·마커 재계산이 돌지 않게 한다.
        /// </summary>
        private void FollowUserIfNeeded()
        {
            if (!followUser || liveMapRenderer == null || !lastUserLocation.IsValid)
            {
                return;
            }

            if (hasFollowedOnce
                && followedLatitude == lastUserLocation.Latitude
                && followedLongitude == lastUserLocation.Longitude)
            {
                return;
            }

            followedLatitude = lastUserLocation.Latitude;
            followedLongitude = lastUserLocation.Longitude;
            hasFollowedOnce = true;
            liveMapRenderer.SetCenter(lastUserLocation.Latitude, lastUserLocation.Longitude);
        }

        /// <summary>
        /// 랜드마크를 선택해 상단 알림 배너를 띄운다.
        /// 배너는 선택된 랜드마크가 있을 때만 보이고, 지도 빈 곳을 탭하면 사라진다.
        /// </summary>
        public void SelectSpot(SpotRuntimeState state, GeoLocation currentLocation)
        {
            if (state == null)
            {
                DeselectSpot();
                return;
            }

            GlobalValue.SelectedSpot = state;
            lastUserLocation = currentLocation.IsValid ? currentLocation : lastUserLocation;
            landmarkBanner.SetActive(true);
            ApplyBanner(state, currentLocation);
            UpdateDistanceIndicator();
        }

        /// <summary>선택을 해제하고 배너와 거리 표시를 감춘다.</summary>
        public void DeselectSpot()
        {
            if (GlobalValue.SelectedSpot == null)
            {
                return;
            }

            GlobalValue.SelectedSpot = null;
            landmarkBanner.SetActive(false);
            distanceIndicator.SetActive(false);
        }

        /// <summary>배너의 이름·자물쇠 아이콘·거리 문구와 `카드 보기` 버튼 상태를 선택된 랜드마크에 맞춘다.</summary>
        private void ApplyBanner(SpotRuntimeState state, GeoLocation currentLocation)
        {
            uiBindings.BannerNameText.text = state.Definition.DisplayName;

            // 잠김/해금은 아이콘 자체로 구분한다. 색은 아이콘 원색을 살리려고 흰색 고정이다.
            Sprite lockSprite = state.IsUnlocked ? unlockedIcon : lockedIcon;
            if (lockSprite != null)
            {
                uiBindings.BannerLockIcon.sprite = lockSprite;
            }

            // 거리 문구는 위치가 갱신될 때마다 다시 계산되므로 여기서는 값 캐시를 비워 강제로 한 번 그린다.
            lastBannerDistanceMeters = int.MinValue;
            UpdateBannerDistance();

            // 카드 보기는 해금된 랜드마크에서만 활성. 누르면 도감 카드와 같은 상세 팝업이 뜬다.
            bool unlocked = state.IsUnlocked;
            uiBindings.BannerCardButton.interactable = unlocked;
            uiBindings.BannerCardButtonBackground.color =
                unlocked ? CardButtonEnabledColor : CardButtonDisabledColor;
            Color labelColor = unlocked ? Color.white : (Color)new Color32(232, 232, 232, 255);
            uiBindings.BannerCardButtonLabel.color = labelColor;
            uiBindings.BannerCardButtonArrow.color = labelColor;
        }

        /// <summary>
        /// 배너의 남은거리 문구를 현재 위치 기준으로 다시 계산한다.
        /// 위치 갱신마다 불리므로 정수 미터가 바뀔 때만 TMP를 건드리고, 문자열은 무할당 SetText로 만든다.
        /// </summary>
        private void UpdateBannerDistance()
        {
            if (GlobalValue.SelectedSpot == null)
            {
                return;
            }

            if (!lastUserLocation.IsValid)
            {
                if (lastBannerDistanceMeters != WaitingForLocationDistance)
                {
                    lastBannerDistanceMeters = WaitingForLocationDistance;
                    uiBindings.BannerDistanceText.SetText("현재 위치를 확인하는 중…");
                }

                return;
            }

            double distance = GeoProjection.DistanceMeters(
                lastUserLocation.Latitude,
                lastUserLocation.Longitude,
                GlobalValue.SelectedSpot.Definition.Latitude,
                GlobalValue.SelectedSpot.Definition.Longitude);
            int meters = (int)System.Math.Round(distance);
            if (meters == lastBannerDistanceMeters)
            {
                return;
            }

            lastBannerDistanceMeters = meters;
            uiBindings.BannerDistanceText.SetText("현재위치에서 {0:0} m", meters);
        }

        /// <summary>
        /// 배너의 `카드 보기`. 도감 카드를 누른 것과 같은 상세 팝업을 지도 위에 그대로 띄운다.
        /// 아이콘은 <see cref="SpotIconLibrary"/>가 캐시하고 있어 다시 조회해도 Resources 접근이 없다.
        /// </summary>
        private void ShowSelectedSpotDetail()
        {
            if (GlobalValue.SelectedSpot == null || !GlobalValue.SelectedSpot.IsUnlocked)
            {
                return;
            }

            codex.ShowDetail(
                GlobalValue.SelectedSpot,
                iconLibrary.Resolve(GlobalValue.SelectedSpot.Definition.IconKey, GlobalValue.SelectedSpot.Definition.Category));
        }

        /// <summary>
        /// 현재 위치와 선택된 랜드마크를 잇는 실선과 남은거리 텍스트를 갱신한다.
        /// 지도 팬·줌 중에도 매 프레임 불리므로 새 오브젝트나 문자열을 만들지 않는다.
        /// </summary>
        private void UpdateDistanceIndicator()
        {
            if (GlobalValue.SelectedSpot == null || liveMapRenderer == null || !lastUserLocation.IsValid)
            {
                distanceIndicator.SetActive(false);
                return;
            }

            Vector2 from = liveMapRenderer.LatLonToViewportLocal(
                lastUserLocation.Latitude,
                lastUserLocation.Longitude);
            Vector2 to = liveMapRenderer.LatLonToViewportLocal(
                GlobalValue.SelectedSpot.Definition.Latitude,
                GlobalValue.SelectedSpot.Definition.Longitude);

            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length < 1f)
            {
                // 두 점이 겹치면 그릴 선이 없다.
                distanceIndicator.SetActive(false);
                return;
            }

            distanceIndicator.SetActive(true);
            distanceLine.anchoredPosition = from;
            distanceLine.sizeDelta = new Vector2(length, DistanceLineThickness);
            distanceLine.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            double distanceMeters = GeoProjection.DistanceMeters(
                lastUserLocation.Latitude,
                lastUserLocation.Longitude,
                GlobalValue.SelectedSpot.Definition.Latitude,
                GlobalValue.SelectedSpot.Definition.Longitude);
            distanceText.rectTransform.anchoredPosition = from + delta * 0.5f;

            // string.Format 대신 TMP의 무할당 SetText를 쓴다.
            distanceText.SetText("{0:0}m (남은거리)", (float)distanceMeters);
        }

        /// <summary>종료 확인 창이 떠 있는지.</summary>
        public bool IsQuitDialogVisible => quitDialog.IsVisible;

        /// <summary>종료 확인 창을 띄운다. 뒤로 가기에서 호출한다.</summary>
        public void ShowQuitDialog()
        {
            quitDialog.SetVisible(true);
        }

        /// <summary>종료 확인 창을 닫는다.</summary>
        public void HideQuitDialog()
        {
            quitDialog.SetVisible(false);
        }

        /// <summary>도감을 열고 닫는다. GNB 선택 표시와 배경 투명도도 함께 맞춰 준다.</summary>
        public void SetCodexVisible(bool visible)
        {
            codex.SetVisible(visible);

            // 도감을 닫으면 지도로 돌아오므로 GNB 선택 상태도 함께 맞춘다.
            gnb.SetCurrent(visible ? GnbTab.Codex : GnbTab.Map);
            ApplyGnbBackground(visible);
        }

        /// <summary>
        /// GNB 배경 투명도를 화면에 맞춘다.
        /// 지도에서는 지도가 비치도록 반투명, 도감처럼 꽉 찬 화면 위에서는 불투명으로 둔다.
        /// </summary>
        private void ApplyGnbBackground(bool panelOpen)
        {
            if (gnbBackground == null)
            {
                return;
            }

            gnbBackground.color = panelOpen ? gnbOpaqueColor : gnbMapColor;
        }

        /// <summary>도감이 열려 있는지. 뒤로 가기 처리에서 참고한다.</summary>
        public bool IsCodexVisible()
        {
            return codex.IsVisible;
        }

        /// <summary>도감 필터 칩을 수록 데이터에서 만든다. 마커를 모두 추가한 뒤 한 번만 호출한다.</summary>
        public void BuildCodexFilters(IList<SpotRuntimeState> spots)
        {
            codex.BuildFilters(spots);
        }

        /// <summary>도감 상단의 수집 진행도를 갱신한다.</summary>
        public void SetProgress(int unlocked, int total)
        {
            codex.SetProgress(unlocked, total);
        }

        /// <summary>
        /// AI 탐험 리포트 탭의 활성 여부와 알림 뱃지를 갱신한다.
        /// 해금이 하나도 없으면 비활성, 마지막 리포트 요청 시점과 해금 개수가 다르면 뱃지를 켠다.
        /// </summary>
        public void SetReportTabState(bool available, bool hasPendingUpdate)
        {
            gnb.SetInteractable(GnbTab.Report, available);
            gnb.SetBadgeVisible(GnbTab.Report, available && hasPendingUpdate);
        }

        /// <summary>해금 등 상태가 바뀐 랜드마크를 마커·배너·도감 카드에 한 번에 반영한다.</summary>
        public void UpdateSpotState(SpotRuntimeState state)
        {
            if (markers.TryGetValue(state.Definition.Id, out MarkerBinding marker))
            {
                ApplyMarkerVisual(marker.Image, state);
            }

            if (GlobalValue.SelectedSpot != null && GlobalValue.SelectedSpot.Definition.Id == state.Definition.Id)
            {
                ApplyBanner(state, lastUserLocation);
            }

            codex.UpdateCard(state);
        }

        /// <summary>
        /// 픽셀 필터를 켜고 끈다. 토글 버튼은 와이어프레임에 없어 UI에서 제거했고,
        /// 값은 map_config.json과 PlayerPrefs로만 관리한다.
        /// </summary>
        public void SetPixelFilter(bool enabled)
        {
            if (pixelFilterEnabled == enabled)
            {
                return;
            }

            pixelFilterEnabled = enabled;
            PlayerPrefs.SetInt(PixelModePreferenceKey, pixelFilterEnabled ? 1 : 0);
            PlayerPrefs.Save();
            ApplyPixelFilter();
        }

        /// <summary>현재 픽셀 필터 설정을 지도 렌더러에 전달한다.</summary>
        private void ApplyPixelFilter()
        {
            if (liveMapRenderer != null)
            {
                liveMapRenderer.SetPixelMode(pixelFilterEnabled);
            }
        }

        /// <summary>드래그 입력을 지도 이동으로 넘긴다. 사용자가 직접 움직였으므로 위치 추적은 해제한다.</summary>
        private void Pan(Vector2 delta)
        {
            StopFollowingUser();
            if (liveMapRenderer != null)
            {
                liveMapRenderer.Pan(delta);
            }
        }

        /// <summary>휠·핀치 입력을 지도 줌으로 넘긴다.</summary>
        private void ZoomAt(float factor, Vector2 screenPosition)
        {
            if (liveMapRenderer != null)
            {
                liveMapRenderer.ZoomAt(factor, screenPosition);
            }
        }

        /// <summary>첫 타일이 그려지면 로딩 안내를 지우고 마커를 배치한 뒤 한 번만 알린다.</summary>
        private void OnFirstLiveTileReady()
        {
            if (mapRendered)
            {
                return;
            }

            mapRendered = true;
            SetMapNotice(null);
            UpdateLiveMarkerPositions();
            MapRendered?.Invoke();
        }

        /// <summary>
        /// 지도 시점이 바뀔 때마다 모든 마커와 유저 마커, 거리 표시의 화면 좌표를 다시 계산한다.
        /// 팬·줌 중 계속 불리므로 좌표 변환 외의 작업은 넣지 않는다.
        /// </summary>
        private void UpdateLiveMarkerPositions()
        {
            if (liveMapRenderer == null)
            {
                return;
            }

            foreach (KeyValuePair<string, MarkerBinding> pair in markers)
            {
                MarkerBinding marker = pair.Value;
                marker.Image.rectTransform.anchoredPosition = liveMapRenderer.LatLonToViewportLocal(
                    marker.Latitude,
                    marker.Longitude);
            }

            if (lastUserLocation.IsValid)
            {
                bool visible = liveMapRenderer.IsInViewport(
                    lastUserLocation.Latitude,
                    lastUserLocation.Longitude,
                    UserMarkerViewportPadding);
                userMarker.gameObject.SetActive(visible);
                if (visible)
                {
                    userMarker.rectTransform.anchoredPosition = liveMapRenderer.LatLonToViewportLocal(
                        lastUserLocation.Latitude,
                        lastUserLocation.Longitude);
                }
            }

            UpdateDistanceIndicator();
        }

        /// <summary>AR 버튼 진입점. 실제 화면 전환은 ARRequested 구독자(PixelRoadApp)가 담당한다.</summary>
        public void OnClickARBtn()
        {
            ARRequested?.Invoke();
        }

        /// <summary>AR 진입 조건(GPS 등)이 아직 안 됐을 때 지도 위 안내 문구로 알린다.</summary>
        public void SetLocationStatus(string message)
        {
            SetMapNotice(message);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || PIXELROAD_LIVE_VECTOR_MAP
        /// <summary>
        /// 라이브 벡터 지도를 켜도 되는지 판단한다.
        /// 에디터에서는 항상 허용하고, 릴리스 빌드는 설정에서 명시적으로 열어 줘야 한다.
        /// </summary>
        private static bool ShouldEnableLiveVectorMap(MapConfig mapConfig)
        {
            if (mapConfig == null || !mapConfig.enableLiveVectorMap)
            {
                return false;
            }

#if UNITY_EDITOR
            return true;
#else
            return Debug.isDebugBuild || mapConfig.allowLiveVectorMapInRelease;
#endif
        }
#endif

        /// <summary>
        /// 해금 상태를 마커·카드 아이콘에 반영한다.
        /// 스프라이트는 프리팹과 Resources 아이콘이 담당하므로 여기서는 틴트만 바꾼다.
        /// </summary>
        private static void ApplyMarkerVisual(Image image, SpotRuntimeState state)
        {
            image.color = state.IsUnlocked ? Color.white : (Color)LockedIconTint;
        }

        /// <summary>
        /// 프리팹이 들고 온 EventSystem을 씬 상황에 맞춘다.
        ///
        /// 씬에 이미 EventSystem이 있으면(예: 손으로 구성한 씬) 프리팹 쪽을 지운다. Unity는 활성
        /// EventSystem이 둘이면 경고를 내고 한쪽 입력만 처리한다.
        /// </summary>
        private void ConfigureUiInput()
        {
            EventSystem prefabEventSystem = uiBindings.EventSystem;
            EventSystem active = EventSystem.current;
            if (active != null && active != prefabEventSystem)
            {
                UnityEngine.Object.Destroy(prefabEventSystem.gameObject);
            }
            else
            {
                active = prefabEventSystem;
            }

            // 기본 드래그 임계값 10px는 고DPI 단말에서 약 0.6mm라, 손가락이 조금만 흔들려도
            // 탭이 드래그로 승격되면서 버튼/카드 클릭 판정이 취소된다. 화면 DPI에 맞춰 넓혀 준다.
            if (Screen.dpi > 1f)
            {
                active.pixelDragThreshold = Mathf.Max(
                    active.pixelDragThreshold,
                    Mathf.RoundToInt(Screen.dpi * DragThresholdInches));
            }
        }

        /// <summary>마커 이미지와 탭 타깃, 원래 위경도를 함께 묶어 둔다. 시점이 바뀔 때 좌표를 다시 계산하는 데 쓴다.</summary>
        private sealed class MarkerBinding
        {
            public readonly Image Image;
            public readonly MapMarkerTapTarget TapTarget;
            public readonly double Latitude;
            public readonly double Longitude;

            public MarkerBinding(
                Image image,
                MapMarkerTapTarget tapTarget,
                double latitude,
                double longitude)
            {
                Image = image;
                TapTarget = tapTarget;
                Latitude = latitude;
                Longitude = longitude;
            }
        }

    }
}
