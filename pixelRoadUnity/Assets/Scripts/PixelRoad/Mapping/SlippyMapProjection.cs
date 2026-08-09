using System;

namespace PixelRoad.Mapping
{
    public readonly struct WorldMercatorPoint
    {
        public readonly double X;
        public readonly double Y;

        public WorldMercatorPoint(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    public static class SlippyMapProjection
    {
        public const double MaxLatitude = 85.05112878;

        public static WorldMercatorPoint LatLonToWorld(double latitude, double longitude)
        {
            double clampedLatitude = Math.Max(-MaxLatitude, Math.Min(MaxLatitude, latitude));
            double latitudeRadians = clampedLatitude * Math.PI / 180.0;
            double x = WrapWorldX((longitude + 180.0) / 360.0);
            double y = 0.5 - Math.Log((1.0 + Math.Sin(latitudeRadians)) / (1.0 - Math.Sin(latitudeRadians))) / (4.0 * Math.PI);
            return new WorldMercatorPoint(x, ClampWorldY(y));
        }

        public static void WorldToLatLon(WorldMercatorPoint world, out double latitude, out double longitude)
        {
            double x = WrapWorldX(world.X);
            double y = ClampWorldY(world.Y);
            longitude = x * 360.0 - 180.0;
            double mercator = Math.PI * (1.0 - 2.0 * y);
            latitude = Math.Atan(Math.Sinh(mercator)) * 180.0 / Math.PI;
        }

        public static double WrapWorldX(double value)
        {
            value -= Math.Floor(value);
            return value < 0.0 ? value + 1.0 : value;
        }

        public static double ClampWorldY(double value)
        {
            return Math.Max(0.0, Math.Min(1.0, value));
        }

        public static double ShortestWrappedDeltaX(double from, double to)
        {
            double delta = to - from;
            if (delta > 0.5)
            {
                delta -= 1.0;
            }
            else if (delta < -0.5)
            {
                delta += 1.0;
            }

            return delta;
        }
    }
}
