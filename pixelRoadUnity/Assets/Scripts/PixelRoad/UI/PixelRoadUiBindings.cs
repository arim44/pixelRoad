using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelRoad.UI
{
    /// <summary>
    /// PixelRoadUIRoot.prefab의 고정 UI 참조 모음.
    /// 계층 이름을 바꿔도 런타임 코드가 깨지지 않도록 모든 참조를 직렬화한다.
    /// </summary>
    public sealed class PixelRoadUiBindings : MonoBehaviour
    {
        [Header("Map")]
        [SerializeField] private RectTransform mapViewport;
        [SerializeField] private PixelRoadMapInput mapInput;
        [SerializeField] private RawImage liveMapImage;
        [SerializeField] private RectTransform markerRoot;
        [SerializeField] private Image userMarker;
        [SerializeField] private TMP_Text mapNoticeText;

        [Header("Top and navigation")]
        [SerializeField] private Button codexButton;
        [SerializeField] private Button arButton;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Button pixelToggleButton;
        [SerializeField] private TMP_Text pixelToggleText;

        [Header("Status and selection")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button attributionButton;
        [SerializeField] private TMP_Text attributionText;
        [SerializeField] private TMP_Text selectedNameText;
        [SerializeField] private TMP_Text selectedDescriptionText;
        [SerializeField] private TMP_Text selectedDistanceText;

        [Header("Codex")]
        [SerializeField] private GameObject codexPanel;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private Button codexCloseButton;
        [SerializeField] private RectTransform codexContent;

        [Header("Dynamic prefabs")]
        [SerializeField] private LandmarkMarkerView landmarkMarkerPrefab;
        [SerializeField] private LandmarkCardView landmarkCardPrefab;

        public RectTransform MapViewport => mapViewport;
        public PixelRoadMapInput MapInput => mapInput;
        public RawImage LiveMapImage => liveMapImage;
        public RectTransform MarkerRoot => markerRoot;
        public Image UserMarker => userMarker;
        public TMP_Text MapNoticeText => mapNoticeText;
        public Button CodexButton => codexButton;
        public Button ArButton => arButton;
        public TMP_Text TitleText => titleText;
        public Button PixelToggleButton => pixelToggleButton;
        public TMP_Text PixelToggleText => pixelToggleText;
        public TMP_Text StatusText => statusText;
        public Button AttributionButton => attributionButton;
        public TMP_Text AttributionText => attributionText;
        public TMP_Text SelectedNameText => selectedNameText;
        public TMP_Text SelectedDescriptionText => selectedDescriptionText;
        public TMP_Text SelectedDistanceText => selectedDistanceText;
        public GameObject CodexPanel => codexPanel;
        public TMP_Text ProgressText => progressText;
        public Button CodexCloseButton => codexCloseButton;
        public RectTransform CodexContent => codexContent;
        public LandmarkMarkerView LandmarkMarkerPrefab => landmarkMarkerPrefab;
        public LandmarkCardView LandmarkCardPrefab => landmarkCardPrefab;

        public void ValidateReferences()
        {
            Require(mapViewport, nameof(mapViewport));
            Require(mapInput, nameof(mapInput));
            Require(liveMapImage, nameof(liveMapImage));
            Require(markerRoot, nameof(markerRoot));
            Require(userMarker, nameof(userMarker));
            Require(mapNoticeText, nameof(mapNoticeText));
            Require(codexButton, nameof(codexButton));
            Require(arButton, nameof(arButton));
            Require(titleText, nameof(titleText));
            Require(pixelToggleButton, nameof(pixelToggleButton));
            Require(pixelToggleText, nameof(pixelToggleText));
            Require(statusText, nameof(statusText));
            Require(attributionButton, nameof(attributionButton));
            Require(attributionText, nameof(attributionText));
            Require(selectedNameText, nameof(selectedNameText));
            Require(selectedDescriptionText, nameof(selectedDescriptionText));
            Require(selectedDistanceText, nameof(selectedDistanceText));
            Require(codexPanel, nameof(codexPanel));
            Require(progressText, nameof(progressText));
            Require(codexCloseButton, nameof(codexCloseButton));
            Require(codexContent, nameof(codexContent));
            Require(landmarkMarkerPrefab, nameof(landmarkMarkerPrefab));
            Require(landmarkCardPrefab, nameof(landmarkCardPrefab));
        }

        private void Require(UnityEngine.Object reference, string fieldName)
        {
            if (reference == null)
            {
                throw new InvalidOperationException(
                    "PixelRoadUIRoot prefab is missing the serialized reference '" + fieldName + "'.");
            }
        }
    }
}
