using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelRoad.UI
{
    /// <summary>하단 GNB 탭 종류. 배열 인덱스로 그대로 쓰므로 값을 바꾸지 말 것.</summary>
    public enum GnbTab
    {
        Map = 0,
        Codex = 1,
        Report = 2,
        Ar = 3,
    }

    /// <summary>
    /// 와이어프레임 main_지도의 하단 Gnb.
    ///
    /// 탭 상태는 셋이다.
    /// - select: 현재 열려 있는 탭(파랑)
    /// - active: 쓸 수 있지만 현재 탭은 아님(흰색)
    /// - deserbled: 조건을 만족하지 못해 못 쓰는 탭(회색)
    ///
    /// 상태 계산은 프레임마다 돌지 않고 상태가 바뀔 때만 색을 다시 칠한다.
    /// </summary>
    public sealed class GnbView : MonoBehaviour
    {
        public const int TabCount = 4;

        private static readonly Color32 SelectColor = new Color32(100, 172, 158, 255);
        private static readonly Color32 ActiveColor = new Color32(255, 255, 255, 255);
        private static readonly Color32 DisabledColor = new Color32(142, 142, 142, 255);

        [Header("Map")]
        [SerializeField] private Button mapButton;
        [SerializeField] private Image mapIcon;
        [SerializeField] private TMP_Text mapLabel;

        [Header("Codex")]
        [SerializeField] private Button codexButton;
        [SerializeField] private Image codexIcon;
        [SerializeField] private TMP_Text codexLabel;

        [Header("Report")]
        [SerializeField] private Button reportButton;
        [SerializeField] private Image reportIcon;
        [SerializeField] private TMP_Text reportLabel;
        /// <summary>신규 알림이 있을 때만 켜는 빨간 점. 리포트 탭에만 있다.</summary>
        [SerializeField] private GameObject reportBadge;

        [Header("AR")]
        [SerializeField] private Button arButton;
        [SerializeField] private Image arIcon;
        [SerializeField] private TMP_Text arLabel;

        private readonly Image[] icons = new Image[TabCount];
        private readonly TMP_Text[] labels = new TMP_Text[TabCount];
        private readonly Button[] buttons = new Button[TabCount];
        private readonly bool[] tabEnabled = new bool[TabCount];
        private GnbTab currentTab = GnbTab.Map;
        private bool initialized;

        /// <summary>쓸 수 있는 탭을 눌렀을 때 발생한다.</summary>
        public event Action<GnbTab> TabSelected;

        /// <summary>
        /// 조건을 만족하지 못해 못 쓰는 탭을 눌렀을 때 발생한다.
        /// 회색 탭이 아무 반응도 없으면 고장으로 보이므로, 왜 못 쓰는지 알려 줄 기회를 준다.
        /// </summary>
        public event Action<GnbTab> TabBlocked;

        public GnbTab CurrentTab
        {
            get { return currentTab; }
        }

        /// <summary>직렬화 참조가 하나라도 비어 있으면 즉시 예외를 던져 프리팹 설정 실수를 잡는다.</summary>
        public void ValidateReferences()
        {
            Require(mapButton, nameof(mapButton));
            Require(mapIcon, nameof(mapIcon));
            Require(mapLabel, nameof(mapLabel));
            Require(codexButton, nameof(codexButton));
            Require(codexIcon, nameof(codexIcon));
            Require(codexLabel, nameof(codexLabel));
            Require(reportButton, nameof(reportButton));
            Require(reportIcon, nameof(reportIcon));
            Require(reportLabel, nameof(reportLabel));
            Require(reportBadge, nameof(reportBadge));
            Require(arButton, nameof(arButton));
            Require(arIcon, nameof(arIcon));
            Require(arLabel, nameof(arLabel));
        }

        /// <summary>선택 이벤트를 연결한다. 프리팹 인스턴스화 직후 한 번만 부른다.</summary>
        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            Bind(GnbTab.Map, mapButton, mapIcon, mapLabel);
            Bind(GnbTab.Codex, codexButton, codexIcon, codexLabel);
            Bind(GnbTab.Report, reportButton, reportIcon, reportLabel);
            Bind(GnbTab.Ar, arButton, arIcon, arLabel);

            SetBadgeVisible(GnbTab.Report, false);
            ApplyAllVisuals();
        }

        /// <summary>현재 열려 있는 탭을 지정한다. 비활성 탭은 무시한다.</summary>
        public void SetCurrent(GnbTab tab)
        {
            if (currentTab == tab || !tabEnabled[(int)tab])
            {
                return;
            }

            currentTab = tab;
            ApplyAllVisuals();
        }

        /// <summary>
        /// 탭의 사용 가능 여부를 지정한다.
        ///
        /// <see cref="Button.interactable"/>은 건드리지 않는다. Unity가 그 값을 끄면 클릭 자체를 삼켜
        /// <see cref="TabBlocked"/>를 알릴 수 없고, 회색 탭이 아무 반응도 없는 상태가 되기 때문이다.
        /// 사용 가능 여부는 <see cref="tabEnabled"/>로만 관리하고 색으로 드러낸다.
        /// </summary>
        public void SetInteractable(GnbTab tab, bool value)
        {
            int index = (int)tab;
            if (tabEnabled[index] == value)
            {
                return;
            }

            tabEnabled[index] = value;
            ApplyVisual(index);
        }

        /// <summary>해당 탭을 지금 누를 수 있는지 알려 준다.</summary>
        public bool IsInteractable(GnbTab tab)
        {
            return tabEnabled[(int)tab];
        }

        /// <summary>알림 점을 켜고 끈다. 배지가 있는 리포트 탭에만 먹히고 나머지는 무시한다.</summary>
        public void SetBadgeVisible(GnbTab tab, bool value)
        {
            if (tab != GnbTab.Report || reportBadge == null)
            {
                return;
            }

            if (reportBadge.activeSelf != value)
            {
                reportBadge.SetActive(value);
            }
        }

        /// <summary>
        /// 탭 하나의 버튼·아이콘·라벨을 인덱스 배열에 담고 클릭을 연결한다. 초기 사용 여부는 버튼 설정을 따른다.
        /// 버튼 자체는 늘 눌리는 상태로 두고(비활성 탭도 클릭을 받아야 안내를 띄울 수 있다),
        /// 못 쓰는 탭인지는 <see cref="HandleTap"/>이 판단한다.
        /// </summary>
        private void Bind(GnbTab tab, Button button, Image icon, TMP_Text label)
        {
            int index = (int)tab;
            buttons[index] = button;
            icons[index] = icon;
            labels[index] = label;
            tabEnabled[index] = button.interactable;
            button.interactable = true;
            button.onClick.AddListener(() => HandleTap(tab));
        }

        /// <summary>탭 클릭을 받아 쓸 수 있으면 TabSelected를, 못 쓰면 TabBlocked를 알린다.</summary>
        private void HandleTap(GnbTab tab)
        {
            if (!tabEnabled[(int)tab])
            {
                TabBlocked?.Invoke(tab);
                return;
            }

            TabSelected?.Invoke(tab);
        }

        /// <summary>네 탭의 색을 한꺼번에 다시 칠한다. 현재 탭이 바뀌었을 때 쓴다.</summary>
        private void ApplyAllVisuals()
        {
            for (int index = 0; index < TabCount; index++)
            {
                ApplyVisual(index);
            }
        }

        /// <summary>탭 하나의 아이콘과 라벨에 현재 상태 색을 입힌다.</summary>
        private void ApplyVisual(int index)
        {
            Color32 color = ResolveColor(index);
            Image icon = icons[index];
            if (icon != null)
            {
                icon.color = color;
            }

            TMP_Text label = labels[index];
            if (label != null)
            {
                label.color = color;
            }
        }

        /// <summary>deserbled / select / active 순으로 탭 색을 고른다.</summary>
        private Color32 ResolveColor(int index)
        {
            if (!tabEnabled[index])
            {
                return DisabledColor;
            }

            return index == (int)currentTab ? SelectColor : ActiveColor;
        }

        /// <summary>참조가 비었으면 어느 필드인지 이름을 담아 예외를 던진다.</summary>
        private void Require(UnityEngine.Object reference, string fieldName)
        {
            if (reference == null)
            {
                throw new InvalidOperationException(
                    "GnbView is missing the serialized reference '" + fieldName + "'.");
            }
        }
    }
}
