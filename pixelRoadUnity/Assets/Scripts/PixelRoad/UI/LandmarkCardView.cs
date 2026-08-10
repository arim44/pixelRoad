using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelRoad.UI
{
    public sealed class LandmarkCardView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text categoryText;
        [SerializeField] private TMP_Text descriptionText;

        public Button Button => button;
        public Image Icon => icon;
        public TMP_Text NameText => nameText;
        public TMP_Text CategoryText => categoryText;
        public TMP_Text DescriptionText => descriptionText;
    }
}
