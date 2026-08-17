using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PixelRoad.AR
{
    /// <summary>
    /// AROverlayUIRoot.prefab의 고정 UI 참조 모음. MapScene의 PixelRoadUiBindings와 같은 패턴이다.
    /// 계층 이름을 바꿔도 런타임 코드가 깨지지 않도록 모든 참조를 직렬화한다.
    ///
    /// 프리팹은 Canvas와 EventSystem까지 들고 있는 자립형이다. AROverlayView는 이 프리팹을
    /// 그대로 받아 쓸 뿐 UI를 코드로 만들지 않는다(랜드마크 핀만 LandmarkPinPrefab을 Instantiate).
    /// </summary>
    public sealed class AROverlayUiBindings : MonoBehaviour
    {
        [Header("Canvas")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasScaler canvasScaler;
        [SerializeField] private GraphicRaycaster graphicRaycaster;
        [SerializeField] private EventSystem eventSystem;

        [Header("Landmarks")]
        /// <summary>랜드마크 핀을 붙이는 부모. 화면 전체에 stretch돼 있다.</summary>
        [SerializeField] private RectTransform landmarkRoot;
        [SerializeField] private ARLandmarkPinView landmarkPinPrefab;

        [Header("Status")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text toastText;

        [Header("Focus direction")]
        [SerializeField] private Image focusDirectionArrow;

        [Header("Back")]
        [SerializeField] private Button backButton;

        [Header("Capture")]
        [SerializeField] private Button captureButton;
        [SerializeField] private Image captureButtonImage;

        [Header("Thumbnail")]
        [SerializeField] private GameObject thumbnailFrame;
        [SerializeField] private Button thumbnailButton;
        [SerializeField] private Image thumbnailImage;

        public Canvas Canvas => canvas;
        public CanvasScaler CanvasScaler => canvasScaler;
        public GraphicRaycaster GraphicRaycaster => graphicRaycaster;
        public EventSystem EventSystem => eventSystem;

        public RectTransform LandmarkRoot => landmarkRoot;
        public ARLandmarkPinView LandmarkPinPrefab => landmarkPinPrefab;

        public TMP_Text StatusText => statusText;
        public TMP_Text ToastText => toastText;

        public Image FocusDirectionArrow => focusDirectionArrow;

        public Button BackButton => backButton;

        public Button CaptureButton => captureButton;
        public Image CaptureButtonImage => captureButtonImage;

        public GameObject ThumbnailFrame => thumbnailFrame;
        public Button ThumbnailButton => thumbnailButton;
        public Image ThumbnailImage => thumbnailImage;

        /// <summary>직렬화 참조가 하나라도 비어 있으면 즉시 예외를 던져 프리팹 설정 실수를 잡는다.</summary>
        public void ValidateReferences()
        {
            Require(canvas, nameof(canvas));
            Require(canvasScaler, nameof(canvasScaler));
            Require(graphicRaycaster, nameof(graphicRaycaster));
            Require(eventSystem, nameof(eventSystem));
            Require(landmarkRoot, nameof(landmarkRoot));
            Require(landmarkPinPrefab, nameof(landmarkPinPrefab));
            Require(statusText, nameof(statusText));
            Require(toastText, nameof(toastText));
            Require(focusDirectionArrow, nameof(focusDirectionArrow));
            Require(backButton, nameof(backButton));
            Require(captureButton, nameof(captureButton));
            Require(captureButtonImage, nameof(captureButtonImage));
            Require(thumbnailFrame, nameof(thumbnailFrame));
            Require(thumbnailButton, nameof(thumbnailButton));
            Require(thumbnailImage, nameof(thumbnailImage));
        }

        private void Require(UnityEngine.Object reference, string fieldName)
        {
            if (reference == null)
            {
                throw new InvalidOperationException(
                    "AROverlayUIRoot prefab is missing the serialized reference '" + fieldName + "'.");
            }
        }
    }
}
