namespace PixelRoad.AR
{
    public interface IHeadingProvider
    {
        /// <summary>진북 기준 0~360도, 시계 방향 기기 방위각.</summary>
        float HeadingDegrees { get; }
        bool IsAvailable { get; }
        void Start();
        void Tick(float deltaTime);
        void Stop();
    }
}
