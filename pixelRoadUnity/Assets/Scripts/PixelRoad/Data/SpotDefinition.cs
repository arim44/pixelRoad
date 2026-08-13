using System;
using System.Globalization;

namespace PixelRoad.Data
{
    public sealed class SpotDefinition
    {
        public int LandmarkId { get; private set; }
        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public string Description { get; private set; }
        public string Category { get; private set; }
        public string CollectionTitle { get; private set; }
        public string Address { get; private set; }
        public double Latitude { get; private set; }
        public double Longitude { get; private set; }
        public float RadiusMeters { get; private set; }
        public string IconKey { get; private set; }
        public string History { get; private set; }
        public string[] Tags { get; private set; }
        public string View360Image { get; private set; }
        public bool InitiallyUnlocked { get; private set; }

        public SpotDefinition(
            int landmarkId,
            string displayName,
            string category,
            string collectionTitle,
            string address,
            double latitude,
            double longitude,
            float radiusMeters,
            string thumbnail,
            string shortDescription,
            string history,
            string[] tags,
            string view360Image)
        {
            LandmarkId = landmarkId;
            Id = landmarkId.ToString(CultureInfo.InvariantCulture);
            DisplayName = displayName ?? string.Empty;
            Description = shortDescription ?? string.Empty;
            Category = category ?? string.Empty;
            CollectionTitle = collectionTitle ?? string.Empty;
            Address = address ?? string.Empty;
            Latitude = latitude;
            Longitude = longitude;
            RadiusMeters = radiusMeters;
            IconKey = thumbnail ?? string.Empty;
            History = history ?? string.Empty;
            Tags = tags ?? Array.Empty<string>();
            View360Image = view360Image;
            InitiallyUnlocked = false;
        }

        // 기존 테스트/도구 코드가 사용하던 생성자를 유지한다. 실제 앱 데이터는
        // landmarks.json의 숫자 id를 받는 위 생성자를 사용한다.
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
            int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out int landmarkId);
            LandmarkId = landmarkId;
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            Category = category ?? string.Empty;
            CollectionTitle = string.Empty;
            Address = string.Empty;
            Latitude = latitude;
            Longitude = longitude;
            RadiusMeters = radiusMeters;
            IconKey = iconKey ?? string.Empty;
            History = string.Empty;
            Tags = Array.Empty<string>();
            View360Image = null;
            InitiallyUnlocked = initiallyUnlocked;
        }
    }
}
