using System;
using PixelRoad.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelRoad.UI
{
    /// <summary>
    /// 도감 카드를 눌렀을 때 도감·지도 위에 겹쳐 뜨는 상세 보기.
    ///
    /// 앞면은 사진과 한 줄 소개, 뒷면은 역사·카테고리·주소를 보여 준다.
    /// 두 면의 배치가 서로 달라서 각각을 통째로 켜고 끄는 방식으로 뒤집는다.
    /// </summary>
    public sealed class CodexDetailView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Button dimmer;

        [Header("앞면")]
        [SerializeField] private GameObject front;
        [SerializeField] private Image image;
        [SerializeField] private TMP_Text badgeText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Button flipButton;

        [Header("뒷면")]
        [SerializeField] private GameObject back;
        [SerializeField] private TMP_Text backNameText;
        [SerializeField] private TMP_Text backDescriptionText;
        [SerializeField] private TMP_Text historyText;
        [SerializeField] private TMP_Text categoryChipLabel;

        /// <summary>주소가 비어 있으면 제목까지 통째로 감추려고 묶어 둔 오브젝트.</summary>
        [SerializeField] private GameObject addressSection;
        [SerializeField] private TMP_Text addressText;
        [SerializeField] private Button backFlipButton;

        [Header("공통")]
        [SerializeField] private Button view360Button;

        private bool showingBack;

        public GameObject Root => root;
        public Button Dimmer => dimmer;
        public GameObject Front => front;
        public GameObject Back => back;
        public Image Image => image;
        public TMP_Text BadgeText => badgeText;
        public TMP_Text NameText => nameText;
        public TMP_Text DescriptionText => descriptionText;
        public TMP_Text HistoryText => historyText;
        public TMP_Text AddressText => addressText;
        public Button FlipButton => flipButton;
        public Button BackFlipButton => backFlipButton;
        public Button View360Button => view360Button;

        public bool IsVisible => root != null && root.activeSelf;

        /// <summary>직렬화 참조가 하나라도 비어 있으면 즉시 예외를 던져 프리팹 설정 실수를 잡는다.</summary>
        public void ValidateReferences()
        {
            Require(root, nameof(root));
            Require(dimmer, nameof(dimmer));
            Require(front, nameof(front));
            Require(image, nameof(image));
            Require(badgeText, nameof(badgeText));
            Require(nameText, nameof(nameText));
            Require(descriptionText, nameof(descriptionText));
            Require(flipButton, nameof(flipButton));
            Require(back, nameof(back));
            Require(backNameText, nameof(backNameText));
            Require(backDescriptionText, nameof(backDescriptionText));
            Require(historyText, nameof(historyText));
            Require(categoryChipLabel, nameof(categoryChipLabel));
            Require(addressSection, nameof(addressSection));
            Require(addressText, nameof(addressText));
            Require(backFlipButton, nameof(backFlipButton));
            Require(view360Button, nameof(view360Button));
        }

        /// <summary>참조가 비었으면 어느 필드인지 이름을 담아 예외를 던진다.</summary>
        private void Require(UnityEngine.Object reference, string fieldName)
        {
            if (reference == null)
            {
                throw new InvalidOperationException(
                    "CardDetail is missing the serialized reference '" + fieldName + "'.");
            }
        }

        /// <summary>딤 영역과 뒤집기 버튼을 연결하고 팝업을 닫힌 상태로 시작한다. 인스턴스마다 한 번만 부른다.</summary>
        public void Initialize(Action onClose)
        {
            dimmer.onClick.AddListener(() => onClose?.Invoke());
            flipButton.onClick.AddListener(ShowBack);
            backFlipButton.onClick.AddListener(ShowFront);
            root.SetActive(false);
        }

        /// <summary>랜드마크 정보를 채워 팝업을 앞면부터 띄운다. 항상 맨 위에 그리도록 마지막 형제로 보낸다.</summary>
        public void Show(SpotDefinition definition, Sprite sprite)
        {
            if (definition == null)
            {
                return;
            }

            badgeText.text = definition.CollectionTitle;
            nameText.text = definition.DisplayName;
            descriptionText.text = definition.Description;
            if (sprite != null)
            {
                image.sprite = sprite;
            }

            backNameText.text = definition.DisplayName;
            backDescriptionText.text = definition.Description;
            historyText.text = string.IsNullOrWhiteSpace(definition.History)
                ? definition.Description
                : definition.History;

            // 카테고리 칩은 해시태그 표기로 보여 준다. 카테고리가 없으면 칩만 감춘다.
            bool hasCategory = !string.IsNullOrWhiteSpace(definition.Category);
            categoryChipLabel.transform.parent.gameObject.SetActive(hasCategory);
            if (hasCategory)
            {
                categoryChipLabel.text = "# " + definition.Category;
            }

            // 주소는 아직 비어 있는 데이터가 많다. 빈 칸을 남기지 않고 제목까지 함께 감춘다.
            bool hasAddress = !string.IsNullOrWhiteSpace(definition.Address);
            addressSection.SetActive(hasAddress);
            if (hasAddress)
            {
                addressText.text = definition.Address;
            }

            // 360도 이미지가 없는 랜드마크에서는 버튼을 숨긴다. 비활성보다 덜 헷갈린다.
            view360Button.gameObject.SetActive(!string.IsNullOrWhiteSpace(definition.View360Image));

            showingBack = false;
            ApplySide();
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        /// <summary>상세 팝업을 닫는다.</summary>
        public void Hide()
        {
            root.SetActive(false);
        }

        /// <summary>뒷면(역사·카테고리·주소)으로 넘긴다.</summary>
        private void ShowBack()
        {
            showingBack = true;
            ApplySide();
        }

        /// <summary>앞면(사진·소개)으로 되돌린다.</summary>
        private void ShowFront()
        {
            showingBack = false;
            ApplySide();
        }

        /// <summary>현재 면만 켜 둔다.</summary>
        private void ApplySide()
        {
            front.SetActive(!showingBack);
            back.SetActive(showingBack);
        }
    }
}
