using System;
using UnityEngine;

namespace PixelRoad.Mapping
{
    /// <summary>
    /// 지도 카메라 상태(중심 좌표와 줌)를 들고 있으면서 화면 픽셀과 월드 좌표 사이를 변환한다.
    /// </summary>
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

        /// <summary>
        /// 시작 위치와 줌 범위를 정한다. 인자 순서가 뒤바뀌어도 되도록 최소·최대를 정렬해 둔다.
        /// </summary>
        public MapViewState(double latitude, double longitude, float zoom, float minZoom, float maxZoom)
        {
            this.minZoom = Mathf.Min(minZoom, maxZoom);
            this.maxZoom = Mathf.Max(minZoom, maxZoom);
            Zoom = Mathf.Clamp(zoom, this.minZoom, this.maxZoom);
            SetCenter(latitude, longitude);
        }

        /// <summary>
        /// 위경도로 지도 중심을 옮긴다.
        /// </summary>
        public void SetCenter(double latitude, double longitude)
        {
            WorldMercatorPoint world = SlippyMapProjection.LatLonToWorld(latitude, longitude);
            CenterX = world.X;
            CenterY = world.Y;
        }

        /// <summary>
        /// 월드 좌표로 중심을 옮긴다. 유효 범위를 벗어난 값은 감거나 잘라서 받아들인다.
        /// </summary>
        public void SetWorldCenter(WorldMercatorPoint world)
        {
            CenterX = SlippyMapProjection.WrapWorldX(world.X);
            CenterY = SlippyMapProjection.ClampWorldY(world.Y);
        }

        /// <summary>
        /// 드래그한 화면 픽셀만큼 지도를 민다. 손가락을 따라오도록 중심은 반대 방향으로 움직인다.
        /// </summary>
        public void Pan(Vector2 screenDelta)
        {
            double pixelsPerWorld = PixelsPerWorld;
            CenterX = SlippyMapProjection.WrapWorldX(CenterX - screenDelta.x / pixelsPerWorld);
            CenterY = SlippyMapProjection.ClampWorldY(CenterY + screenDelta.y / pixelsPerWorld);
        }

        /// <summary>
        /// 지정한 화면 지점을 고정한 채 확대·축소한다. 그 지점의 지리 좌표가 그대로 남도록 중심을 다시 맞춘다.
        /// </summary>
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

        /// <summary>
        /// 월드 좌표를 뷰포트 중심 기준의 로컬 픽셀 좌표로 바꾼다.
        /// </summary>
        public Vector2 WorldToLocal(WorldMercatorPoint world)
        {
            double pixelsPerWorld = PixelsPerWorld;
            double deltaX = SlippyMapProjection.ShortestWrappedDeltaX(CenterX, world.X);
            double deltaY = world.Y - CenterY;
            return new Vector2((float)(deltaX * pixelsPerWorld), (float)(-deltaY * pixelsPerWorld));
        }

        /// <summary>
        /// 로컬 픽셀 좌표를 월드 좌표로 되돌린다.
        /// </summary>
        public WorldMercatorPoint LocalToWorld(Vector2 localPoint)
        {
            double pixelsPerWorld = PixelsPerWorld;
            return new WorldMercatorPoint(
                SlippyMapProjection.WrapWorldX(CenterX + localPoint.x / pixelsPerWorld),
                SlippyMapProjection.ClampWorldY(CenterY - localPoint.y / pixelsPerWorld));
        }

        /// <summary>
        /// 현재 줌에 맞춰 실제로 내려받을 타일 줌 단계를 고른다. 서버가 제공하는 범위 안으로 제한한다.
        /// </summary>
        public int SourceZoom(int minimumSourceZoom, int maximumSourceZoom)
        {
            int lower = Math.Min(minimumSourceZoom, maximumSourceZoom);
            int upper = Math.Max(minimumSourceZoom, maximumSourceZoom);
            return Math.Max(lower, Math.Min(upper, Mathf.FloorToInt(Zoom)));
        }
    }
}
