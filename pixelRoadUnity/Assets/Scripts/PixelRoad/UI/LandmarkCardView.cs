using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelRoad.UI
{
    /// <summary>
    /// 도감 그리드에 깔리는 랜드마크 카드 한 장의 참조 모음.
    /// 채우는 값은 CodexView가 정하고 여기서는 위젯만 들고 있는다.
    /// </summary>
    public sealed class LandmarkCardView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private Image imageFrame;
        [SerializeField] private TMP_Text badgeText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Image lockIcon;

        public Button Button => button;

        /// <summary>랜드마크 이미지 자리. 썸네일이 없으면 프리팹의 기본 스프라이트가 남는다.</summary>
        public Image Icon => icon;

        /// <summary>Icon을 감싸는 테두리. 잠금 여부에 따라 색이 바뀐다.</summary>
        public Image ImageFrame => imageFrame;

        /// <summary>카드 좌상단의 도감 묶음(collectionTitle) 태그.</summary>
        public TMP_Text BadgeText => badgeText;

        public TMP_Text NameText => nameText;
        public TMP_Text DescriptionText => descriptionText;

        /// <summary>잠김 상태에서만 보이는 자물쇠 표시.</summary>
        public Image LockIcon => lockIcon;
    }
}
