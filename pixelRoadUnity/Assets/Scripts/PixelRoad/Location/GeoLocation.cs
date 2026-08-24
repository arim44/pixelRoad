namespace PixelRoad.Location
{
    /// <summary>
    /// 위치 제공자가 넘겨주는 한 번의 측위 결과. 정확도와 유효 여부까지 함께 담아
    /// 값을 쓰기 전에 신뢰할 수 있는지 판단하게 한다.
    /// </summary>
    public readonly struct GeoLocation
    {
        public readonly double Latitude;
        public readonly double Longitude;
        public readonly float HorizontalAccuracyMeters;
        public readonly bool IsValid;

        /// <summary>측위 결과 한 건을 만든다.</summary>
        public GeoLocation(double latitude, double longitude, float horizontalAccuracyMeters, bool isValid)
        {
            Latitude = latitude;
            Longitude = longitude;
            HorizontalAccuracyMeters = horizontalAccuracyMeters;
            IsValid = isValid;
        }
    }
}
