namespace PixelRoad.Location
{
    public readonly struct GeoLocationSample
    {
        public readonly double Latitude;
        public readonly double Longitude;
        public readonly float HorizontalAccuracyMeters;
        public readonly bool IsValid;

        public GeoLocationSample(double latitude, double longitude, float horizontalAccuracyMeters, bool isValid)
        {
            Latitude = latitude;
            Longitude = longitude;
            HorizontalAccuracyMeters = horizontalAccuracyMeters;
            IsValid = isValid;
        }
    }
}
