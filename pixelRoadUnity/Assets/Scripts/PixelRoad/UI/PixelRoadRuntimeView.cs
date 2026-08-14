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

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace PixelRoad.UI
{
    public sealed class PixelRoadRuntimeView
    {
        private const string PixelModePreferenceKey = "PixelRoad.MapPixelMode";
        private const string UiPrefabResourcePath = "PixelRoad/UI/PixelRoadUIRoot";

        /// <summary>유저 마커를 화면 가장자리에서 유지할 여유(px).</summary>
        private const float UserMarkerViewportPadding = 24f;

        /// <summary>도감 카드 썸네일 크기. 지도 마커 크기와 무관하게 고정.</summary>
        private const int CodexIconSize = 42;

        /// <summary>터치 판정 크기를 계산할 때 쓰는 화면 DPI 대비 여유(인치). 약 1.5mm.</summary>
        private const float DragThresholdInches = 0.06f;

        private static readonly Color32 UnlockedMarkerColor = new Color32(208, 56, 48, 255);
        private static readonly Color32 LockedMarkerColor = new Color32(70, 67, 61, 255);
        private static readonly Color32 LockedIconTint = new Color32(124, 120, 112, 235);
        private static readonly Color32 UserMarkerColor = new Color32(52, 122, 255, 255);

        private readonly MapConfig config;
        private readonly Dictionary<string, MarkerBinding> markers = new Dictionary<string, MarkerBinding>();
        private readonly Dictionary<string, CodexBinding> codexCards = new Dictionary<string, CodexBinding>();
        private readonly Dictionary<ShapeKey, Sprite> shapeSprites = new Dictionary<ShapeKey, Sprite>();
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
        private readonly GameObject codexPanel;
        private readonly TMP_Text statusText;
        private readonly TMP_Text mapNoticeText;
        private readonly TMP_Text progressText;
        private readonly TMP_Text selectedNameText;
        private readonly TMP_Text selectedDescriptionText;
        private readonly TMP_Text selectedDistanceText;
        private readonly Button pixelToggleButton;
        private readonly TMP_Text pixelToggleText;
        private readonly TMP_FontAsset pixelFont;
        private readonly PixelRoadUiBindings uiBindings;
        private bool pixelFilterEnabled;
        private bool mapRendered;
        private GeoLocationSample lastUserLocation;
        private int unlockedCount;
        private int totalCount;

        public event Action CodexRequested;
        public event Action ARRequested;

        private PixelRoadRuntimeView(MapConfig config)
        {
            this.config = config;
            spotMarkerSize = Mathf.Clamp(config.spotMarkerPixelSize, 8, 256);
            userMarkerSize = Mathf.Clamp(config.userMarkerPixelSize, 8, 256);
            markerTapMinimumSize = Mathf.Clamp(config.markerTapMinimumPixelSize, spotMarkerSize, 512);
            iconLibrary = new SpotIconLibrary(config.spotIconResourceFolder, config.defaultSpotIconName);
            pixelFilterEnabled = PlayerPrefs.HasKey(PixelModePreferenceKey)
                ? PlayerPrefs.GetInt(PixelModePreferenceKey, 0) != 0
                : config.enablePixelFilter;

            Canvas canvas = GetOrCreateCanvas();
            EnsureUiInput(canvas);
            pixelFont = null;

            uiBindings = UnityEngine.Object.FindFirstObjectByType<PixelRoadUiBindings>(FindObjectsInactive.Include);
            if (uiBindings == null)
            {
                GameObject uiPrefab = Resources.Load<GameObject>(UiPrefabResourcePath);
                if (uiPrefab == null)
                {
                    throw new InvalidOperationException(
                        "Missing Resources/" + UiPrefabResourcePath + ".prefab. "
                        + "Run Tools > Pixel Road > Rebuild UI Prefabs in the Unity Editor.");
                }

                GameObject uiObject = UnityEngine.Object.Instantiate(uiPrefab, canvas.transform, false);
                uiObject.name = "PixelRoadUIRoot";
                uiBindings = uiObject.GetComponent<PixelRoadUiBindings>();
            }

            uiBindings.ValidateReferences();
            viewport = uiBindings.MapViewport;
            liveMapImage = uiBindings.LiveMapImage;
            markerRoot = uiBindings.MarkerRoot;
            userMarker = uiBindings.UserMarker;
            mapNoticeText = uiBindings.MapNoticeText;
            statusText = uiBindings.StatusText;
            progressText = uiBindings.ProgressText;
            selectedNameText = uiBindings.SelectedNameText;
            selectedDescriptionText = uiBindings.SelectedDescriptionText;
            selectedDistanceText = uiBindings.SelectedDistanceText;
            pixelToggleButton = uiBindings.PixelToggleButton;
            pixelToggleText = uiBindings.PixelToggleText;
            codexPanel = uiBindings.CodexPanel;
            mapInput = uiBindings.MapInput;

            mapInput.Dragged += Pan;
            mapInput.Zoomed += ZoomAt;
            liveMapImage.enabled = false;
            mapNoticeText.gameObject.SetActive(false);

            liveMapRenderer = CreateLiveMapRenderer(out string liveMapUnavailableReason);
            if (liveMapRenderer != null)
            {
                liveMapRenderer.SetPixelMode(pixelFilterEnabled);
            }

            Sprite userIcon = iconLibrary.Load(config.userIconName);
            userMarker.sprite = userIcon != null
                ? userIcon
                : GetShapeSprite(true, userMarkerSize, UserMarkerColor);
            userMarker.rectTransform.sizeDelta = new Vector2(userMarkerSize, userMarkerSize);
            userMarker.preserveAspect = true;
            userMarker.raycastTarget = false;
            userMarker.gameObject.SetActive(false);

            uiBindings.TitleText.text = config.appTitle;
            uiBindings.CodexButton.onClick.AddListener(() => CodexRequested?.Invoke());
            pixelToggleButton.onClick.AddListener(TogglePixelFilter);
            uiBindings.CodexCloseButton.onClick.AddListener(() => SetCodexVisible(false));

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

            codexPanel.SetActive(false);
            UpdatePixelToggleText();
            ReportLiveMapAvailability(liveMapUnavailableReason);
            Canvas.ForceUpdateCanvases();
        }

        public static PixelRoadRuntimeView Create(MapConfig config)
        {
            return new PixelRoadRuntimeView(config);
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

        private void SetMapNotice(string message)
        {
            bool visible = !string.IsNullOrEmpty(message);
            if (visible)
            {
                mapNoticeText.text = message;
            }

            mapNoticeText.gameObject.SetActive(visible);
        }

        public void AddSpotMarker(SpotRuntimeState state, Action<SpotRuntimeState> onClick)
        {
            Sprite icon = iconLibrary.Resolve(state.Definition.IconKey, state.Definition.Category);
            LandmarkMarkerView markerView = UnityEngine.Object.Instantiate(
                uiBindings.LandmarkMarkerPrefab,
                markerRoot,
                false);
            markerView.gameObject.name = "Spot_" + state.Definition.Id;
            Image marker = markerView.Icon;
            marker.sprite = icon;
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
                state.Definition.Longitude,
                spotMarkerSize,
                icon != null);

            CreateCodexCard(state, icon, onClick);
            UpdateSpotState(state);
        }

        public void UpdateUserLocation(GeoLocationSample location)
        {
            lastUserLocation = location;
            if (!location.IsValid || liveMapRenderer == null)
            {
                userMarker.gameObject.SetActive(false);
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
        }

        public void CenterOnLocation(double latitude, double longitude)
        {
            if (liveMapRenderer != null)
            {
                liveMapRenderer.SetCenter(latitude, longitude);
            }
        }

        public void SelectSpot(SpotRuntimeState state, GeoLocationSample currentLocation)
        {
            selectedNameText.text = state.IsUnlocked ? state.Definition.DisplayName : "???";
            selectedDescriptionText.text = state.IsUnlocked ? state.Definition.Description : "아직 해금되지 않은 거점입니다.";
            if (currentLocation.IsValid)
            {
                double distance = GeoProjection.DistanceMeters(
                    currentLocation.Latitude,
                    currentLocation.Longitude,
                    state.Definition.Latitude,
                    state.Definition.Longitude);
                selectedDistanceText.text = string.Format("{0:0}m\n반경 {1:0}m", distance, state.Definition.RadiusMeters);
            }
            else
            {
                selectedDistanceText.text = string.Format("반경 {0:0}m", state.Definition.RadiusMeters);
            }
        }

        public void SetLocationStatus(string message)
        {
            statusText.text = message;
        }

        public void SetCodexVisible(bool visible)
        {
            if (visible)
            {
                codexPanel.transform.SetAsLastSibling();
            }

            codexPanel.SetActive(visible);
        }

        public bool IsCodexVisible()
        {
            return codexPanel.activeSelf;
        }

        public void SetProgress(int unlocked, int total)
        {
            unlockedCount = unlocked;
            totalCount = total;
            progressText.text = string.Format("{0} / {1}", unlockedCount, totalCount);
        }

        public void UpdateSpotState(SpotRuntimeState state)
        {
            if (markers.TryGetValue(state.Definition.Id, out MarkerBinding marker))
            {
                ApplyMarkerVisual(marker.Image, state, marker.HasIcon, marker.Size);
            }

            if (codexCards.TryGetValue(state.Definition.Id, out CodexBinding card))
            {
                card.NameText.text = state.IsUnlocked ? state.Definition.DisplayName : "???";
                card.CategoryText.text = state.IsUnlocked ? state.Definition.Category : "잠김";
                card.DescriptionText.text = state.IsUnlocked ? state.Definition.Description : "근처에 도착하면 해금됩니다.";
                ApplyMarkerVisual(card.Icon, state, card.HasIcon, CodexIconSize);
            }
        }

        private void CreateCodexCard(SpotRuntimeState state, Sprite icon, Action<SpotRuntimeState> onClick)
        {
            LandmarkCardView cardView = UnityEngine.Object.Instantiate(
                uiBindings.LandmarkCardPrefab,
                uiBindings.CodexContent,
                false);
            cardView.gameObject.name = "Codex_" + state.Definition.Id;
            cardView.Button.onClick.AddListener(() =>
            {
                onClick?.Invoke(state);
                SetCodexVisible(false);
            });

            cardView.Icon.sprite = icon;
            cardView.Icon.raycastTarget = false;
            codexCards[state.Definition.Id] = new CodexBinding(
                cardView.Icon,
                cardView.NameText,
                cardView.CategoryText,
                cardView.DescriptionText,
                icon != null);
        }

        private void TogglePixelFilter()
        {
            pixelFilterEnabled = !pixelFilterEnabled;
            PlayerPrefs.SetInt(PixelModePreferenceKey, pixelFilterEnabled ? 1 : 0);
            PlayerPrefs.Save();
            ApplyPixelFilter();
            UpdatePixelToggleText();
        }

        private void ApplyPixelFilter()
        {
            if (liveMapRenderer != null)
            {
                liveMapRenderer.SetPixelMode(pixelFilterEnabled);
            }
        }

        private void UpdatePixelToggleText()
        {
            pixelToggleText.text = pixelFilterEnabled ? "픽셀 ON" : "픽셀 OFF";
            if (pixelToggleButton.targetGraphic is Image background)
            {
                background.color = pixelFilterEnabled
                    ? new Color32(94, 205, 162, 255)
                    : new Color32(239, 228, 199, 255);
            }
        }

        private void Pan(Vector2 delta)
        {
            if (liveMapRenderer != null)
            {
                liveMapRenderer.Pan(delta);
            }
        }

        private void ZoomAt(float factor, Vector2 screenPosition)
        {
            if (liveMapRenderer != null)
            {
                liveMapRenderer.ZoomAt(factor, screenPosition);
            }
        }

        private void OnFirstLiveTileReady()
        {
            if (mapRendered)
            {
                return;
            }

            mapRendered = true;
            SetMapNotice(null);
            UpdateLiveMarkerPositions();
        }

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
        }

        public void OnClickARBtn()
        {
            ARRequested?.Invoke();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || PIXELROAD_LIVE_VECTOR_MAP
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

        private static Color32 MarkerColor(SpotRuntimeState state)
        {
            return state.IsUnlocked ? UnlockedMarkerColor : LockedMarkerColor;
        }

        /// <summary>
        /// 해금 상태를 마커에 반영한다.
        /// JSON의 thumbnail 아이콘이 있으면 색만 바꾸고(잠김은 흐리게), 없으면 코드 생성 도형을 교체한다.
        /// </summary>
        private void ApplyMarkerVisual(Image image, SpotRuntimeState state, bool hasIcon, int size)
        {
            if (hasIcon)
            {
                image.color = state.IsUnlocked ? Color.white : (Color)LockedIconTint;
                return;
            }

            image.sprite = GetShapeSprite(false, size, MarkerColor(state));
            image.color = Color.white;
        }

        /// <summary>
        /// 아이콘 리소스가 없을 때 쓰는 코드 생성 도형 스프라이트. 모양·크기·색 조합별로 1개만 만든다.
        /// </summary>
        private Sprite GetShapeSprite(bool circle, int size, Color32 color)
        {
            ShapeKey key = new ShapeKey(circle, size, color);
            if (shapeSprites.TryGetValue(key, out Sprite cached) && cached != null)
            {
                return cached;
            }

            Texture2D texture = circle ? CreateCircleTexture(size, color) : CreateDiamondTexture(size, color);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            shapeSprites[key] = sprite;
            return sprite;
        }

        private static Canvas GetOrCreateCanvas()
        {
            Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                if (canvas.GetComponent<GraphicRaycaster>() == null)
                {
                    canvas.gameObject.AddComponent<GraphicRaycaster>();
                }

                return canvas;
            }

            GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void EnsureUiInput(Canvas canvas)
        {
            if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            EventSystem eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
                eventSystem = eventSystemObject.GetComponent<EventSystem>();
            }

            eventSystem.enabled = true;

            // 기본 드래그 임계값 10px는 고DPI 단말에서 약 0.6mm라, 손가락이 조금만 흔들려도
            // 탭이 드래그로 승격되면서 버튼/카드 클릭 판정이 취소된다. 화면 DPI에 맞춰 넓혀 준다.
            if (Screen.dpi > 1f)
            {
                eventSystem.pixelDragThreshold = Mathf.Max(
                    eventSystem.pixelDragThreshold,
                    Mathf.RoundToInt(Screen.dpi * DragThresholdInches));
            }

#if ENABLE_INPUT_SYSTEM
            InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
            {
                inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            if (inputModule.actionsAsset == null
                || inputModule.point == null
                || inputModule.point.action == null
                || inputModule.leftClick == null
                || inputModule.leftClick.action == null
                || inputModule.scrollWheel == null
                || inputModule.scrollWheel.action == null)
            {
                inputModule.AssignDefaultActions();
            }

            inputModule.enabled = true;
#else
            StandaloneInputModule inputModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (inputModule == null)
            {
                inputModule = eventSystem.gameObject.AddComponent<StandaloneInputModule>();
            }

            inputModule.enabled = true;
#endif
        }

        private static Texture2D CreateDiamondTexture(int size, Color32 color)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            Color32 clear = new Color32(0, 0, 0, 0);
            float center = (size - 1) * 0.5f;
            float radius = size * 0.42f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool inside = Mathf.Abs(x - center) + Mathf.Abs(y - center) <= radius;
                    bool border = Mathf.Abs(Mathf.Abs(x - center) + Mathf.Abs(y - center) - radius) < 1.4f;
                    texture.SetPixel(x, y, inside ? (border ? new Color32(20, 18, 16, 255) : color) : clear);
                }
            }

            texture.Apply();
            return texture;
        }

        private static Texture2D CreateCircleTexture(int size, Color32 color)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            Color32 clear = new Color32(0, 0, 0, 0);
            float center = (size - 1) * 0.5f;
            float radius = size * 0.38f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    if (distance <= radius)
                    {
                        texture.SetPixel(x, y, distance > radius - 2f ? new Color32(245, 240, 220, 255) : color);
                    }
                    else
                    {
                        texture.SetPixel(x, y, clear);
                    }
                }
            }

            texture.Apply();
            return texture;
        }

        private sealed class MarkerBinding
        {
            public readonly Image Image;
            public readonly MapMarkerTapTarget TapTarget;
            public readonly double Latitude;
            public readonly double Longitude;
            public readonly int Size;
            public readonly bool HasIcon;

            public MarkerBinding(
                Image image,
                MapMarkerTapTarget tapTarget,
                double latitude,
                double longitude,
                int size,
                bool hasIcon)
            {
                Image = image;
                TapTarget = tapTarget;
                Latitude = latitude;
                Longitude = longitude;
                Size = size;
                HasIcon = hasIcon;
            }
        }

        private sealed class CodexBinding
        {
            public readonly Image Icon;
            public readonly TMP_Text NameText;
            public readonly TMP_Text CategoryText;
            public readonly TMP_Text DescriptionText;
            public readonly bool HasIcon;

            public CodexBinding(
                Image icon,
                TMP_Text nameText,
                TMP_Text categoryText,
                TMP_Text descriptionText,
                bool hasIcon)
            {
                Icon = icon;
                NameText = nameText;
                CategoryText = categoryText;
                DescriptionText = descriptionText;
                HasIcon = hasIcon;
            }
        }

        /// <summary>코드 생성 도형 스프라이트 캐시 키. 박싱 없는 비교를 위해 구조체로 둔다.</summary>
        private readonly struct ShapeKey : IEquatable<ShapeKey>
        {
            private readonly bool circle;
            private readonly int size;
            private readonly uint color;

            public ShapeKey(bool circle, int size, Color32 color)
            {
                this.circle = circle;
                this.size = size;
                this.color = ((uint)color.r << 24) | ((uint)color.g << 16) | ((uint)color.b << 8) | color.a;
            }

            public bool Equals(ShapeKey other)
            {
                return circle == other.circle && size == other.size && color == other.color;
            }

            public override bool Equals(object obj)
            {
                return obj is ShapeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return (int)(color ^ ((uint)size << 3) ^ (circle ? 0x9E3779B9u : 0u));
            }
        }
    }
}
