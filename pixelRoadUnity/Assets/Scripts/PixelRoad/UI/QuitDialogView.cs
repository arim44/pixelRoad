using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelRoad.UI
{
    /// <summary>
    /// 뒤로가기를 눌렀을 때 뜨는 종료 확인 창.
    /// 다른 화면 위에 겹쳐야 해서 캔버스 직속으로 두고, 열릴 때 마지막 형제로 보낸다.
    /// </summary>
    public sealed class QuitDialogView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        /// <summary>뒤 화면을 덮는 반투명 판. 창 밖 터치가 아래로 새지 않게 막는 역할도 한다.</summary>
        [SerializeField] private Image dimmer;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button confirmButton;

        public GameObject Root => root;
        public Image Dimmer => dimmer;
        public TMP_Text TitleText => titleText;
        public TMP_Text MessageText => messageText;
        public Button CancelButton => cancelButton;
        public Button ConfirmButton => confirmButton;

        public bool IsVisible => root != null && root.activeSelf;

        /// <summary>직렬화 참조가 하나라도 비어 있으면 즉시 예외를 던져 프리팹 설정 실수를 잡는다.</summary>
        public void ValidateReferences()
        {
            Require(root, nameof(root));
            Require(dimmer, nameof(dimmer));
            Require(titleText, nameof(titleText));
            Require(messageText, nameof(messageText));
            Require(cancelButton, nameof(cancelButton));
            Require(confirmButton, nameof(confirmButton));
        }

        /// <summary>참조가 비었으면 어느 필드인지 이름을 담아 예외를 던진다.</summary>
        private void Require(UnityEngine.Object reference, string fieldName)
        {
            if (reference == null)
            {
                throw new InvalidOperationException(
                    "QuitDialog is missing the serialized reference '" + fieldName + "'.");
            }
        }

        /// <summary>버튼 이벤트를 연결한다. 프리팹 인스턴스마다 한 번만 부른다.</summary>
        public void Initialize(Action onCancel, Action onConfirm)
        {
            cancelButton.onClick.AddListener(() => onCancel?.Invoke());
            confirmButton.onClick.AddListener(() => onConfirm?.Invoke());
            root.SetActive(false);
        }

        /// <summary>확인 창을 여닫는다. 열 때마다 마지막 형제로 올려 다른 화면에 가리지 않게 한다.</summary>
        public void SetVisible(bool visible)
        {
            if (visible)
            {
                root.transform.SetAsLastSibling();
            }

            root.SetActive(visible);
        }
    }
}
