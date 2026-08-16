using UnityEngine;
using UnityEngine.UI;

namespace PixelRoad.UI
{
    /// <summary>
    /// 지도 위 랜드마크 마커. 이름은 마커에 붙이지 않고 선택했을 때 상단 배너에서만 보여 준다.
    /// </summary>
    public sealed class LandmarkMarkerView : MonoBehaviour
    {
        [SerializeField] private Image icon;
        /// <summary>Button 대신 쓰는 탭 판정. 지도 팬과 겹쳐도 모바일에서 마커 선택이 살아 있게 한다.</summary>
        [SerializeField] private MapMarkerTapTarget tapTarget;

        public Image Icon => icon;
        public MapMarkerTapTarget TapTarget => tapTarget;
    }
}
