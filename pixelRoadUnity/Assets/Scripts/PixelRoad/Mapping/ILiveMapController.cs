using System;
using PixelRoad.Data;
using UnityEngine;
using UnityEngine.UI;

namespace PixelRoad.Mapping
{
    /// <summary>
    /// 실시간 지도 뷰를 다루는 창구. UI 쪽은 이 인터페이스만 알면 되고,
    /// 타일 적재나 렌더링 방식이 바뀌어도 영향을 받지 않는다.
    /// </summary>
    public interface ILiveMapController
    {
        event Action ViewChanged;
        event Action FirstTileReady;

        bool IsInitialized { get; }
        bool HasRenderedTile { get; }
        string LastError { get; }

        /// <summary>
        /// 지도를 뷰포트와 출력 이미지에 연결하고 시작 위치를 잡는다. 준비에 실패하면 false를 준다.
        /// </summary>
        bool Initialize(
            MapConfig mapConfig,
            RectTransform mapViewport,
            RawImage mapOutput,
            double startLatitude,
            double startLongitude);

        /// <summary>
        /// 화면 드래그량만큼 지도를 이동한다.
        /// </summary>
        void Pan(Vector2 screenDelta);
        /// <summary>
        /// 지정한 화면 지점을 기준으로 확대·축소한다.
        /// </summary>
        void ZoomAt(float factor, Vector2 screenPosition);
        /// <summary>
        /// 지도 중심을 지정한 위경도로 옮긴다.
        /// </summary>
        void SetCenter(double latitude, double longitude);
        /// <summary>
        /// 위경도를 뷰포트 안의 로컬 좌표로 바꾼다. 랜드마크 마커를 배치할 때 쓴다.
        /// </summary>
        Vector2 LatLonToViewportLocal(double latitude, double longitude);
        /// <summary>
        /// 해당 지점이 현재 화면에 보이는지 판단한다. padding으로 경계를 넉넉히 잡을 수 있다.
        /// </summary>
        bool IsInViewport(double latitude, double longitude, float padding = 0f);
    }
}
