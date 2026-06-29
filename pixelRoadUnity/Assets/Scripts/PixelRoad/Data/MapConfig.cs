using System;

namespace PixelRoad.Data
{
    [Serializable]
    public sealed class MapConfig
    {
        public string appTitle = "Pixel Road";
        public string mapImageResourcePath = "PixelRoad/Maps/gyeongbokgung_demo";
        public string spotsCsvResourcePath = "PixelRoad/spots";
        public string projection = "WebMercator";
        public MapBounds bounds = new MapBounds();
        public float defaultUnlockRadiusMeters = 50f;
        public bool enablePixelFilter = true;
        public int pixelBlockSize = 4;
        public bool enableBackgroundUnlock = false;
        public int maxActiveGeofences = 100;
        public float desiredAccuracyMeters = 15f;
        public float locationUpdateDistanceMeters = 3f;
        public double editorStartLatitude = 37.579617;
        public double editorStartLongitude = 126.977041;
        public float editorMoveSpeedMetersPerSecond = 250f;
        public float editorFastMoveMultiplier = 4f;
        public bool editorFollowSimulatedLocation = true;
    }
}
