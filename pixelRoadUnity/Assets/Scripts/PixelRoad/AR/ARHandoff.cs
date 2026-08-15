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
        public static float InitialHeadingDegrees { get; private set; }
        public static bool HasInitialHeading { get; private set; }

        public static void Prepare(IReadOnlyList<ARLandmarkSnapshot> landmarks, GeoLocationSample initialLocation)
        {
            Landmarks = landmarks ?? Array.Empty<ARLandmarkSnapshot>();
            InitialLocation = initialLocation;
            HasData = true;
        }

        /// <summary>
        /// ARScene(및 ARSession)이 뜨기 전, 아직 MapScene에 있는 동안 예열해 둔 나침반 값을 기억해 둔다.
        /// ARCore가 세션을 시작하면 기기에 따라 나침반 센서를 독점해 이후 Input.compass가 멈추는 경우가
        /// 많아서, ARScene 쪽에서 라이브로 첫 값을 기다리면 늦거나 아예 못 받을 수 있다.
        /// </summary>
        public static void SetInitialHeading(float headingDegrees)
        {
            InitialHeadingDegrees = headingDegrees;
            HasInitialHeading = true;
        }

        public static void Clear()
        {
            Landmarks = Array.Empty<ARLandmarkSnapshot>();
            HasData = false;
            HasInitialHeading = false;
        }
    }
}
