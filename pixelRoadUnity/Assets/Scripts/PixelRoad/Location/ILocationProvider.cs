using System.Collections;

namespace PixelRoad.Location
{
    /// <summary>
    /// 현재 위치를 공급하는 창구. 실기기 GPS와 에디터 시뮬레이션을 같은 방식으로 쓰기 위한 인터페이스다.
    /// </summary>
    public interface ILocationProvider
    {
        GeoLocation Current { get; }
        string StatusText { get; }
        /// <summary>권한 요청과 초기화를 진행한다. 준비까지 시간이 걸리므로 코루틴으로 돌린다.</summary>
        IEnumerator Start();
        /// <summary>매 프레임 호출해 현재 위치를 갱신한다.</summary>
        void Tick(float deltaTime);
        /// <summary>위치 갱신을 멈추고 자원을 놓는다.</summary>
        void Stop();
    }
}
