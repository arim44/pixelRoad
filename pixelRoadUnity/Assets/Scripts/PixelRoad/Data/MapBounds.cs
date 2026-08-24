using System;

namespace PixelRoad.Data
{
    /// <summary>
    /// 맵 이미지가 덮는 위경도 사각 영역. 좌표를 화면 위치로 투영할 때 기준이 된다.
    /// </summary>
    [Serializable]
    public sealed class MapBounds
    {
        public double northLat;
        public double southLat;
        public double westLon;
        public double eastLon;

        /// <summary>남북/동서가 뒤집히지 않았는지 확인한다. 투영 전에 검사한다.</summary>
        public bool IsValid()
        {
            return northLat > southLat && eastLon > westLon;
        }
    }
}
