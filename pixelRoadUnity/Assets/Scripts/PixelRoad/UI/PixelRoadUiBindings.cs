using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PixelRoad.UI
{
    /// <summary>
    /// PixelRoadUIRoot.prefab의 고정 UI 참조 모음.
    /// 계층 이름을 바꿔도 런타임 코드가 깨지지 않도록 모든 참조를 직렬화한다.
    ///
    /// 프리팹은 Canvas와 EventSystem까지 들고 있는 자립형이다. 런타임은 이 프리팹을 그대로
    /// Instantiate 할 뿐 Canvas·EventSystem·마커 스프라이트를 코드로 만들지 않는다.
    /// </summary>
    public sealed class PixelRoadUiBindings : MonoBehaviour
    {
        [Header("Canvas")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasScaler canvasScaler;
        [SerializeField] private GraphicRaycaster graphicRaycaster;
        [SerializeField] private EventSystem eventSystem;

        [Header("Map")]
        [SerializeField] private RectTransform mapViewport;
        [SerializeField] private PixelRoadMapInput mapInput;
        /// <summary>라이브 벡터 지도 타일이 그려지는 판. 렌더러가 준비되기 전에는 꺼 둔다.</summary>
        [SerializeField] private RawImage liveMapImage;
        /// <summary>랜드마크 마커를 붙이는 부모. 마커 위치는 지도 뷰가 바뀔 때마다 다시 계산한다.</summary>
        [SerializeField] private RectTransform markerRoot;
        [SerializeField] private Image userMarker;
        /// <summary>지도 로딩 상태나 사용 불가 사유를 알리는 안내 문구. 보여 줄 내용이 없으면 통째로 끈다.</summary>
        [SerializeField] private TMP_Text mapNoticeText;

        [Header("Follow user")]
        [SerializeField] private Button recenterButton;

        [Header("Distance indicator")]
        [SerializeField] private GameObject distanceIndicator;
        [SerializeField] private RectTransform distanceLine;
        [SerializeField] private TMP_Text distanceText;

        [Header("Landmark banner")]
        [SerializeField] private GameObject landmarkBanner;
        [SerializeField] private Image bannerLockIcon;
        [SerializeField] private TMP_Text bannerNameText;
        [SerializeField] private TMP_Text bannerDistanceText;
        [SerializeField] private Button bannerCardButton;
        [SerializeField] private Image bannerCardButtonBackground;
        [SerializeField] private TMP_Text bannerCardButtonLabel;
        [SerializeField] private Image bannerCardButtonArrow;

        [Header("Attribution")]
        [SerializeField] private Button attributionButton;
        [SerializeField] private TMP_Text attributionText;

        [Header("Gnb")]
        [SerializeField] private GnbView gnb;

        [Header("Codex")]
        [SerializeField] private CodexView codexView;

        [Header("Report")]
        [SerializeField] private ReportView reportView;

        [Header("Quit dialog")]
        [SerializeField] private QuitDialogView quitDialog;

        [Header("Unlock dialog")]
        [SerializeField] private UnlockDialogView unlockDialog;

        [Header("Dynamic prefabs")]
        [SerializeField] private LandmarkMarkerView landmarkMarkerPrefab;

        public Canvas Canvas => canvas;
        public CanvasScaler CanvasScaler => canvasScaler;
        public GraphicRaycaster GraphicRaycaster => graphicRaycaster;

        /// <summary>프리팹이 들고 있는 EventSystem. 씬에 이미 있으면 런타임이 이쪽을 제거한다.</summary>
        public EventSystem EventSystem => eventSystem;

        public RectTransform MapViewport => mapViewport;
        public PixelRoadMapInput MapInput => mapInput;
        public RawImage LiveMapImage => liveMapImage;
        public RectTransform MarkerRoot => markerRoot;
        public Image UserMarker => userMarker;
        public TMP_Text MapNoticeText => mapNoticeText;

        /// <summary>지도 우하단의 "현재 위치로" 버튼. 드래그로 풀린 추적을 다시 켠다.</summary>
        public Button RecenterButton => recenterButton;

        /// <summary>현재 위치와 선택된 랜드마크를 잇는 실선 + 남은거리 텍스트 묶음.</summary>
        public GameObject DistanceIndicator => distanceIndicator;

        /// <summary>피벗이 (0, 0.5)인 실선. 런타임이 길이와 각도만 바꾼다.</summary>
        public RectTransform DistanceLine => distanceLine;

        public TMP_Text DistanceText => distanceText;

        public GameObject LandmarkBanner => landmarkBanner;
        public Image BannerLockIcon => bannerLockIcon;
        public TMP_Text BannerNameText => bannerNameText;
        public TMP_Text BannerDistanceText => bannerDistanceText;
        public Button BannerCardButton => bannerCardButton;
        public Image BannerCardButtonBackground => bannerCardButtonBackground;
        public TMP_Text BannerCardButtonLabel => bannerCardButtonLabel;
        public Image BannerCardButtonArrow => bannerCardButtonArrow;
        public Button AttributionButton => attributionButton;
        public TMP_Text AttributionText => attributionText;
        public GnbView Gnb => gnb;
        public CodexView CodexView => codexView;

        /// <summary>AI 탐험 리포트 화면. 도감과 마찬가지로 지도 위를 덮는 패널이다.</summary>
        public ReportView ReportView => reportView;

        /// <summary>랜드마크를 처음 해금했을 때 뜨는 `랜드마크 발견!` 창.</summary>
        public UnlockDialogView UnlockDialog => unlockDialog;

        /// <summary>뒤로가기로 뜨는 종료 확인 창.</summary>
        public QuitDialogView QuitDialog => quitDialog;
        public LandmarkMarkerView LandmarkMarkerPrefab => landmarkMarkerPrefab;

        /// <summary>직렬화 참조가 하나라도 비어 있으면 즉시 예외를 던져 프리팹 설정 실수를 잡는다. 하위 뷰도 같이 검사한다.</summary>
        public void ValidateReferences()
        {
            Require(canvas, nameof(canvas));
            Require(canvasScaler, nameof(canvasScaler));
            Require(graphicRaycaster, nameof(graphicRaycaster));
            Require(eventSystem, nameof(eventSystem));
            Require(mapViewport, nameof(mapViewport));
            Require(mapInput, nameof(mapInput));
            Require(liveMapImage, nameof(liveMapImage));
            Require(markerRoot, nameof(markerRoot));
            Require(userMarker, nameof(userMarker));
            Require(mapNoticeText, nameof(mapNoticeText));
            Require(recenterButton, nameof(recenterButton));
            Require(distanceIndicator, nameof(distanceIndicator));
            Require(distanceLine, nameof(distanceLine));
            Require(distanceText, nameof(distanceText));
            Require(landmarkBanner, nameof(landmarkBanner));
            Require(bannerLockIcon, nameof(bannerLockIcon));
            Require(bannerNameText, nameof(bannerNameText));
            Require(bannerDistanceText, nameof(bannerDistanceText));
            Require(bannerCardButton, nameof(bannerCardButton));
            Require(bannerCardButtonBackground, nameof(bannerCardButtonBackground));
            Require(bannerCardButtonLabel, nameof(bannerCardButtonLabel));
            Require(bannerCardButtonArrow, nameof(bannerCardButtonArrow));
            Require(attributionButton, nameof(attributionButton));
            Require(attributionText, nameof(attributionText));
            Require(gnb, nameof(gnb));
            Require(codexView, nameof(codexView));
            Require(reportView, nameof(reportView));
            Require(quitDialog, nameof(quitDialog));
            Require(unlockDialog, nameof(unlockDialog));
            Require(landmarkMarkerPrefab, nameof(landmarkMarkerPrefab));
            gnb.ValidateReferences();
            codexView.ValidateReferences();
            reportView.ValidateReferences();
            quitDialog.ValidateReferences();
            unlockDialog.ValidateReferences();
        }

        /// <summary>참조가 비었으면 어느 필드인지 이름을 담아 예외를 던진다.</summary>
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
