using PixelRoad.AR;
using PixelRoad.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PixelRoad.Editor
{
    /// <summary>
    /// AR 화면 UI를 PixelRoadUIRoot.prefab과 같은 "프리팹 참조" 방식으로 만들기 위한 에디터 도구.
    ///
    /// AROverlayView는 더 이상 UI를 코드로 만들지 않고 AROverlayUiBindings 참조를 그대로 쓴다.
    /// 이 프리팹들은 손으로 하나하나 배치하는 대신, 지금까지 AROverlayView/ARUiFactory에 박혀 있던
    /// 위치·크기·색 값을 그대로 옮겨 코드로 한 번에 생성한다 - 재생성·수정이 쉽고, 값이 어긋날 일이 없다.
    /// </summary>
    public static class ARUiPrefabBuilder
    {
        private const string ArScenePath = "Assets/Scenes/ARScene.unity";
        private const string PinPrefabPath = "Assets/Resources/PixelRoad/UI/ARLandmarkPinView.prefab";
        private const string OverlayPrefabPath = "Assets/Resources/PixelRoad/UI/AROverlayUIRoot.prefab";
        private const string PixelFontAssetPath = "Assets/Resources/PixelRoad/Fonts/Galmuri11 SDF.asset";

        // AROverlayView/ARUiFactory에 있던 값을 그대로 옮겨 왔다.
        private const float DefaultIconPixelSize = 96f;
        private const float CaptureButtonSize = 88f;
        private const float CaptureButtonBottomMargin = 48f;
        private const float ThumbnailWidth = 160f;
        private const float ThumbnailHeight = 220f;
        private const float ThumbnailMargin = 16f;
        private const float ThumbnailFrameBorder = 6f;
        private const float FocusDirectionArrowSize = 96f;
        private const float FocusDirectionArrowVerticalOffset = 180f;

        // PixelRoadUIRoot.prefab의 UnlockDialog(Dimmer/Panel/Surface/Title/Name/ConfirmButton) 값을
        // 그대로 옮겨 왔다 - 해금 알림창은 화면이 달라도 똑같이 보여야 한다.
        private const float DialogPanelWidth = 840f;
        private const float DialogPanelHeight = 460f;
        private const float DialogSurfaceInset = 6f;
        private const int DialogTitleFontSize = 46;
        private const int DialogNameFontSize = 44;
        private const int DialogButtonFontSize = 30;
        private const float DialogButtonHeight = 88f;
        private const float DialogButtonHorizontalMargin = 36f;
        private const float DialogButtonBottomMargin = 40f;

        private static readonly Color32 TextColor = new Color32(246, 237, 217, 255);
        private static readonly Color32 BackButtonBackground = new Color32(239, 228, 199, 255);
        private static readonly Color32 BackButtonLabel = new Color32(18, 17, 15, 255);
        private static readonly Color32 ThumbnailBackground = new Color32(18, 17, 15, 220);
        private static readonly Color32 DialogDimmerColor = new Color32(18, 17, 15, 200);
        private static readonly Color32 DialogPanelColor = new Color32(18, 17, 15, 255);
        private static readonly Color32 DialogSurfaceColor = new Color32(239, 229, 207, 255);
        private static readonly Color32 DialogConfirmButtonColor = new Color32(27, 139, 122, 255);

        /// <summary>ARLandmarkPinView.prefab과 AROverlayUIRoot.prefab을 코드로 다시 만들어 저장한다.</summary>
        [MenuItem("Tools/Pixel Road/AR/Rebuild AR UI Prefabs")]
        public static void RebuildAROverlayPrefabs()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PixelFontAssetPath);
            if (font == null)
            {
                Debug.LogError("[PixelRoad] Galmuri11 SDF 폰트 에셋을 찾지 못했습니다: " + PixelFontAssetPath);
                return;
            }

            ARLandmarkPinView pinPrefab = BuildLandmarkPinPrefab(font);
            if (pinPrefab == null)
            {
                return;
            }

            BuildOverlayRootPrefab(font, pinPrefab);
            Debug.Log("[PixelRoad] AR UI 프리팹을 다시 만들었습니다: " + PinPrefabPath + ", " + OverlayPrefabPath);
        }

        /// <summary>
        /// AR 씬을 "프리팹 참조" 구성으로 맞춘다: 예전에 손으로 만든 Canvas/EventSystem을 지우고
        /// AROverlayUIRoot 프리팹을 배치한 뒤, ARSceneController.uiBindings에 연결한다.
        /// AR Session/XR Origin 등 AR Foundation 계층은 건드리지 않는다.
        /// </summary>
        [MenuItem("Tools/Pixel Road/AR/Setup AR Scene")]
        public static void SetupARScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ArScenePath) == null)
            {
                Debug.LogWarning("[PixelRoad] AR 씬 파일이 없습니다: " + ArScenePath);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ArScenePath, OpenSceneMode.Additive);

            AROverlayUiBindings bindings = FindInScene<AROverlayUiBindings>(scene);
            int removed = RemoveDuplicateUiObjects(scene);

            if (bindings == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OverlayPrefabPath);
                if (prefab == null)
                {
                    Debug.LogError("[PixelRoad] 프리팹을 찾지 못했습니다: " + OverlayPrefabPath
                        + ". 먼저 Tools/Pixel Road/AR/Rebuild AR UI Prefabs 를 실행하세요.");
                    EditorSceneManager.CloseScene(scene, true);
                    return;
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = "AROverlayUIRoot";
                bindings = instance.GetComponent<AROverlayUiBindings>();
            }

            ARSceneController controller = FindInScene<ARSceneController>(scene);
            if (controller == null)
            {
                Debug.LogError("[PixelRoad] 씬에서 ARSceneController를 찾지 못했습니다: " + ArScenePath);
                EditorSceneManager.CloseScene(scene, true);
                return;
            }

            AssignReference(controller, "uiBindings", bindings);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
            Debug.Log("[PixelRoad] AR 씬을 구성했습니다(중복 UI " + removed
                + "개 제거, AROverlayUIRoot 배치 + ARSceneController 연결): " + ArScenePath);
        }

        private static ARLandmarkPinView BuildLandmarkPinPrefab(TMP_FontAsset font)
        {
            GameObject root = new GameObject("ARLandmarkPinView", typeof(RectTransform));
            RectTransform rootRect = (RectTransform)root.transform;
            AnchorCenter(rootRect);
            rootRect.sizeDelta = Vector2.zero;

            GameObject iconObject = new GameObject("Icon", typeof(RectTransform));
            iconObject.transform.SetParent(root.transform, false);
            RectTransform iconRect = (RectTransform)iconObject.transform;
            AnchorCenter(iconRect);
            iconRect.sizeDelta = new Vector2(DefaultIconPixelSize, DefaultIconPixelSize);
            Image icon = iconObject.AddComponent<Image>();
            icon.raycastTarget = true;
            icon.preserveAspect = true;
            Button button = iconObject.AddComponent<Button>();
            button.targetGraphic = icon;

            TMP_Text distanceLabel = CreateText(root.transform, "Distance", string.Empty, 18, font);
            RectTransform labelRect = distanceLabel.rectTransform;
            labelRect.anchorMin = new Vector2(0.5f, 0f);
            labelRect.anchorMax = new Vector2(0.5f, 0f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.sizeDelta = new Vector2(160f, 32f);
            labelRect.anchoredPosition = new Vector2(0f, -(DefaultIconPixelSize * 0.5f) - 4f);

            ARLandmarkPinView pinView = root.AddComponent<ARLandmarkPinView>();
            AssignReference(pinView, "icon", icon);
            AssignReference(pinView, "distanceLabel", distanceLabel);
            AssignReference(pinView, "button", button);

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, PinPrefabPath);
            Object.DestroyImmediate(root);
            return savedPrefab.GetComponent<ARLandmarkPinView>();
        }

        private static void BuildOverlayRootPrefab(TMP_FontAsset font, ARLandmarkPinView pinPrefab)
        {
            GameObject root = new GameObject(
                "AROverlayUIRoot",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(AROverlayUiBindings));

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GraphicRaycaster raycaster = root.GetComponent<GraphicRaycaster>();

            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystemObject.transform.SetParent(root.transform, false);
            EventSystem eventSystem = eventSystemObject.GetComponent<EventSystem>();

            RectTransform landmarkRoot = CreateRect(root.transform, "LandmarkRoot");
            Stretch(landmarkRoot);

            TMP_Text statusText = CreateText(root.transform, "StatusMessage", string.Empty, 22, font);
            statusText.raycastTarget = false;
            statusText.rectTransform.anchorMin = new Vector2(0.1f, 0.4f);
            statusText.rectTransform.anchorMax = new Vector2(0.9f, 0.6f);
            statusText.rectTransform.offsetMin = Vector2.zero;
            statusText.rectTransform.offsetMax = Vector2.zero;
            statusText.gameObject.SetActive(false);

            TMP_Text toastText = CreateText(root.transform, "Toast", string.Empty, 18, font);
            toastText.raycastTarget = false;
            toastText.rectTransform.anchorMin = new Vector2(0.1f, 0.68f);
            toastText.rectTransform.anchorMax = new Vector2(0.9f, 0.76f);
            toastText.rectTransform.offsetMin = Vector2.zero;
            toastText.rectTransform.offsetMax = Vector2.zero;
            toastText.gameObject.SetActive(false);

            GameObject focusArrowObject = new GameObject("FocusDirectionArrow", typeof(RectTransform), typeof(Image));
            focusArrowObject.transform.SetParent(root.transform, false);
            RectTransform focusArrowRect = (RectTransform)focusArrowObject.transform;
            AnchorCenter(focusArrowRect);
            focusArrowRect.sizeDelta = new Vector2(FocusDirectionArrowSize, FocusDirectionArrowSize);
            focusArrowRect.anchoredPosition = new Vector2(0f, -FocusDirectionArrowVerticalOffset);
            Image focusArrowImage = focusArrowObject.GetComponent<Image>();
            focusArrowImage.preserveAspect = true;
            focusArrowImage.raycastTarget = false;
            focusArrowObject.SetActive(false);

            Button backButton = CreateBackButton(root.transform, font);

            GameObject captureButtonObject = new GameObject("CaptureButton", typeof(RectTransform), typeof(Image), typeof(Button));
            captureButtonObject.transform.SetParent(root.transform, false);
            RectTransform captureRect = (RectTransform)captureButtonObject.transform;
            captureRect.anchorMin = new Vector2(0.5f, 0f);
            captureRect.anchorMax = new Vector2(0.5f, 0f);
            captureRect.pivot = new Vector2(0.5f, 0f);
            captureRect.sizeDelta = new Vector2(CaptureButtonSize, CaptureButtonSize);
            captureRect.anchoredPosition = new Vector2(0f, CaptureButtonBottomMargin);
            Image captureImage = captureButtonObject.GetComponent<Image>();
            captureImage.color = Color.white;
            Button captureButton = captureButtonObject.GetComponent<Button>();
            captureButton.targetGraphic = captureImage;

            (GameObject thumbnailFrame, Button thumbnailButton, Image thumbnailImage) =
                CreateThumbnailFrame(root.transform);

            UnlockDialogView unlockDialog = BuildUnlockDialog(root.transform, font);

            AROverlayUiBindings bindings = root.GetComponent<AROverlayUiBindings>();
            AssignReference(bindings, "canvas", canvas);
            AssignReference(bindings, "canvasScaler", scaler);
            AssignReference(bindings, "graphicRaycaster", raycaster);
            AssignReference(bindings, "eventSystem", eventSystem);
            AssignReference(bindings, "landmarkRoot", landmarkRoot);
            AssignReference(bindings, "landmarkPinPrefab", pinPrefab);
            AssignReference(bindings, "statusText", statusText);
            AssignReference(bindings, "toastText", toastText);
            AssignReference(bindings, "focusDirectionArrow", focusArrowImage);
            AssignReference(bindings, "backButton", backButton);
            AssignReference(bindings, "captureButton", captureButton);
            AssignReference(bindings, "captureButtonImage", captureImage);
            AssignReference(bindings, "thumbnailFrame", thumbnailFrame);
            AssignReference(bindings, "thumbnailButton", thumbnailButton);
            AssignReference(bindings, "thumbnailImage", thumbnailImage);
            AssignReference(bindings, "unlockDialog", unlockDialog);

            PrefabUtility.SaveAsPrefabAsset(root, OverlayPrefabPath);
            Object.DestroyImmediate(root);
        }

        private static Button CreateBackButton(Transform parent, TMP_FontAsset font)
        {
            GameObject buttonObject = new GameObject("BackButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(112f, 52f);
            rect.anchoredPosition = new Vector2(16f, 16f);

            Image background = buttonObject.GetComponent<Image>();
            background.color = BackButtonBackground;
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = background;

            TMP_Text label = CreateText(buttonObject.transform, "Label", "지도로", 18, font);
            label.color = BackButtonLabel;
            Stretch(label.rectTransform);

            return button;
        }

        private static (GameObject frame, Button button, Image photo) CreateThumbnailFrame(Transform parent)
        {
            GameObject frame = new GameObject("CaptureThumbnailFrame", typeof(RectTransform), typeof(Image), typeof(Button));
            frame.transform.SetParent(parent, false);
            RectTransform frameRect = (RectTransform)frame.transform;
            frameRect.anchorMin = new Vector2(1f, 0f);
            frameRect.anchorMax = new Vector2(1f, 0f);
            frameRect.pivot = new Vector2(1f, 0f);
            frameRect.sizeDelta = new Vector2(ThumbnailWidth, ThumbnailHeight);
            frameRect.anchoredPosition = new Vector2(-ThumbnailMargin, ThumbnailMargin);

            Image frameBackground = frame.GetComponent<Image>();
            frameBackground.color = ThumbnailBackground;
            Button frameButton = frame.GetComponent<Button>();
            frameButton.targetGraphic = frameBackground;

            RectTransform photoRect = CreateRect(frame.transform, "Photo");
            photoRect.anchorMin = Vector2.zero;
            photoRect.anchorMax = Vector2.one;
            photoRect.offsetMin = new Vector2(ThumbnailFrameBorder, ThumbnailFrameBorder);
            photoRect.offsetMax = new Vector2(-ThumbnailFrameBorder, -ThumbnailFrameBorder);
            Image photo = photoRect.gameObject.AddComponent<Image>();
            photo.preserveAspect = true;
            photo.raycastTarget = false;

            frame.SetActive(false);
            return (frame, frameButton, photo);
        }

        /// <summary>
        /// MapScene의 UnlockDialogView와 같은 클래스를 씌운 AR 전용 인스턴스. PixelRoadUIRoot.prefab의
        /// UnlockDialog(딤머 - 어두운 Panel - 안쪽 크림색 Surface - 제목/이름/확인 버튼) 계층·색·크기를
        /// 그대로 옮겨서, 클래스는 재사용하되 화면마다 모양이 달라 보이지 않게 한다.
        /// </summary>
        private static UnlockDialogView BuildUnlockDialog(Transform parent, TMP_FontAsset font)
        {
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            GameObject root = new GameObject("UnlockDialog", typeof(RectTransform), typeof(UnlockDialogView));
            root.transform.SetParent(parent, false);
            Stretch((RectTransform)root.transform);

            GameObject dimmerObject = new GameObject("Dimmer", typeof(RectTransform), typeof(Image));
            dimmerObject.transform.SetParent(root.transform, false);
            Stretch((RectTransform)dimmerObject.transform);
            Image dimmer = dimmerObject.GetComponent<Image>();
            dimmer.sprite = uiSprite;
            dimmer.color = DialogDimmerColor;
            dimmer.raycastTarget = true;

            GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            RectTransform panelRect = (RectTransform)panel.transform;
            AnchorCenter(panelRect);
            panelRect.sizeDelta = new Vector2(DialogPanelWidth, DialogPanelHeight);
            Image panelImage = panel.GetComponent<Image>();
            panelImage.sprite = uiSprite;
            panelImage.color = DialogPanelColor;

            GameObject surface = new GameObject("Surface", typeof(RectTransform), typeof(Image));
            surface.transform.SetParent(panel.transform, false);
            RectTransform surfaceRect = (RectTransform)surface.transform;
            surfaceRect.anchorMin = Vector2.zero;
            surfaceRect.anchorMax = Vector2.one;
            surfaceRect.offsetMin = new Vector2(DialogSurfaceInset, DialogSurfaceInset);
            surfaceRect.offsetMax = new Vector2(-DialogSurfaceInset, -DialogSurfaceInset);
            Image surfaceImage = surface.GetComponent<Image>();
            surfaceImage.sprite = uiSprite;
            surfaceImage.color = DialogSurfaceColor;

            TMP_Text titleText = CreateText(surface.transform, "Title", "랜드마크 발견!", DialogTitleFontSize, font);
            titleText.color = Color.white;
            RectTransform titleRect = titleText.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(720f, 80f);
            titleRect.anchoredPosition = new Vector2(0f, -56f);

            TMP_Text landmarkNameText = CreateText(surface.transform, "Name", "[랜드마크]", DialogNameFontSize, font);
            landmarkNameText.color = Color.white;
            RectTransform nameRect = landmarkNameText.rectTransform;
            nameRect.anchorMin = new Vector2(0.5f, 1f);
            nameRect.anchorMax = new Vector2(0.5f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.sizeDelta = new Vector2(720f, 80f);
            nameRect.anchoredPosition = new Vector2(0f, -160f);

            GameObject confirmButtonObject = new GameObject("ConfirmButton", typeof(RectTransform), typeof(Image), typeof(Button));
            confirmButtonObject.transform.SetParent(surface.transform, false);
            RectTransform confirmRect = (RectTransform)confirmButtonObject.transform;
            confirmRect.anchorMin = new Vector2(0f, 0f);
            confirmRect.anchorMax = new Vector2(1f, 0f);
            confirmRect.pivot = new Vector2(0.5f, 0f);
            confirmRect.sizeDelta = new Vector2(-DialogButtonHorizontalMargin * 2f, DialogButtonHeight);
            confirmRect.anchoredPosition = new Vector2(0f, DialogButtonBottomMargin);
            Image confirmBackground = confirmButtonObject.GetComponent<Image>();
            confirmBackground.sprite = uiSprite;
            confirmBackground.color = DialogConfirmButtonColor;
            Button confirmButton = confirmButtonObject.GetComponent<Button>();
            confirmButton.targetGraphic = confirmBackground;

            TMP_Text confirmLabel = CreateText(confirmButtonObject.transform, "Label", "확인", DialogButtonFontSize, font);
            confirmLabel.color = Color.white;
            Stretch(confirmLabel.rectTransform);

            UnlockDialogView unlockDialog = root.GetComponent<UnlockDialogView>();
            AssignReference(unlockDialog, "root", root);
            AssignReference(unlockDialog, "dimmer", dimmer);
            AssignReference(unlockDialog, "titleText", titleText);
            AssignReference(unlockDialog, "landmarkNameText", landmarkNameText);
            AssignReference(unlockDialog, "confirmButton", confirmButton);

            root.SetActive(false);
            return unlockDialog;
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void AnchorCenter(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static TMP_Text CreateText(Transform parent, string name, string value, int fontSize, TMP_FontAsset font)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.font = font;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.color = TextColor;
            return text;
        }

        /// <summary>프리팹이 들고 오는 Canvas·EventSystem과 겹치는 씬 오브젝트를 지운다.</summary>
        private static int RemoveDuplicateUiObjects(Scene scene)
        {
            int removed = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                GameObject candidate = roots[index];
                if (candidate.GetComponentInChildren<AROverlayUiBindings>(true) != null)
                {
                    continue;
                }

                if (candidate.GetComponent<Canvas>() == null
                    && candidate.GetComponent<EventSystem>() == null)
                {
                    continue;
                }

                Object.DestroyImmediate(candidate);
                removed++;
            }

            return removed;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                T found = roots[index].GetComponentInChildren<T>(true);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void AssignReference(Object target, string fieldName, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError("[PixelRoad] " + target.GetType().Name + " 에 직렬화 필드 '" + fieldName + "' 가 없습니다.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
