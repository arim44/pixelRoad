namespace PixelRoad.AR
{
    /// <summary>MapScene에서 ARScene으로 넘기는 랜드마크 정보의 경량 스냅샷.</summary>
    public readonly struct ARLandmarkSnapshot
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string IconKey;
        public readonly string Category;
        public readonly double Latitude;
        public readonly double Longitude;
        public readonly float RadiusMeters;
        public readonly bool IsUnlocked;

        public ARLandmarkSnapshot(
            string id,
            string displayName,
            string iconKey,
            string category,
            double latitude,
            double longitude,
            float radiusMeters,
            bool isUnlocked)
        {
            Id = id;
            DisplayName = displayName;
            IconKey = iconKey;
            Category = category;
            Latitude = latitude;
            Longitude = longitude;
            RadiusMeters = radiusMeters;
            IsUnlocked = isUnlocked;
        }
    }
}
