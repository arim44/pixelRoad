using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelRoad.AR
{
    /// <summary>
    /// AR 화면의 랜드마크 핀 하나(아이콘 + 거리 라벨) 프리팹의 직렬화 참조.
    /// MapScene의 LandmarkMarkerView와 같은 패턴이다.
    ///
    /// 화면 안에 있을 때는 실제 랜드마크 아이콘을, 화면 밖일 때는 방향 화살표를 같은 Icon에
    /// 번갈아 넣어 재사용한다(AROverlayView.ShowOnScreen/ShowAtEdge 참고).
    /// </summary>
    public sealed class ARLandmarkPinView : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text distanceLabel;
        [SerializeField] private Button button;

        public Image Icon => icon;
        public TMP_Text DistanceLabel => distanceLabel;
        public Button Button => button;
    }
}
