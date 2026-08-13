using UnityEngine;
using UnityEngine.UI;

namespace PixelRoad.UI
{
    public sealed class LandmarkMarkerView : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private MapMarkerTapTarget tapTarget;

        public Image Icon => icon;
        public MapMarkerTapTarget TapTarget => tapTarget;
    }
}
