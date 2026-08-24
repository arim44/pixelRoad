using System;
using System.Collections.Generic;
using PixelRoad.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelRoad.UI
{
    /// <summary>
    /// 랜드마크를 처음 해금했을 때 뜨는 `랜드마크 발견!` 창.
    ///
    /// 지도·도감·리포트 어느 화면에서 해금되든 보여야 해서 <see cref="QuitDialogView"/>와 같은 규칙을 따른다.
    /// 캔버스 직속에 두고, 열 때마다 마지막 형제로 올려 도감 패널이나 카드 상세 위에 겹치게 한다.
    ///
    /// 한 프레임에 여러 곳이 동시에 해금될 수 있어서 이름을 큐에 쌓고 `확인`을 누를 때마다 하나씩 보여 준다.
    /// </summary>
    public sealed class UnlockDialogView : MonoBehaviour
    {
        [SerializeField] private GameObject root;

        /// <summary>뒤 화면을 덮는 반투명 판. 창 밖 터치가 아래로 새지 않게 막는 역할도 한다.</summary>
        [SerializeField] private Image dimmer;

        /// <summary>고정 문구 `랜드마크 발견!`. 프리팹 값을 그대로 쓰므로 런타임이 건드리지 않는다.</summary>
        [SerializeField] private TMP_Text titleText;

        /// <summary>해금된 랜드마크 이름이 들어가는 자리.</summary>
        [SerializeField] private TMP_Text landmarkNameText;
        [SerializeField] private Button confirmButton;

        /// <summary>아직 보여 주지 않은 해금 이름. 연속 해금 때만 두 개 이상 쌓인다.</summary>
        private readonly Queue<string> pending = new Queue<string>();

        public GameObject Root => root;
        public Image Dimmer => dimmer;
        public TMP_Text TitleText => titleText;
        public TMP_Text LandmarkNameText => landmarkNameText;
        public Button ConfirmButton => confirmButton;

        public bool IsVisible => root != null && root.activeSelf;

        /// <summary>직렬화 참조가 하나라도 비어 있으면 즉시 예외를 던져 프리팹 설정 실수를 잡는다.</summary>
        public void ValidateReferences()
        {
            Require(root, nameof(root));
            Require(dimmer, nameof(dimmer));
            Require(titleText, nameof(titleText));
            Require(landmarkNameText, nameof(landmarkNameText));
            Require(confirmButton, nameof(confirmButton));
        }

        /// <summary>참조가 비었으면 어느 필드인지 이름을 담아 예외를 던진다.</summary>
        private void Require(UnityEngine.Object reference, string fieldName)
        {
            if (reference == null)
            {
                throw new InvalidOperationException(
                    "UnlockDialog is missing the serialized reference '" + fieldName + "'.");
            }
        }

        /// <summary>확인 버튼을 연결한다. 프리팹 인스턴스마다 한 번만 부른다.</summary>
        public void Initialize()
        {
            confirmButton.onClick.AddListener(Dismiss);
            root.SetActive(false);
        }

        /// <summary>
        /// 해금 알림을 예약한다. 이미 창이 떠 있으면 큐에 쌓고, 아니면 바로 띄운다.
        /// 같은 이름이 연달아 들어오면(같은 프레임 중복 호출) 한 번만 남긴다.
        /// </summary>
        public void Enqueue(SpotDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            string displayName = definition.DisplayName;
            if (IsVisible && landmarkNameText.text == FormatName(displayName))
            {
                return;
            }

            pending.Enqueue(displayName);
            if (!IsVisible)
            {
                ShowNext();
            }
        }

        /// <summary>`확인`을 눌렀을 때. 남은 알림이 있으면 이어서 보여 주고, 없으면 창을 닫는다.</summary>
        public void Dismiss()
        {
            if (pending.Count > 0)
            {
                ShowNext();
                return;
            }

            root.SetActive(false);
        }

        /// <summary>큐에 쌓인 알림이나 떠 있는 창을 모두 정리한다.</summary>
        public void Clear()
        {
            pending.Clear();
            root.SetActive(false);
        }

        /// <summary>큐에서 하나 꺼내 이름을 채우고 맨 위로 올린다.</summary>
        private void ShowNext()
        {
            if (pending.Count == 0)
            {
                root.SetActive(false);
                return;
            }

            landmarkNameText.text = FormatName(pending.Dequeue());
            root.transform.SetAsLastSibling();
            root.SetActive(true);
        }

        /// <summary>와이어프레임 표기는 대괄호로 감싼 이름이다.</summary>
        private static string FormatName(string displayName)
        {
            return "[" + displayName + "]";
        }
    }
}
