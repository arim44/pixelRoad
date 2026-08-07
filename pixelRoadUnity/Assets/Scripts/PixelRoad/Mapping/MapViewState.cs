using System;
using UnityEngine;

namespace PixelRoad.Mapping
{
    public sealed class MapViewState
    {
        public const double TileSize = 256.0;

        private readonly float minZoom;
        private readonly float maxZoom;

        public double CenterX { get; private set; }
        public double CenterY { get; private set; }
        public float Zoom { get; private set; }

        public double PixelsPerWorld
        {
            get { return TileSize * Math.Pow(2.0, Zoom); }
        }

        public MapViewState(double latitude, double longitude, float zoom, float minZoom, float maxZoom)
        {
            this.minZoom = Mathf.Min(minZoom, maxZoom);
            this.maxZoom = Mathf.Max(minZoom, maxZoom);
            Zoom = Mathf.Clamp(zoom, this.minZoom, this.maxZoom);
            SetCenter(latitude, longitude);
        }

        public void SetCenter(double latitude, double longitude)
        {
            WorldMercatorPoint world = SlippyMapProjection.LatLonToWorld(latitude, longitude);
            CenterX = world.X;
            CenterY = world.Y;
        }

        public void SetWorldCenter(WorldMercatorPoint world)
        {
            CenterX = SlippyMapProjection.WrapWorldX(world.X);
            CenterY = SlippyMapProjection.ClampWorldY(world.Y);
        }

        public void Pan(Vector2 screenDelta)
        {
            double pixelsPerWorld = PixelsPerWorld;
            CenterX = SlippyMapProjection.WrapWorldX(CenterX - screenDelta.x / pixelsPerWorld);
            CenterY = SlippyMapProjection.ClampWorldY(CenterY + screenDelta.y / pixelsPerWorld);
        }

        public void ZoomAt(float factor, Vector2 localPoint)
        {
            if (factor <= 0f)
            {
                return;
            }

            WorldMercatorPoint before = LocalToWorld(localPoint);
            float nextZoom = Mathf.Clamp(Zoom + Mathf.Log(factor, 2f), minZoom, maxZoom);
            if (Mathf.Approximately(nextZoom, Zoom))
            {
                return;
            }

            Zoom = nextZoom;
            double pixelsPerWorld = PixelsPerWorld;
            CenterX = SlippyMapProjection.WrapWorldX(before.X - localPoint.x / pixelsPerWorld);
            CenterY = SlippyMapProjection.ClampWorldY(before.Y + localPoint.y / pixelsPerWorld);
        }

        public Vector2 WorldToLocal(WorldMercatorPoint world)
        {
            double pixelsPerWorld = PixelsPerWorld;
            double deltaX = SlippyMapProjection.ShortestWrappedDeltaX(CenterX, world.X);
            double deltaY = world.Y - CenterY;
            return new Vector2((float)(deltaX * pixelsPerWorld), (float)(-deltaY * pixelsPerWorld));
        }

        public WorldMercatorPoint LocalToWorld(Vector2 localPoint)
        {
            double pixelsPerWorld = PixelsPerWorld;
            return new WorldMercatorPoint(
                SlippyMapProjection.WrapWorldX(CenterX + localPoint.x / pixelsPerWorld),
                SlippyMapProjection.ClampWorldY(CenterY - localPoint.y / pixelsPerWorld));
        }

        public int SourceZoom(int minimumSourceZoom, int maximumSourceZoom)
        {
            int lower = Math.Min(minimumSourceZoom, maximumSourceZoom);
            int upper = Math.Max(minimumSourceZoom, maximumSourceZoom);
            return Math.Max(lower, Math.Min(upper, Mathf.FloorToInt(Zoom)));
        }
    }
}
