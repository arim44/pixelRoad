using System;
using PixelRoad.Data;
using UnityEngine;
using UnityEngine.UI;

namespace PixelRoad.Mapping
{
    public interface ILiveMapController
    {
        event Action ViewChanged;
        event Action FirstTileReady;

        bool IsInitialized { get; }
        bool HasRenderedTile { get; }
        string LastError { get; }

        bool Initialize(
            MapConfig mapConfig,
            RectTransform mapViewport,
            RawImage mapOutput,
            double startLatitude,
            double startLongitude);

        void Pan(Vector2 screenDelta);
        void ZoomAt(float factor, Vector2 screenPosition);
        void SetCenter(double latitude, double longitude);
        Vector2 LatLonToViewportLocal(double latitude, double longitude);
        bool IsInViewport(double latitude, double longitude, float padding = 0f);
        void SetPixelMode(bool enabled);
    }
}
