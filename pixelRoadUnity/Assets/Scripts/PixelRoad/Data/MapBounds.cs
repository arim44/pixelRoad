using System;

namespace PixelRoad.Data
{
    [Serializable]
    public sealed class MapBounds
    {
        public double northLat;
        public double southLat;
        public double westLon;
        public double eastLon;

        public bool IsValid()
        {
            return northLat > southLat && eastLon > westLon;
        }
    }
}
