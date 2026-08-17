using System;
using System.Collections.Generic;
using PixelRoad.Data;
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

        /// <summary>
        /// Landmarks와 같은 랜드마크들의 실제 SpotRuntimeState 참조.
        /// ARLandmarkSnapshot은 화면 표시용 경량 DTO라 GlobalValue.SelectedSpot에 쓸 실제 인스턴스가 없어,
        /// AR에서 핀을 클릭했을 때 이 목록에서 id로 찾아 GlobalValue.SelectedSpot에 채워 넣는다.
        /// </summary>
        public static IReadOnlyList<SpotRuntimeState> Spots { get; private set; } = Array.Empty<SpotRuntimeState>();

        public static GeoLocation InitialLocation { get; private set; }
        public static bool HasData { get; private set; }
        public static float InitialHeadingDegrees { get; private set; }
        public static bool HasInitialHeading { get; private set; }

        /// <summary>
        /// MapScene에서 만든 로딩 화면(DontDestroyOnLoad로 씬 전환을 넘어 살아있음)을 ARScene에 넘긴다.
        /// ARSceneController가 자기 UI를 다 세운 뒤 이걸 가져가 페이드아웃시킨다 - MapScene에 있을 때
        /// 미리 페이드아웃해 버리면 전환 애니메이션 도중 지도 화면이 잠깐 다시 보이는(깜빡이는) 문제가 있었다.
        /// </summary>
        public static LoadingScreenView PendingLoadingScreen { get; set; }

        public static void Prepare(
            IReadOnlyList<ARLandmarkSnapshot> landmarks,
            IReadOnlyList<SpotRuntimeState> spots,
            GeoLocation initialLocation)
        {
            Landmarks = landmarks ?? Array.Empty<ARLandmarkSnapshot>();
            Spots = spots ?? Array.Empty<SpotRuntimeState>();
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
            Spots = Array.Empty<SpotRuntimeState>();
            HasData = false;
            HasInitialHeading = false;
        }
    }
}
