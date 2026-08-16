using PixelRoad.Data;
using UnityEngine;

namespace PixelRoad.Geo
{
    /// <summary>
    /// 위경도를 화면 좌표로 옮기거나 두 지점의 거리를 재는 계산 모음.
    /// 맵 이미지가 웹 메르카토르 기준이라 같은 방식으로 투영해야 마커가 어긋나지 않는다.
    /// </summary>
    public static class GeoProjection
    {
        private const double EarthRadiusMeters = 6371000.0;
        private const double MinMercatorLatitude = -85.05112878;
        private const double MaxMercatorLatitude = 85.05112878;

        /// <summary>
        /// 좌표를 맵 영역 안의 0~1 비율로 바꾼다. y는 위쪽이 0이라 이미지 좌표와 방향이 같다.
        /// </summary>
        public static Vector2 LatLonToNormalizedWebMercator(double latitude, double longitude, MapBounds bounds)
        {
            double westX = LongitudeToMercatorX(bounds.westLon);
            double eastX = LongitudeToMercatorX(bounds.eastLon);
            double northY = LatitudeToMercatorY(bounds.northLat);
            double southY = LatitudeToMercatorY(bounds.southLat);
            double pointX = LongitudeToMercatorX(longitude);
            double pointY = LatitudeToMercatorY(latitude);

            float normalizedX = Mathf.InverseLerp((float)westX, (float)eastX, (float)pointX);
            float normalizedY = Mathf.InverseLerp((float)northY, (float)southY, (float)pointY);
            return new Vector2(normalizedX, normalizedY);
        }

        /// <summary>두 좌표 사이의 실제 거리(m). 방문 판정에 쓰는 하버사인 계산이다.</summary>
        public static double DistanceMeters(double latA, double lonA, double latB, double lonB)
        {
            double dLat = DegreesToRadians(latB - latA);
            double dLon = DegreesToRadians(lonB - lonA);
            double a = System.Math.Sin(dLat / 2.0) * System.Math.Sin(dLat / 2.0) +
                       System.Math.Cos(DegreesToRadians(latA)) *
                       System.Math.Cos(DegreesToRadians(latB)) *
                       System.Math.Sin(dLon / 2.0) *
                       System.Math.Sin(dLon / 2.0);
            double c = 2.0 * System.Math.Atan2(System.Math.Sqrt(a), System.Math.Sqrt(1.0 - a));
            return EarthRadiusMeters * c;
        }

        /// <summary>A 지점에서 B 지점을 바라보는 초기 방위각(진북 기준 0~360도, 시계 방향).</summary>
        public static double BearingDegrees(double latA, double lonA, double latB, double lonB)
        {
            double lat1 = DegreesToRadians(latA);
            double lat2 = DegreesToRadians(latB);
            double dLon = DegreesToRadians(lonB - lonA);

            double y = System.Math.Sin(dLon) * System.Math.Cos(lat2);
            double x = System.Math.Cos(lat1) * System.Math.Sin(lat2) -
                       System.Math.Sin(lat1) * System.Math.Cos(lat2) * System.Math.Cos(dLon);

            double bearingDegrees = RadiansToDegrees(System.Math.Atan2(y, x));
            return (bearingDegrees + 360.0) % 360.0;
        }

        /// <summary>경도의 메르카토르 x. 경도는 선형이라 라디안 값을 그대로 쓴다.</summary>
        private static double LongitudeToMercatorX(double longitude)
        {
            return DegreesToRadians(longitude);
        }

        /// <summary>위도의 메르카토르 y. 극지방에서 값이 발산하므로 먼저 범위를 자른다.</summary>
        private static double LatitudeToMercatorY(double latitude)
        {
            double clamped = System.Math.Max(MinMercatorLatitude, System.Math.Min(MaxMercatorLatitude, latitude));
            double radians = DegreesToRadians(clamped);
            return System.Math.Log(System.Math.Tan(System.Math.PI / 4.0 + radians / 2.0));
        }

        /// <summary>도 단위를 라디안으로 바꾼다.</summary>
        private static double DegreesToRadians(double degrees)
        {
            return degrees * System.Math.PI / 180.0;
        }

        private static double RadiansToDegrees(double radians)
        {
            return radians * 180.0 / System.Math.PI;
        }
    }
}
