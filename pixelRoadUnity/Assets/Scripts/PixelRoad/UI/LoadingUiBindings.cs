using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelRoad.UI
{
    /// <summary>
    /// LoadingUIRoot.prefab의 고정 UI 참조 모음.
    /// 계층 이름을 바꿔도 런타임 코드가 깨지지 않도록 모든 참조를 직렬화한다.
    /// </summary>
    public sealed class LoadingUiBindings : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        /// <summary>로딩이 끝난 뒤 알파를 낮춰 화면을 걷어낼 때 쓴다.</summary>
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text logoText;
        [SerializeField] private Image progressFill;
        [SerializeField] private TMP_Text percentText;

        public Canvas Canvas => canvas;
        public CanvasGroup CanvasGroup => canvasGroup;
        public TMP_Text LogoText => logoText;
        public Image ProgressFill => progressFill;
        public TMP_Text PercentText => percentText;

        /// <summary>직렬화 참조가 하나라도 비어 있으면 즉시 예외를 던져 프리팹 설정 실수를 잡는다.</summary>
        public void ValidateReferences()
        {
            Require(canvas, nameof(canvas));
            Require(canvasGroup, nameof(canvasGroup));
            Require(logoText, nameof(logoText));
            Require(progressFill, nameof(progressFill));
            Require(percentText, nameof(percentText));
        }

        /// <summary>참조가 비었으면 어느 필드인지 이름을 담아 예외를 던진다.</summary>
        private void Require(UnityEngine.Object reference, string fieldName)
        {
            if (reference == null)
            {
                throw new InvalidOperationException(
                    "LoadingUIRoot prefab is missing the serialized reference '" + fieldName + "'.");
            }
        }
    }
}
