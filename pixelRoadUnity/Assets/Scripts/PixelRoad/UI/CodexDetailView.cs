using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelRoad.UI
{
    /// <summary>
    /// 도감 카드를 눌렀을 때 도감 위에 겹쳐 뜨는 상세 보기.
    /// 뒷면(역사 설명)과 360도 보기는 아직 자료가 없어 버튼만 준비해 둔다.
    /// </summary>
    public sealed class CodexDetailView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Button dimmer;
        [SerializeField] private Image image;
        [SerializeField] private TMP_Text badgeText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Button flipButton;
        [SerializeField] private TMP_Text flipLabel;
        [SerializeField] private Button view360Button;

        private bool showingBack;

        /// <summary>앞면(shortDescription)과 뒷면(history) 문구를 각각 들고 있다가 뒤집을 때 쓴다.</summary>
        private string frontText = string.Empty;
        private string backText = string.Empty;

        public GameObject Root => root;
        public Button Dimmer => dimmer;
        public Image Image => image;
        public TMP_Text BadgeText => badgeText;
        public TMP_Text NameText => nameText;
        public TMP_Text DescriptionText => descriptionText;
        public Button FlipButton => flipButton;
        public Button View360Button => view360Button;

        public bool IsVisible => root != null && root.activeSelf;

        /// <summary>직렬화 참조가 하나라도 비어 있으면 즉시 예외를 던져 프리팹 설정 실수를 잡는다.</summary>
        public void ValidateReferences()
        {
            Require(root, nameof(root));
            Require(dimmer, nameof(dimmer));
            Require(image, nameof(image));
            Require(badgeText, nameof(badgeText));
            Require(nameText, nameof(nameText));
            Require(descriptionText, nameof(descriptionText));
            Require(flipButton, nameof(flipButton));
            Require(flipLabel, nameof(flipLabel));
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
            flipButton.onClick.AddListener(ToggleSide);
            root.SetActive(false);
        }

        /// <summary>랜드마크 정보를 채워 팝업을 앞면부터 띄운다. 항상 맨 위에 그리도록 마지막 형제로 보낸다.</summary>
        public void Show(string badge, string displayName, string front, string back, Sprite sprite, bool has360)
        {
            badgeText.text = badge;
            nameText.text = displayName;
            frontText = front ?? string.Empty;
            backText = string.IsNullOrWhiteSpace(back) ? front : back;
            if (sprite != null)
            {
                image.sprite = sprite;
            }

            // 360도 이미지가 없는 랜드마크에서는 버튼을 숨긴다. 비활성보다 덜 헷갈린다.
            view360Button.gameObject.SetActive(has360);

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

        /// <summary>앞면과 뒷면을 번갈아 보여 준다.</summary>
        private void ToggleSide()
        {
            showingBack = !showingBack;
            ApplySide();
        }

        /// <summary>현재 면에 맞는 설명과 버튼 문구를 반영한다.</summary>
        private void ApplySide()
        {
            descriptionText.text = showingBack ? backText : frontText;
            flipLabel.text = showingBack ? "앞면 보기 >" : "뒷면 보기 >";
        }
    }
}
