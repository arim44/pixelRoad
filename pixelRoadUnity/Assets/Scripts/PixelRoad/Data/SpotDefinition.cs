namespace PixelRoad.Data
{
    public sealed class SpotDefinition
    {
        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public string Description { get; private set; }
        public string Category { get; private set; }
        public double Latitude { get; private set; }
        public double Longitude { get; private set; }
        public float RadiusMeters { get; private set; }
        public string IconKey { get; private set; }
        public bool InitiallyUnlocked { get; private set; }

        public SpotDefinition(
            string id,
            string displayName,
            string description,
            string category,
            double latitude,
            double longitude,
            float radiusMeters,
            string iconKey,
            bool initiallyUnlocked)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            Category = category;
            Latitude = latitude;
            Longitude = longitude;
            RadiusMeters = radiusMeters;
            IconKey = iconKey;
            InitiallyUnlocked = initiallyUnlocked;
        }
    }
}
