using UnityEngine;

namespace PixelRoad.Data
{
    /// <summary>
    /// 씬과 시스템을 넘어 공유하는 전역 값 모음.
    ///
    /// AR처럼 지도 UI와 참조를 주고받기 어려운 곳에서도 "지금 무엇을 고른 상태인지"를 알아야 해서
    /// 선택 상태만 여기에 둔다. 값을 늘릴 때는 정말 전역이어야 하는지 먼저 따져 본다.
    /// </summary>
    public static class GlobalValue
    {
        /// <summary>지도에서 선택 중인 랜드마크. 선택이 없으면 null.</summary>
        public static SpotRuntimeState SelectedSpot { get; set; }

        /// <summary>
        /// 도메인 리로드를 끄고(Enter Play Mode Options) 에디터에서 Stop 후 다시 Play 할 때
        /// 이전 세션의 값이 남지 않도록 앱 프로세스당 한 번만 비운다.
        /// MapScene은 ARScene 왕복 등으로 플레이 중에도 여러 번 다시 로드되는데, 그때마다 비우면
        /// AR에서 고른 선택 상태가 지도로 돌아오자마자 사라져 버리므로 씬 로드 시점이 아니라 여기서 한 번만 한다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearOnProcessStart()
        {
            SelectedSpot = null;
        }

        /// <summary>선택 상태를 비운다.</summary>
        public static void Clear()
        {
            SelectedSpot = null;
        }
    }
}
