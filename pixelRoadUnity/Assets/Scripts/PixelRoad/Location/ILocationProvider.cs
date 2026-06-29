using System.Collections;

namespace PixelRoad.Location
{
    public interface ILocationProvider
    {
        GeoLocationSample Current { get; }
        string StatusText { get; }
        IEnumerator Start();
        void Tick(float deltaTime);
        void Stop();
    }
}
