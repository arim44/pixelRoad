namespace PixelRoad.Data
{
    /// <summary>
    /// 랜드마크 정의에 해금 여부만 얹은 실행 중 상태. 정의는 바뀌지 않고 이 래퍼만 변한다.
    /// </summary>
    public sealed class SpotRuntimeState
    {
        public SpotDefinition Definition { get; private set; }
        public bool IsUnlocked { get; private set; }

        /// <summary>정의와 초기 해금 상태를 묶는다.</summary>
        public SpotRuntimeState(SpotDefinition definition, bool isUnlocked)
        {
            Definition = definition;
            IsUnlocked = isUnlocked;
        }

        /// <summary>방문이 확정됐을 때 해금 처리한다. 한 번 열리면 되돌리지 않는다.</summary>
        public void Unlock()
        {
            IsUnlocked = true;
        }
    }
}
