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
        private const float MinZoom = 0.45f;
        private const float MaxZoom = 6f;
        private const string PixelModePreferenceKey = "PixelRoad.MapPixelMode";

        private readonly MapConfig config;
        private readonly Texture2D mapTexture;
        private readonly Dictionary<string, MarkerBinding> markers = new Dictionary<string, MarkerBinding>();
        private readonly Dictionary<string, CodexBinding> codexCards = new Dictionary<string, CodexBinding>();
        private readonly RectTransform viewport;
        private readonly RectTransform mapContent;
        private readonly RawImage mapImage;
        private readonly RawImage liveMapImage;
        private readonly RectTransform markerRoot;
        private readonly ILiveMapController liveMapRenderer;
        private readonly Image userMarker;
        private readonly GameObject codexPanel;
        private readonly TMP_Text statusText;
        private readonly TMP_Text progressText;
        private readonly TMP_Text selectedNameText;
        private readonly TMP_Text selectedDescriptionText;
        private readonly TMP_Text selectedDistanceText;
        private readonly Button pixelToggleButton;
        private readonly TMP_Text pixelToggleText;
        private readonly TMP_FontAsset pixelFont;
        private readonly Material pixelMaterial;
        private float zoom = 1f;
        private bool pixelFilterEnabled;
        private bool liveMapReady;
        private GeoLocationSample lastUserLocation;
        private int unlockedCount;
        private int totalCount;

        public event Action CodexRequested;

        private PixelRoadRuntimeView(MapConfig config, Texture2D mapTexture)
        {
            this.config = config;
            this.mapTexture = mapTexture;
            pixelFilterEnabled = PlayerPrefs.HasKey(PixelModePreferenceKey)
                ? PlayerPrefs.GetInt(PixelModePreferenceKey, 0) != 0
                : config.enablePixelFilter;

            Canvas canvas = GetOrCreateCanvas();
            EnsureUiInput(canvas);
            pixelFont = CreateRuntimePixelFont();
            viewport = CreateRect("MapViewport", canvas.transform);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image viewportBackground = viewport.gameObject.AddComponent<Image>();
            viewportBackground.color = new Color32(24, 22, 19, 255);

            PixelRoadMapInput input = viewport.gameObject.AddComponent<PixelRoadMapInput>();
            input.Dragged += Pan;
            input.Zoomed += ZoomAt;

            mapContent = CreateRect("MapContent", viewport);
            mapContent.anchorMin = new Vector2(0.5f, 0.5f);
            mapContent.anchorMax = new Vector2(0.5f, 0.5f);
            mapContent.pivot = new Vector2(0.5f, 0.5f);
            mapContent.sizeDelta = new Vector2(mapTexture.width, mapTexture.height);
            mapContent.anchoredPosition = Vector2.zero;

            mapImage = CreateObject("MapImage", mapContent).AddComponent<RawImage>();
            mapImage.texture = mapTexture;
            RectTransform imageRect = mapImage.rectTransform;
            imageRect.anchorMin = new Vector2(0.5f, 0.5f);
            imageRect.anchorMax = new Vector2(0.5f, 0.5f);
            imageRect.pivot = new Vector2(0.5f, 0.5f);
            imageRect.sizeDelta = new Vector2(mapTexture.width, mapTexture.height);
            imageRect.anchoredPosition = Vector2.zero;

            liveMapImage = CreateObject("LiveVectorMap", viewport).AddComponent<RawImage>();
            Stretch(liveMapImage.rectTransform);
            liveMapImage.raycastTarget = false;
            liveMapImage.enabled = false;

            markerRoot = CreateRect("MapMarkerOverlay", viewport);
            Stretch(markerRoot);

            Shader pixelShader = Shader.Find("PixelRoad/UI Pixelate");
            if (pixelShader != null)
            {
                pixelMaterial = new Material(pixelShader);
                pixelMaterial.SetFloat("_PixelSize", Mathf.Max(1, config.pixelBlockSize));
            }

            ApplyPixelFilter();

            ILiveMapController renderer = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || PIXELROAD_LIVE_VECTOR_MAP
            if (ShouldEnableLiveVectorMap(config))
            {
                renderer = viewport.gameObject.AddComponent<LiveVectorMapRenderer>();
                renderer.ViewChanged += UpdateLiveMarkerPositions;
                renderer.FirstTileReady += OnFirstLiveTileReady;
                if (!renderer.Initialize(
                        config,
                        viewport,
                        liveMapImage,
                        config.editorStartLatitude,
                        config.editorStartLongitude))
                {
                    renderer.ViewChanged -= UpdateLiveMarkerPositions;
                    renderer.FirstTileReady -= OnFirstLiveTileReady;
                    UnityEngine.Object.Destroy(renderer as UnityEngine.Object);
                    renderer = null;
                }
            }
#endif

            liveMapRenderer = renderer;
            liveMapRenderer?.SetPixelMode(pixelFilterEnabled);

            userMarker = CreateMarkerImage("UserMarker", mapContent, new Color32(52, 122, 255, 255), 22, true);
            userMarker.gameObject.SetActive(false);

            RectTransform topBar = CreateTopBar(canvas.transform);
            CreateCodexButton(topBar);
            CreateTitle(topBar);
            pixelToggleButton = CreatePixelToggle(topBar);
            pixelToggleText = pixelToggleButton.GetComponentInChildren<TMP_Text>();
            CreateZoomControls(canvas.transform);

            statusText = CreateText("StatusText", canvas.transform, "GPS", 18, TextAlignmentOptions.Left);
            RectTransform statusRect = statusText.rectTransform;
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.sizeDelta = new Vector2(-32f, 28f);
            statusRect.anchoredPosition = new Vector2(0f, 14f);
            statusText.color = new Color32(246, 237, 217, 255);

            CreateAttribution(canvas.transform);

            RectTransform bottomPanel = CreateBottomPanel(canvas.transform);
            selectedNameText = CreateText("SelectedName", bottomPanel, "거점을 선택하세요", 22, TextAlignmentOptions.Left);
            selectedNameText.rectTransform.anchorMin = new Vector2(0f, 0.45f);
            selectedNameText.rectTransform.anchorMax = new Vector2(0.68f, 1f);
            selectedNameText.rectTransform.offsetMin = new Vector2(18f, 0f);
            selectedNameText.rectTransform.offsetMax = new Vector2(-8f, -8f);

            selectedDescriptionText = CreateText("SelectedDescription", bottomPanel, "지도 위 마커를 누르면 도감 정보가 열립니다.", 17, TextAlignmentOptions.Left);
            selectedDescriptionText.rectTransform.anchorMin = new Vector2(0f, 0f);
            selectedDescriptionText.rectTransform.anchorMax = new Vector2(0.72f, 0.55f);
            selectedDescriptionText.rectTransform.offsetMin = new Vector2(18f, 10f);
            selectedDescriptionText.rectTransform.offsetMax = new Vector2(-8f, -2f);

            selectedDistanceText = CreateText("SelectedDistance", bottomPanel, "-", 18, TextAlignmentOptions.Right);
            selectedDistanceText.rectTransform.anchorMin = new Vector2(0.72f, 0f);
            selectedDistanceText.rectTransform.anchorMax = new Vector2(1f, 1f);
            selectedDistanceText.rectTransform.offsetMin = new Vector2(0f, 10f);
            selectedDistanceText.rectTransform.offsetMax = new Vector2(-18f, -10f);

            codexPanel = CreateCodexPanel(canvas.transform);
            progressText = codexPanel.transform.Find("Window/Header/Progress").GetComponent<TMP_Text>();
            codexPanel.SetActive(false);
            UpdatePixelToggleText();
            Canvas.ForceUpdateCanvases();
        }

        public static PixelRoadRuntimeView Create(MapConfig config, Texture2D mapTexture)
        {
            return new PixelRoadRuntimeView(config, mapTexture);
        }

        public void AddSpotMarker(SpotRuntimeState state, Action<SpotRuntimeState> onClick)
        {
            Transform parent = liveMapReady ? markerRoot : mapContent;
            Image marker = CreateMarkerImage("Spot_" + state.Definition.Id, parent, MarkerColor(state), 28, false);
            marker.rectTransform.anchoredPosition = liveMapReady
                ? liveMapRenderer.LatLonToViewportLocal(state.Definition.Latitude, state.Definition.Longitude)
                : LatLonToMapLocal(state.Definition.Latitude, state.Definition.Longitude);
            Button button = marker.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = marker;
            button.onClick.AddListener(() => onClick?.Invoke(state));
            markers[state.Definition.Id] = new MarkerBinding(
                marker,
                button,
                state.Definition.Latitude,
                state.Definition.Longitude);

            CreateCodexCard(state, onClick);
            UpdateSpotState(state);
        }

        public void UpdateUserLocation(GeoLocationSample location)
        {
            lastUserLocation = location;
            if (!location.IsValid)
            {
                userMarker.gameObject.SetActive(false);
                return;
            }

            if (liveMapReady)
            {
                bool isVisible = liveMapRenderer.IsInViewport(location.Latitude, location.Longitude, 24f);
                userMarker.gameObject.SetActive(isVisible);
                if (isVisible)
                {
                    userMarker.rectTransform.anchoredPosition = liveMapRenderer.LatLonToViewportLocal(
                        location.Latitude,
                        location.Longitude);
                }

                return;
            }

            Vector2 normalized = GeoProjection.LatLonToNormalizedWebMercator(location.Latitude, location.Longitude, config.bounds);
            bool isInside = normalized.x >= 0f && normalized.x <= 1f && normalized.y >= 0f && normalized.y <= 1f;
            userMarker.gameObject.SetActive(isInside);
            if (isInside)
            {
                userMarker.rectTransform.anchoredPosition = NormalizedToMapLocal(normalized);
            }
        }

        public void CenterOnLocation(double latitude, double longitude)
        {
            if (liveMapRenderer != null)
            {
                liveMapRenderer.SetCenter(latitude, longitude);
            }

            if (liveMapReady)
            {
                return;
            }

            Vector2 local = LatLonToMapLocal(latitude, longitude);
            mapContent.anchoredPosition = -local * zoom;
            ClampContent();
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
                marker.Image.color = MarkerColor(state);
            }

            if (codexCards.TryGetValue(state.Definition.Id, out CodexBinding card))
            {
                card.NameText.text = state.IsUnlocked ? state.Definition.DisplayName : "???";
                card.CategoryText.text = state.IsUnlocked ? state.Definition.Category : "잠김";
                card.DescriptionText.text = state.IsUnlocked ? state.Definition.Description : "근처에 도착하면 해금됩니다.";
                card.Icon.color = MarkerColor(state);
            }
        }

        private RectTransform CreateTopBar(Transform parent)
        {
            RectTransform topBar = CreateRect("TopBar", parent);
            topBar.anchorMin = new Vector2(0f, 1f);
            topBar.anchorMax = new Vector2(1f, 1f);
            topBar.pivot = new Vector2(0.5f, 1f);
            topBar.sizeDelta = new Vector2(0f, 72f);
            topBar.anchoredPosition = Vector2.zero;
            Image background = topBar.gameObject.AddComponent<Image>();
            background.color = new Color32(18, 17, 15, 238);
            return topBar;
        }

        private void CreateCodexButton(RectTransform topBar)
        {
            Button button = CreateButton("CodexButton", topBar, "도감", new Vector2(92f, 48f));
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(16f, 0f);
            button.onClick.AddListener(() => CodexRequested?.Invoke());
        }

        private void CreateTitle(RectTransform topBar)
        {
            TMP_Text title = CreateText("Title", topBar, config.appTitle, 24, TextAlignmentOptions.Center);
            RectTransform rect = title.rectTransform;
            rect.anchorMin = new Vector2(0.24f, 0f);
            rect.anchorMax = new Vector2(0.76f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private Button CreatePixelToggle(RectTransform topBar)
        {
            Button button = CreateButton("PixelToggle", topBar, "픽셀", new Vector2(104f, 48f));
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-16f, 0f);
            button.onClick.AddListener(TogglePixelFilter);
            return button;
        }

        private void CreateZoomControls(Transform parent)
        {
            RectTransform controls = CreateRect("ZoomControls", parent);
            controls.anchorMin = new Vector2(1f, 0.5f);
            controls.anchorMax = new Vector2(1f, 0.5f);
            controls.pivot = new Vector2(1f, 0.5f);
            controls.sizeDelta = new Vector2(64f, 128f);
            controls.anchoredPosition = new Vector2(-16f, 24f);

            Button zoomIn = CreateButton("ZoomIn", controls, "+", new Vector2(56f, 56f));
            RectTransform zoomInRect = zoomIn.GetComponent<RectTransform>();
            zoomInRect.anchorMin = new Vector2(0.5f, 1f);
            zoomInRect.anchorMax = new Vector2(0.5f, 1f);
            zoomInRect.pivot = new Vector2(0.5f, 1f);
            zoomInRect.anchoredPosition = Vector2.zero;
            zoomIn.onClick.AddListener(() => ZoomAtViewportCenter(2f));

            Button zoomOut = CreateButton("ZoomOut", controls, "-", new Vector2(56f, 56f));
            RectTransform zoomOutRect = zoomOut.GetComponent<RectTransform>();
            zoomOutRect.anchorMin = new Vector2(0.5f, 0f);
            zoomOutRect.anchorMax = new Vector2(0.5f, 0f);
            zoomOutRect.pivot = new Vector2(0.5f, 0f);
            zoomOutRect.anchoredPosition = Vector2.zero;
            zoomOut.onClick.AddListener(() => ZoomAtViewportCenter(0.5f));
        }

        private void CreateAttribution(Transform parent)
        {
            RectTransform panel = CreateRect("MapAttribution", parent);
            panel.anchorMin = new Vector2(1f, 0f);
            panel.anchorMax = new Vector2(1f, 0f);
            panel.pivot = new Vector2(1f, 0f);
            panel.sizeDelta = new Vector2(310f, 30f);
            panel.anchoredPosition = new Vector2(-10f, 172f);
            Image background = panel.gameObject.AddComponent<Image>();
            background.color = new Color32(18, 17, 15, 210);

            Button button = panel.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = background;
            if (!string.IsNullOrWhiteSpace(config.mapAttributionUrl))
            {
                button.onClick.AddListener(() => Application.OpenURL(config.mapAttributionUrl));
            }
            else
            {
                button.interactable = false;
            }

            TMP_Text text = CreateText(
                "Label",
                panel,
                string.IsNullOrWhiteSpace(config.mapAttribution)
                    ? "© OpenStreetMap contributors"
                    : config.mapAttribution,
                13,
                TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            text.color = new Color32(246, 237, 217, 255);
        }

        private RectTransform CreateBottomPanel(Transform parent)
        {
            RectTransform panel = CreateRect("SpotInfoPanel", parent);
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(1f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(-24f, 118f);
            panel.anchoredPosition = new Vector2(0f, 44f);
            Image background = panel.gameObject.AddComponent<Image>();
            background.color = new Color32(235, 222, 188, 246);
            return panel;
        }

        private GameObject CreateCodexPanel(Transform parent)
        {
            RectTransform overlay = CreateRect("CodexPanel", parent);
            Stretch(overlay);
            Image overlayBackground = overlay.gameObject.AddComponent<Image>();
            overlayBackground.color = new Color32(15, 14, 12, 230);

            RectTransform window = CreateRect("Window", overlay);
            window.anchorMin = new Vector2(0.08f, 0.08f);
            window.anchorMax = new Vector2(0.92f, 0.92f);
            window.pivot = new Vector2(0.5f, 0.5f);
            window.offsetMin = Vector2.zero;
            window.offsetMax = Vector2.zero;
            Image windowBackground = window.gameObject.AddComponent<Image>();
            windowBackground.color = new Color32(236, 224, 194, 255);

            RectTransform header = CreateRect("Header", window);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, 72f);
            header.anchoredPosition = Vector2.zero;
            Image headerBackground = header.gameObject.AddComponent<Image>();
            headerBackground.color = new Color32(18, 17, 15, 255);

            TMP_Text title = CreateText("Title", header, "수집 도감", 24, TextAlignmentOptions.Left);
            title.rectTransform.anchorMin = new Vector2(0f, 0f);
            title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.offsetMin = new Vector2(18f, 0f);
            title.rectTransform.offsetMax = Vector2.zero;

            TMP_Text progress = CreateText("Progress", header, "0 / 0", 20, TextAlignmentOptions.Right);
            progress.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            progress.rectTransform.anchorMax = new Vector2(1f, 1f);
            progress.rectTransform.offsetMin = Vector2.zero;
            progress.rectTransform.offsetMax = new Vector2(-96f, 0f);

            Button close = CreateButton("CloseButton", header, "X", new Vector2(56f, 44f));
            RectTransform closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 0.5f);
            closeRect.anchorMax = new Vector2(1f, 0.5f);
            closeRect.pivot = new Vector2(1f, 0.5f);
            closeRect.anchoredPosition = new Vector2(-16f, 0f);
            close.onClick.AddListener(() => SetCodexVisible(false));

            ScrollRect scroll = CreateObject("Scroll", window).AddComponent<ScrollRect>();
            RectTransform scrollRect = scroll.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(16f, 16f);
            scrollRect.offsetMax = new Vector2(-16f, -88f);
            scroll.viewport = scrollRect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            Image scrollBackground = scroll.gameObject.AddComponent<Image>();
            scrollBackground.color = new Color32(236, 224, 194, 255);
            scroll.gameObject.AddComponent<RectMask2D>();

            RectTransform content = CreateRect("Content", scrollRect);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(294f, 168f);
            grid.spacing = new Vector2(14f, 14f);
            grid.padding = new RectOffset(4, 4, 4, 4);
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;

            return overlay.gameObject;
        }

        private void CreateCodexCard(SpotRuntimeState state, Action<SpotRuntimeState> onClick)
        {
            RectTransform content = codexPanel.transform.Find("Window/Scroll/Content").GetComponent<RectTransform>();
            Button card = CreateButton("Codex_" + state.Definition.Id, content, string.Empty, new Vector2(294f, 168f));
            Image background = card.GetComponent<Image>();
            background.color = new Color32(28, 25, 21, 255);
            card.onClick.AddListener(() =>
            {
                onClick?.Invoke(state);
                SetCodexVisible(false);
            });

            Image icon = CreateMarkerImage("Icon", card.transform, MarkerColor(state), 42, false);
            icon.rectTransform.anchorMin = new Vector2(0f, 1f);
            icon.rectTransform.anchorMax = new Vector2(0f, 1f);
            icon.rectTransform.pivot = new Vector2(0f, 1f);
            icon.rectTransform.anchoredPosition = new Vector2(14f, -14f);

            TMP_Text name = CreateText("Name", card.transform, string.Empty, 19, TextAlignmentOptions.Left);
            name.rectTransform.anchorMin = new Vector2(0f, 0.56f);
            name.rectTransform.anchorMax = new Vector2(1f, 1f);
            name.rectTransform.offsetMin = new Vector2(68f, 0f);
            name.rectTransform.offsetMax = new Vector2(-12f, -12f);

            TMP_Text category = CreateText("Category", card.transform, string.Empty, 15, TextAlignmentOptions.Right);
            category.rectTransform.anchorMin = new Vector2(0.52f, 0.56f);
            category.rectTransform.anchorMax = new Vector2(1f, 1f);
            category.rectTransform.offsetMin = Vector2.zero;
            category.rectTransform.offsetMax = new Vector2(-12f, -42f);
            category.color = new Color32(94, 205, 162, 255);

            TMP_Text description = CreateText("Description", card.transform, string.Empty, 15, TextAlignmentOptions.Left);
            description.rectTransform.anchorMin = new Vector2(0f, 0f);
            description.rectTransform.anchorMax = new Vector2(1f, 0.58f);
            description.rectTransform.offsetMin = new Vector2(14f, 12f);
            description.rectTransform.offsetMax = new Vector2(-14f, -8f);

            codexCards[state.Definition.Id] = new CodexBinding(icon, name, category, description);
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

            if (!liveMapReady)
            {
                mapTexture.filterMode = pixelFilterEnabled ? FilterMode.Point : FilterMode.Bilinear;
                mapImage.material = pixelFilterEnabled ? pixelMaterial : null;
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

            if (liveMapReady)
            {
                return;
            }

            mapContent.anchoredPosition += delta;
            ClampContent();
        }

        private void ZoomAt(float factor, Vector2 screenPosition)
        {
            if (liveMapRenderer != null)
            {
                liveMapRenderer.ZoomAt(factor, screenPosition);
            }

            if (liveMapReady)
            {
                return;
            }

            float nextZoom = Mathf.Clamp(zoom * factor, MinZoom, MaxZoom);
            if (Mathf.Approximately(nextZoom, zoom))
            {
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, screenPosition, null, out Vector2 localPoint);
            Vector2 before = (localPoint - mapContent.anchoredPosition) / zoom;
            zoom = nextZoom;
            mapContent.localScale = Vector3.one * zoom;
            mapContent.anchoredPosition = localPoint - before * zoom;
            ClampContent();
        }

        private void ZoomAtViewportCenter(float factor)
        {
            Vector3 worldCenter = viewport.TransformPoint(viewport.rect.center);
            Vector2 screenCenter = RectTransformUtility.WorldToScreenPoint(null, worldCenter);
            ZoomAt(factor, screenCenter);
        }

        private void ClampContent()
        {
            Vector2 viewportSize = viewport.rect.size;
            Vector2 mapSize = mapContent.sizeDelta * zoom;
            float maxX = Mathf.Max(0f, (mapSize.x - viewportSize.x) * 0.5f);
            float maxY = Mathf.Max(0f, (mapSize.y - viewportSize.y) * 0.5f);
            Vector2 position = mapContent.anchoredPosition;
            position.x = Mathf.Clamp(position.x, -maxX, maxX);
            position.y = Mathf.Clamp(position.y, -maxY, maxY);
            mapContent.anchoredPosition = position;
        }

        private Vector2 LatLonToMapLocal(double latitude, double longitude)
        {
            Vector2 normalized = GeoProjection.LatLonToNormalizedWebMercator(latitude, longitude, config.bounds);
            return NormalizedToMapLocal(normalized);
        }

        private Vector2 NormalizedToMapLocal(Vector2 normalized)
        {
            float x = normalized.x * mapTexture.width - mapTexture.width * 0.5f;
            float y = mapTexture.height * 0.5f - normalized.y * mapTexture.height;
            return new Vector2(x, y);
        }

        private void OnFirstLiveTileReady()
        {
            if (liveMapReady || liveMapRenderer == null)
            {
                return;
            }

            liveMapReady = true;
            ReparentMarkerToOverlay(userMarker);
            foreach (KeyValuePair<string, MarkerBinding> pair in markers)
            {
                ReparentMarkerToOverlay(pair.Value.Image);
            }

            mapContent.gameObject.SetActive(false);
            liveMapImage.enabled = true;
            UpdateLiveMarkerPositions();
            ApplyPixelFilter();
        }

        private void UpdateLiveMarkerPositions()
        {
            if (!liveMapReady || liveMapRenderer == null)
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
                    24f);
                userMarker.gameObject.SetActive(visible);
                if (visible)
                {
                    userMarker.rectTransform.anchoredPosition = liveMapRenderer.LatLonToViewportLocal(
                        lastUserLocation.Latitude,
                        lastUserLocation.Longitude);
                }
            }
        }

        private void ReparentMarkerToOverlay(Image marker)
        {
            marker.rectTransform.SetParent(markerRoot, false);
            marker.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            marker.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            marker.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            marker.rectTransform.localScale = Vector3.one;
        }

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

        private static Color32 MarkerColor(SpotRuntimeState state)
        {
            return state.IsUnlocked ? new Color32(208, 56, 48, 255) : new Color32(70, 67, 61, 255);
        }

        private Image CreateMarkerImage(string name, Transform parent, Color32 color, int size, bool circle)
        {
            Image image = CreateObject(name, parent).AddComponent<Image>();
            image.sprite = Sprite.Create(circle ? CreateCircleTexture(size, color) : CreateDiamondTexture(size, color), new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            image.color = Color.white;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            return image;
        }

        private Button CreateButton(string name, Transform parent, string label, Vector2 size)
        {
            Button button = CreateObject(name, parent).AddComponent<Button>();
            Image image = button.gameObject.AddComponent<Image>();
            image.color = new Color32(239, 228, 199, 255);
            button.targetGraphic = image;
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            if (!string.IsNullOrEmpty(label))
            {
                TMP_Text text = CreateText("Label", button.transform, label, 18, TextAlignmentOptions.Center);
                Stretch(text.rectTransform);
                text.color = new Color32(18, 17, 15, 255);
            }

            return button;
        }

        private TMP_Text CreateText(string name, Transform parent, string value, int size, TextAlignmentOptions alignment)
        {
            TextMeshProUGUI text = CreateObject(name, parent).AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.color = new Color32(18, 17, 15, 255);
            if (pixelFont != null)
            {
                text.font = pixelFont;
            }

            return text;
        }

        private TMP_FontAsset CreateRuntimePixelFont()
        {
            Font font = Resources.Load<Font>("PixelRoad/Fonts/Galmuri11");
            if (font == null)
            {
                return null;
            }

            try
            {
                return TMP_FontAsset.CreateFontAsset(font);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[PixelRoad] Failed to create Galmuri TMP font asset. Falling back to TMP default font. " + exception.Message);
                return null;
            }
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

        private static GameObject CreateObject(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            return CreateObject(name, parent).GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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
            public readonly Button Button;
            public readonly double Latitude;
            public readonly double Longitude;

            public MarkerBinding(Image image, Button button, double latitude, double longitude)
            {
                Image = image;
                Button = button;
                Latitude = latitude;
                Longitude = longitude;
            }
        }

        private sealed class CodexBinding
        {
            public readonly Image Icon;
            public readonly TMP_Text NameText;
            public readonly TMP_Text CategoryText;
            public readonly TMP_Text DescriptionText;

            public CodexBinding(Image icon, TMP_Text nameText, TMP_Text categoryText, TMP_Text descriptionText)
            {
                Icon = icon;
                NameText = nameText;
                CategoryText = categoryText;
                DescriptionText = descriptionText;
            }
        }
    }
}
