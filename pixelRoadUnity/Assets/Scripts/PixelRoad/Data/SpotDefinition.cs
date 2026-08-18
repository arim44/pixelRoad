using System;
using System.Globalization;

namespace PixelRoad.Data
{
    /// <summary>
    /// 랜드마크 한 곳의 불변 정보. 위치와 표시용 텍스트를 한데 묶어
    /// 런타임 상태(<see cref="SpotRuntimeState"/>)와 분리해 둔다.
    /// </summary>
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
        /// <summary>도감 이미지를 찾을 때 쓰는 키(landmarks.json의 thumbnail).</summary>
        public string ThumbnailKey { get; private set; }

        /// <summary>
        /// 지도·AR 마커 아이콘을 찾을 때 쓰는 키. 데이터를 읽는 시점에 <see cref="Category"/>로
        /// 한 번 해석해 두므로(그 이름의 PNG가 있으면 category, 없으면 기본 아이콘 이름)
        /// 이후에는 그대로 읽기만 하면 되고 지도와 AR이 같은 그림을 쓴다.
        /// </summary>
        public string IconKey { get; private set; }
        public string History { get; private set; }
        public string[] Tags { get; private set; }
        public string View360Image { get; private set; }
        public bool InitiallyUnlocked { get; private set; }

        /// <summary>
        /// landmarks.json 레코드로부터 만든다. 빈 문자열 처리를 여기서 끝내
        /// 이후 UI 코드가 null을 신경 쓰지 않게 한다.
        /// </summary>
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
            string view360Image,
            string iconKey)
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
            ThumbnailKey = thumbnail ?? string.Empty;
            History = history ?? string.Empty;
            Tags = tags ?? Array.Empty<string>();
            View360Image = view360Image;
            IconKey = string.IsNullOrWhiteSpace(iconKey) ? Category : iconKey.Trim();
            InitiallyUnlocked = false;
        }

        // 기존 테스트/도구 코드가 사용하던 생성자를 유지한다. 실제 앱 데이터는
        // landmarks.json의 숫자 id를 받는 위 생성자를 사용한다.
        /// <summary>문자열 id로 만드는 호환용 생성자. 해금 여부를 직접 지정할 수 있다.</summary>
        public SpotDefinition(
            string id,
            string displayName,
            string description,
            string category,
            double latitude,
            double longitude,
            float radiusMeters,
            string thumbnail,
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
            ThumbnailKey = thumbnail ?? string.Empty;
            History = string.Empty;
            Tags = Array.Empty<string>();
            View360Image = null;
            IconKey = Category;
            InitiallyUnlocked = initiallyUnlocked;
        }
    }
}
