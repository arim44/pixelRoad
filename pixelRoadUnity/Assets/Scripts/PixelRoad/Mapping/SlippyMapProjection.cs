using System;

namespace PixelRoad.Mapping
{
    /// <summary>
    /// 웹 메르카토르 정규 좌표 한 점. X와 Y 모두 0~1 범위이며 Y는 아래로 증가한다.
    /// </summary>
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

    /// <summary>
    /// 위경도와 슬리피 맵 타일 좌표계를 오가는 변환을 모아 둔다.
    /// 표준 타일 서버와 같은 웹 메르카토르 규칙을 따른다.
    /// </summary>
    public static class SlippyMapProjection
    {
        public const double MaxLatitude = 85.05112878;

        /// <summary>
        /// 위경도를 0~1 월드 좌표로 옮긴다. 극지방은 투영이 발산하므로 위도를 잘라낸다.
        /// </summary>
        public static WorldMercatorPoint LatLonToWorld(double latitude, double longitude)
        {
            double clampedLatitude = Math.Max(-MaxLatitude, Math.Min(MaxLatitude, latitude));
            double latitudeRadians = clampedLatitude * Math.PI / 180.0;
            double x = WrapWorldX((longitude + 180.0) / 360.0);
            double y = 0.5 - Math.Log((1.0 + Math.Sin(latitudeRadians)) / (1.0 - Math.Sin(latitudeRadians))) / (4.0 * Math.PI);
            return new WorldMercatorPoint(x, ClampWorldY(y));
        }

        /// <summary>
        /// 월드 좌표를 다시 위경도로 되돌린다.
        /// </summary>
        public static void WorldToLatLon(WorldMercatorPoint world, out double latitude, out double longitude)
        {
            double x = WrapWorldX(world.X);
            double y = ClampWorldY(world.Y);
            longitude = x * 360.0 - 180.0;
            double mercator = Math.PI * (1.0 - 2.0 * y);
            latitude = Math.Atan(Math.Sinh(mercator)) * 180.0 / Math.PI;
        }

        /// <summary>
        /// 경도 방향은 지구를 한 바퀴 돌므로 값을 0~1 안으로 감아 넣는다.
        /// </summary>
        public static double WrapWorldX(double value)
        {
            value -= Math.Floor(value);
            return value < 0.0 ? value + 1.0 : value;
        }

        /// <summary>
        /// 위도 방향은 감기지 않으므로 0~1 밖으로 나가지 않게 자른다.
        /// </summary>
        public static double ClampWorldY(double value)
        {
            return Math.Max(0.0, Math.Min(1.0, value));
        }

        /// <summary>
        /// 날짜변경선을 가로지르는 쪽이 더 가까울 수 있으므로 두 X 사이의 최단 이동량을 구한다.
        /// </summary>
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
