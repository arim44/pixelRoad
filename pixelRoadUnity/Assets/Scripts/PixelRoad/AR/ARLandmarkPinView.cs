using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelRoad.AR
{
    /// <summary>
    /// AR 화면의 랜드마크 핀 하나(아이콘 + 거리 라벨) 프리팹의 직렬화 참조.
    /// MapScene의 LandmarkMarkerView와 같은 패턴이다.
    ///
    /// 화면 안에 있을 때나 화면 밖(가장자리)일 때나 같은 랜드마크 아이콘을 Icon에 넣어 재사용한다.
    /// 크기만 화면 밖에서 더 작아진다(AROverlayView.ShowOnScreen/ShowAtEdge 참고).
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
