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
        /// <summary>잠긴 랜드마크의 설명 자리. 도감 그리드 카드와 같은 표기를 쓴다.</summary>
        private const string LockedDescription = "???";

        /// <summary>잠긴 랜드마크 이미지에 씌우는 틴트. 도감 카드와 같은 값이다.</summary>
        private static readonly Color32 LockedImageTint = new Color32(124, 120, 112, 235);

        [SerializeField] private GameObject root;
        [SerializeField] private Button dimmer;

        [Header("앞면")]
        [SerializeField] private GameObject front;
        [SerializeField] private Image image;
        [SerializeField] private TMP_Text badgeText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Button flipButton;

        /// <summary>`→ 지도에서 보기`. 이 랜드마크로 지도를 옮기고 카드를 닫는다.</summary>
        [SerializeField] private Button mapButton;

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

        /// <summary>지금 띄워 둔 랜드마크. `지도에서 보기`가 어디로 가야 하는지 여기서 읽는다.</summary>
        private SpotDefinition currentDefinition;

        /// <summary>앞면의 `지도에서 보기`를 눌렀을 때. 실제 지도 이동은 구독자가 맡는다.</summary>
        public event Action<SpotDefinition> MapRequested;

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
        public Button MapButton => mapButton;
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
            Require(mapButton, nameof(mapButton));
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
            mapButton.onClick.AddListener(RequestMap);
            root.SetActive(false);
        }

        /// <summary>해금된 랜드마크의 상세를 띄운다.</summary>
        public void Show(SpotDefinition definition, Sprite sprite)
        {
            Show(definition, sprite, true);
        }

        /// <summary>
        /// 랜드마크 정보를 채워 팝업을 앞면부터 띄운다. 항상 맨 위에 그리도록 마지막 형제로 보낸다.
        ///
        /// 잠긴 랜드마크도 띄운다. 어디로 가야 해금되는지 알려 주려면 `지도에서 보기`를 눌릴 수 있어야 하기 때문이다.
        /// 대신 내용은 도감 그리드 카드와 같은 수준으로 가린다 — 이름만 남기고 설명은 ???,
        /// 이미지는 대체 이미지에 틴트, 뒷면과 360도 버튼은 감춘다.
        /// </summary>
        public void Show(SpotDefinition definition, Sprite sprite, bool unlocked)
        {
            if (definition == null)
            {
                return;
            }

            currentDefinition = definition;
            badgeText.text = definition.CollectionTitle;
            nameText.text = definition.DisplayName;
            descriptionText.text = unlocked ? definition.Description : LockedDescription;
            if (sprite != null)
            {
                image.sprite = sprite;
            }

            image.color = unlocked ? Color.white : (Color)LockedImageTint;

            // 뒷면(역사·카테고리·주소)은 해금한 랜드마크에서만 볼 수 있다.
            flipButton.gameObject.SetActive(unlocked);

            backNameText.text = definition.DisplayName;
            backDescriptionText.text = definition.Description;
            historyText.text = string.IsNullOrWhiteSpace(definition.History)
                ? definition.Description
                : definition.History;

            // 카테고리 칩은 해시태그 표기로 보여 준다. 카테고리가 없으면 칩만 감춘다.
            // 데이터에 남은 영문 표기(station 등)는 표시용 이름으로 바꿔 쓴다.
            string category = SpotCategory.Normalize(definition.Category);
            bool hasCategory = !string.IsNullOrWhiteSpace(category);
            categoryChipLabel.transform.parent.gameObject.SetActive(hasCategory);
            if (hasCategory)
            {
                categoryChipLabel.text = "# " + category;
            }

            // 주소는 아직 비어 있는 데이터가 많다. 빈 칸을 남기지 않고 제목까지 함께 감춘다.
            bool hasAddress = !string.IsNullOrWhiteSpace(definition.Address);
            addressSection.SetActive(hasAddress);
            if (hasAddress)
            {
                addressText.text = definition.Address;
            }

            // 360도 이미지가 없거나 아직 잠긴 랜드마크에서는 버튼을 숨긴다. 비활성보다 덜 헷갈린다.
            view360Button.gameObject.SetActive(
                unlocked && !string.IsNullOrWhiteSpace(definition.View360Image));

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

        /// <summary>앞면의 `지도에서 보기`를 눌렀을 때. 어떤 랜드마크인지 함께 알린다.</summary>
        private void RequestMap()
        {
            if (currentDefinition == null)
            {
                return;
            }

            MapRequested?.Invoke(currentDefinition);
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
