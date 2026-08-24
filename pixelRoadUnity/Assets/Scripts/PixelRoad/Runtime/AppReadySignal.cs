using System;
using UnityEngine;

namespace PixelRoad.Runtime
{
    /// <summary>
    /// 로딩 씬과 지도 씬을 잇는 준비 완료 신호.
    ///
    /// 로딩 화면은 씬 로드가 끝난 뒤에도 지도 첫 타일이 그려질 때까지 떠 있어야 한다.
    /// 로딩 오버레이는 DontDestroyOnLoad로 살아남고, 지도 씬의 <see cref="PixelRoadApp"/>가
    /// 준비가 끝났을 때 <see cref="RaiseMapReady"/>를 호출한다.
    /// 서로를 직접 참조하지 않도록 정적 신호 하나만 공유한다.
    /// </summary>
    public static class AppReadySignal
    {
        private static Action mapReady;

        /// <summary>지도 표시 준비가 끝났는지. 지도를 쓸 수 없는 구성이어도 true가 된다.</summary>
        public static bool IsMapReady { get; private set; }

        /// <summary>
        /// 준비 완료 시 한 번만 호출된다. 구독 시점에 이미 준비가 끝났으면 즉시 호출된다.
        /// </summary>
        public static event Action MapReady
        {
            add
            {
                if (value == null)
                {
                    return;
                }

                if (IsMapReady)
                {
                    value();
                    return;
                }

                mapReady += value;
            }

            remove { mapReady -= value; }
        }

        /// <summary>준비 완료를 알린다. 두 번째부터는 무시되고, 구독자는 호출 뒤 비운다.</summary>
        public static void RaiseMapReady()
        {
            if (IsMapReady)
            {
                return;
            }

            IsMapReady = true;
            Action handler = mapReady;
            mapReady = null;
            handler?.Invoke();
        }

        /// <summary>
        /// 도메인 리로드를 끈 프로젝트에서도 플레이 시작 시 상태가 남지 않도록 초기화한다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            IsMapReady = false;
            mapReady = null;
        }
    }
}
