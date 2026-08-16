using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelRoad.UI
{
    /// <summary>
    /// 도감 상단의 카테고리 필터 칩 하나.
    /// 선택 상태는 라벨 색과 밑줄로만 표현한다(와이어프레임 기준).
    /// </summary>
    public sealed class CodexFilterChipView : MonoBehaviour
    {
        private static readonly Color32 SelectedColor = new Color32(246, 237, 217, 255);
        private static readonly Color32 NormalColor = new Color32(138, 132, 120, 255);

        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image underline;

        public Button Button => button;

        /// <summary>이 칩이 대표하는 category 값. `전체` 칩은 빈 문자열이다.</summary>
        public string Category { get; private set; }

        /// <summary>칩이 담당할 category와 표시 문구를 지정한다. 하이어라키에서 찾기 쉽도록 이름도 같이 바꾼다.</summary>
        public void Initialize(string category, string displayName)
        {
            Category = category ?? string.Empty;
            label.text = displayName;
            gameObject.name = "Chip_" + (string.IsNullOrEmpty(Category) ? "All" : Category);
        }

        /// <summary>선택 상태를 라벨 색과 밑줄로 표시한다.</summary>
        public void SetSelected(bool selected)
        {
            label.color = selected ? SelectedColor : NormalColor;
            underline.enabled = selected;
        }
    }
}
