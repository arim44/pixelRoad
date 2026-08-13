using PixelRoad.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace PixelRoad.Editor
{
    public static class PixelRoadUiPrefabBuilder
    {
        private const string UiFolder = "Assets/Resources/PixelRoad/UI";
        private const string RootPrefabPath = UiFolder + "/PixelRoadUIRoot.prefab";
        private const string MarkerPrefabPath = UiFolder + "/LandmarkMarker.prefab";
        private const string CardPrefabPath = UiFolder + "/LandmarkCodexCard.prefab";
        private const string FontSourcePath = "Assets/Resources/PixelRoad/Fonts/Galmuri11.ttf";
        private const string FontAssetPath = "Assets/Resources/PixelRoad/Fonts/Galmuri11 SDF.asset";

        private static TMP_FontAsset font;

        [MenuItem("Tools/Pixel Road/Rebuild UI Prefabs")]
        public static void RebuildUiPrefabs()
        {
            EnsureFolder("Assets/Resources/PixelRoad", "UI");
            font = LoadOrCreateFontAsset();

            LandmarkMarkerView markerPrefab = BuildMarkerPrefab();
            LandmarkCardView cardPrefab = BuildCardPrefab();
            BuildRootPrefab(markerPrefab, cardPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PixelRoad] UI prefabs rebuilt: " + RootPrefabPath);
        }

        private static TMP_FontAsset LoadOrCreateFontAsset()
        {
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
            {
                return existing;
            }

            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontSourcePath);
            if (sourceFont == null)
            {
                Debug.LogWarning("[PixelRoad] UI font source is missing: " + FontSourcePath);
                return TMP_Settings.defaultFontAsset;
            }

            TMP_FontAsset created = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                90,
                9,
                GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic,
                true);
            if (created == null)
            {
                Debug.LogWarning("[PixelRoad] Could not create the Galmuri TMP font asset.");
                return TMP_Settings.defaultFontAsset;
            }

            created.name = "Galmuri11 SDF";
            created.atlasTextures[0].name = "Galmuri11 Atlas";
            created.material.name = "Galmuri11 Atlas Material";
            AssetDatabase.CreateAsset(created, FontAssetPath);
            AssetDatabase.AddObjectToAsset(created.atlasTextures[0], created);
            AssetDatabase.AddObjectToAsset(created.material, created);
            EditorUtility.SetDirty(created);
            AssetDatabase.SaveAssets();
            return created;
        }

        private static LandmarkMarkerView BuildMarkerPrefab()
        {
            GameObject root = new GameObject(
                "LandmarkMarker",
                typeof(RectTransform),
                typeof(Image),
                typeof(MapMarkerTapTarget),
                typeof(LandmarkMarkerView));
            RectTransform rect = root.GetComponent<RectTransform>();
            SetCentered(rect, new Vector2(56f, 56f));
            Image image = root.GetComponent<Image>();
            image.color = Color.white;
            image.preserveAspect = true;
            LandmarkMarkerView view = root.GetComponent<LandmarkMarkerView>();
            Assign(view, "icon", image);
            Assign(view, "tapTarget", root.GetComponent<MapMarkerTapTarget>());

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, MarkerPrefabPath);
            Object.DestroyImmediate(root);
            return saved.GetComponent<LandmarkMarkerView>();
        }

        private static LandmarkCardView BuildCardPrefab()
        {
            GameObject root = new GameObject(
                "LandmarkCodexCard",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LandmarkCardView));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(294f, 168f);
            Image background = root.GetComponent<Image>();
            background.color = new Color32(28, 25, 21, 255);
            Button button = root.GetComponent<Button>();
            button.targetGraphic = background;

            Image icon = CreateImage("Icon", root.transform, Color.white);
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = new Vector2(0f, 1f);
            iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(0f, 1f);
            iconRect.sizeDelta = new Vector2(42f, 42f);
            iconRect.anchoredPosition = new Vector2(14f, -14f);
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            TMP_Text name = CreateText("Name", root.transform, string.Empty, 19, TextAlignmentOptions.Left);
            name.rectTransform.anchorMin = new Vector2(0f, 0.56f);
            name.rectTransform.anchorMax = new Vector2(1f, 1f);
            name.rectTransform.offsetMin = new Vector2(68f, 0f);
            name.rectTransform.offsetMax = new Vector2(-12f, -12f);

            TMP_Text category = CreateText("Category", root.transform, string.Empty, 15, TextAlignmentOptions.Right);
            category.rectTransform.anchorMin = new Vector2(0.52f, 0.56f);
            category.rectTransform.anchorMax = new Vector2(1f, 1f);
            category.rectTransform.offsetMin = Vector2.zero;
            category.rectTransform.offsetMax = new Vector2(-12f, -42f);
            category.color = new Color32(94, 205, 162, 255);

            TMP_Text description = CreateText("Description", root.transform, string.Empty, 15, TextAlignmentOptions.Left);
            description.rectTransform.anchorMin = new Vector2(0f, 0f);
            description.rectTransform.anchorMax = new Vector2(1f, 0.58f);
            description.rectTransform.offsetMin = new Vector2(14f, 12f);
            description.rectTransform.offsetMax = new Vector2(-14f, -8f);

            LandmarkCardView view = root.GetComponent<LandmarkCardView>();
            Assign(view, "button", button);
            Assign(view, "icon", icon);
            Assign(view, "nameText", name);
            Assign(view, "categoryText", category);
            Assign(view, "descriptionText", description);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, CardPrefabPath);
            Object.DestroyImmediate(root);
            return saved.GetComponent<LandmarkCardView>();
        }

        private static void BuildRootPrefab(
            LandmarkMarkerView markerPrefab,
            LandmarkCardView cardPrefab)
        {
            GameObject root = new GameObject(
                "PixelRoadUIRoot",
                typeof(RectTransform),
                typeof(PixelRoadUiBindings));
            Stretch(root.GetComponent<RectTransform>());
            PixelRoadUiBindings bindings = root.GetComponent<PixelRoadUiBindings>();

            RectTransform viewport = CreateRect("MapViewport", root.transform);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image viewportBackground = viewport.gameObject.AddComponent<Image>();
            viewportBackground.color = new Color32(24, 22, 19, 255);
            PixelRoadMapInput mapInput = viewport.gameObject.AddComponent<PixelRoadMapInput>();

            RawImage liveMapImage = CreateRect("LiveVectorMap", viewport).gameObject.AddComponent<RawImage>();
            Stretch(liveMapImage.rectTransform);
            liveMapImage.raycastTarget = false;
            liveMapImage.enabled = false;

            RectTransform markerRoot = CreateRect("MapMarkerOverlay", viewport);
            Stretch(markerRoot);

            Image userMarker = CreateImage("UserMarker", markerRoot, Color.white);
            SetCentered(userMarker.rectTransform, new Vector2(44f, 44f));
            userMarker.preserveAspect = true;
            userMarker.raycastTarget = false;
            userMarker.gameObject.SetActive(false);

            TMP_Text mapNotice = CreateText(
                "MapNotice",
                viewport,
                string.Empty,
                20,
                TextAlignmentOptions.Center);
            RectTransform noticeRect = mapNotice.rectTransform;
            noticeRect.anchorMin = new Vector2(0.1f, 0.5f);
            noticeRect.anchorMax = new Vector2(0.9f, 0.5f);
            noticeRect.pivot = new Vector2(0.5f, 0.5f);
            noticeRect.sizeDelta = new Vector2(0f, 120f);
            noticeRect.anchoredPosition = Vector2.zero;
            mapNotice.color = new Color32(246, 237, 217, 255);
            mapNotice.gameObject.SetActive(false);

            RectTransform topBar = CreateRect("TopBar", root.transform);
            topBar.anchorMin = new Vector2(0f, 1f);
            topBar.anchorMax = new Vector2(1f, 1f);
            topBar.pivot = new Vector2(0.5f, 1f);
            topBar.sizeDelta = new Vector2(0f, 72f);
            topBar.anchoredPosition = Vector2.zero;
            topBar.gameObject.AddComponent<Image>().color = new Color32(18, 17, 15, 238);

            Button codexButton = CreateButton("CodexButton", topBar, "도감", new Vector2(92f, 48f));
            RectTransform codexButtonRect = codexButton.GetComponent<RectTransform>();
            codexButtonRect.anchorMin = new Vector2(0f, 0.5f);
            codexButtonRect.anchorMax = new Vector2(0f, 0.5f);
            codexButtonRect.pivot = new Vector2(0f, 0.5f);
            codexButtonRect.anchoredPosition = new Vector2(16f, 0f);

            Button arButton = CreateButton("ArButton", topBar, "AR", new Vector2(88f, 48f));
            RectTransform arButtonRect = arButton.GetComponent<RectTransform>();
            arButtonRect.anchorMin = new Vector2(1f, 0.5f);
            arButtonRect.anchorMax = new Vector2(1f, 0.5f);
            arButtonRect.pivot = new Vector2(1f, 0.5f);
            arButtonRect.anchoredPosition = new Vector2(-128f, 0f);

            TMP_Text title = CreateText("Title", topBar, "픽셀 로드", 24, TextAlignmentOptions.Center);
            title.rectTransform.anchorMin = new Vector2(0.24f, 0f);
            title.rectTransform.anchorMax = new Vector2(0.76f, 1f);
            title.rectTransform.offsetMin = Vector2.zero;
            title.rectTransform.offsetMax = Vector2.zero;

            Button pixelToggle = CreateButton("PixelToggle", topBar, "픽셀 OFF", new Vector2(104f, 48f));
            RectTransform pixelRect = pixelToggle.GetComponent<RectTransform>();
            pixelRect.anchorMin = new Vector2(1f, 0.5f);
            pixelRect.anchorMax = new Vector2(1f, 0.5f);
            pixelRect.pivot = new Vector2(1f, 0.5f);
            pixelRect.anchoredPosition = new Vector2(-16f, 0f);
            TMP_Text pixelText = pixelToggle.GetComponentInChildren<TMP_Text>();

            TMP_Text status = CreateText("StatusText", root.transform, "GPS", 18, TextAlignmentOptions.Left);
            RectTransform statusRect = status.rectTransform;
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.sizeDelta = new Vector2(-32f, 28f);
            statusRect.anchoredPosition = new Vector2(0f, 14f);
            status.color = new Color32(246, 237, 217, 255);

            Button attribution = CreateButton(
                "MapAttribution",
                root.transform,
                "© OpenStreetMap contributors",
                new Vector2(310f, 30f));
            RectTransform attributionRect = attribution.GetComponent<RectTransform>();
            attributionRect.anchorMin = new Vector2(1f, 0f);
            attributionRect.anchorMax = new Vector2(1f, 0f);
            attributionRect.pivot = new Vector2(1f, 0f);
            attributionRect.anchoredPosition = new Vector2(-10f, 172f);
            attribution.transition = Selectable.Transition.None;
            attribution.GetComponent<Image>().color = new Color32(18, 17, 15, 210);
            TMP_Text attributionText = attribution.GetComponentInChildren<TMP_Text>();
            attributionText.fontSize = 13;
            attributionText.color = new Color32(246, 237, 217, 255);

            RectTransform infoPanel = CreateRect("SpotInfoPanel", root.transform);
            infoPanel.anchorMin = new Vector2(0f, 0f);
            infoPanel.anchorMax = new Vector2(1f, 0f);
            infoPanel.pivot = new Vector2(0.5f, 0f);
            infoPanel.sizeDelta = new Vector2(-24f, 118f);
            infoPanel.anchoredPosition = new Vector2(0f, 44f);
            infoPanel.gameObject.AddComponent<Image>().color = new Color32(235, 222, 188, 246);

            TMP_Text selectedName = CreateText(
                "SelectedName",
                infoPanel,
                "랜드마크를 선택하세요",
                22,
                TextAlignmentOptions.Left);
            selectedName.rectTransform.anchorMin = new Vector2(0f, 0.45f);
            selectedName.rectTransform.anchorMax = new Vector2(0.68f, 1f);
            selectedName.rectTransform.offsetMin = new Vector2(18f, 0f);
            selectedName.rectTransform.offsetMax = new Vector2(-8f, -8f);

            TMP_Text selectedDescription = CreateText(
                "SelectedDescription",
                infoPanel,
                "지도 위 마커를 누르면 도감 정보가 열립니다.",
                17,
                TextAlignmentOptions.Left);
            selectedDescription.rectTransform.anchorMin = new Vector2(0f, 0f);
            selectedDescription.rectTransform.anchorMax = new Vector2(0.72f, 0.55f);
            selectedDescription.rectTransform.offsetMin = new Vector2(18f, 10f);
            selectedDescription.rectTransform.offsetMax = new Vector2(-8f, -2f);

            TMP_Text selectedDistance = CreateText(
                "SelectedDistance",
                infoPanel,
                "-",
                18,
                TextAlignmentOptions.Right);
            selectedDistance.rectTransform.anchorMin = new Vector2(0.72f, 0f);
            selectedDistance.rectTransform.anchorMax = new Vector2(1f, 1f);
            selectedDistance.rectTransform.offsetMin = new Vector2(0f, 10f);
            selectedDistance.rectTransform.offsetMax = new Vector2(-18f, -10f);

            GameObject codexPanel = BuildCodexPanel(
                root.transform,
                out TMP_Text progress,
                out Button closeButton,
                out RectTransform content);

            Assign(bindings, "mapViewport", viewport);
            Assign(bindings, "mapInput", mapInput);
            Assign(bindings, "liveMapImage", liveMapImage);
            Assign(bindings, "markerRoot", markerRoot);
            Assign(bindings, "userMarker", userMarker);
            Assign(bindings, "mapNoticeText", mapNotice);
            Assign(bindings, "codexButton", codexButton);
            Assign(bindings, "arButton", arButton);
            Assign(bindings, "titleText", title);
            Assign(bindings, "pixelToggleButton", pixelToggle);
            Assign(bindings, "pixelToggleText", pixelText);
            Assign(bindings, "statusText", status);
            Assign(bindings, "attributionButton", attribution);
            Assign(bindings, "attributionText", attributionText);
            Assign(bindings, "selectedNameText", selectedName);
            Assign(bindings, "selectedDescriptionText", selectedDescription);
            Assign(bindings, "selectedDistanceText", selectedDistance);
            Assign(bindings, "codexPanel", codexPanel);
            Assign(bindings, "progressText", progress);
            Assign(bindings, "codexCloseButton", closeButton);
            Assign(bindings, "codexContent", content);
            Assign(bindings, "landmarkMarkerPrefab", markerPrefab);
            Assign(bindings, "landmarkCardPrefab", cardPrefab);

            codexPanel.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, RootPrefabPath);
            Object.DestroyImmediate(root);
        }

        private static GameObject BuildCodexPanel(
            Transform parent,
            out TMP_Text progress,
            out Button closeButton,
            out RectTransform content)
        {
            RectTransform overlay = CreateRect("CodexPanel", parent);
            Stretch(overlay);
            overlay.gameObject.AddComponent<Image>().color = new Color32(15, 14, 12, 230);

            RectTransform window = CreateRect("Window", overlay);
            window.anchorMin = new Vector2(0.08f, 0.08f);
            window.anchorMax = new Vector2(0.92f, 0.92f);
            window.pivot = new Vector2(0.5f, 0.5f);
            window.offsetMin = Vector2.zero;
            window.offsetMax = Vector2.zero;
            window.gameObject.AddComponent<Image>().color = new Color32(236, 224, 194, 255);

            RectTransform header = CreateRect("Header", window);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, 72f);
            header.anchoredPosition = Vector2.zero;
            header.gameObject.AddComponent<Image>().color = new Color32(18, 17, 15, 255);

            TMP_Text title = CreateText("Title", header, "수집 도감", 24, TextAlignmentOptions.Left);
            title.rectTransform.anchorMin = new Vector2(0f, 0f);
            title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.offsetMin = new Vector2(18f, 0f);
            title.rectTransform.offsetMax = Vector2.zero;

            progress = CreateText("Progress", header, "0 / 0", 20, TextAlignmentOptions.Right);
            progress.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            progress.rectTransform.anchorMax = new Vector2(1f, 1f);
            progress.rectTransform.offsetMin = Vector2.zero;
            progress.rectTransform.offsetMax = new Vector2(-96f, 0f);

            closeButton = CreateButton("CloseButton", header, "X", new Vector2(56f, 44f));
            RectTransform closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 0.5f);
            closeRect.anchorMax = new Vector2(1f, 0.5f);
            closeRect.pivot = new Vector2(1f, 0.5f);
            closeRect.anchoredPosition = new Vector2(-16f, 0f);

            ScrollRect scroll = CreateRect("Scroll", window).gameObject.AddComponent<ScrollRect>();
            RectTransform scrollRect = scroll.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(16f, 16f);
            scrollRect.offsetMax = new Vector2(-16f, -88f);
            scroll.viewport = scrollRect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.gameObject.AddComponent<Image>().color = new Color32(236, 224, 194, 255);
            scroll.gameObject.AddComponent<RectMask2D>();

            content = CreateRect("Content", scrollRect);
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

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<RectTransform>();
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            Image image = CreateRect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static Button CreateButton(string name, Transform parent, string label, Vector2 size)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.sizeDelta = size;
            Image background = rect.gameObject.AddComponent<Image>();
            background.color = new Color32(239, 228, 199, 255);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            TMP_Text text = CreateText("Label", rect, label, 18, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            return button;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            string value,
            int size,
            TextAlignmentOptions alignment)
        {
            TextMeshProUGUI text = CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.color = new Color32(18, 17, 15, 255);
            text.raycastTarget = false;
            if (font != null)
            {
                text.font = font;
            }

            return text;
        }

        private static void SetCentered(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Assign(Object target, string propertyName, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new MissingReferenceException(
                    target.GetType().Name + " does not have serialized field '" + propertyName + "'.");
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string parent, string child)
        {
            string fullPath = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
