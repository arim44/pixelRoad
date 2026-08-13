using System;
using System.Collections.Generic;
using PixelRoad.Location;

namespace PixelRoad.AR
{
    /// <summary>
    /// MapScene -> ARScene 씬 전환 시 랜드마크 목록과 현재 위치를 넘기는 정적 홀더.
    /// 정적 필드는 SceneManager.LoadScene 간에도 값이 유지되므로 DontDestroyOnLoad 객체가 필요 없다.
    /// </summary>
    public static class ARHandoff
    {
        public static IReadOnlyList<ARLandmarkSnapshot> Landmarks { get; private set; } = Array.Empty<ARLandmarkSnapshot>();
        public static GeoLocationSample InitialLocation { get; private set; }
        public static bool HasData { get; private set; }

        public static void Prepare(IReadOnlyList<ARLandmarkSnapshot> landmarks, GeoLocationSample initialLocation)
        {
            Landmarks = landmarks ?? Array.Empty<ARLandmarkSnapshot>();
            InitialLocation = initialLocation;
            HasData = true;
        }

        public static void Clear()
        {
            Landmarks = Array.Empty<ARLandmarkSnapshot>();
            HasData = false;
        }
    }
}
