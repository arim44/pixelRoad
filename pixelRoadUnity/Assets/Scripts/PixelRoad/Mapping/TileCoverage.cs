using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelRoad.Mapping
{
    public readonly struct TileKey : IEquatable<TileKey>
    {
        public readonly int Zoom;
        public readonly int X;
        public readonly int Y;

        public TileKey(int zoom, int x, int y)
        {
            Zoom = zoom;
            X = x;
            Y = y;
        }

        public bool Equals(TileKey other)
        {
            return Zoom == other.Zoom && X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is TileKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Zoom;
                hash = hash * 397 ^ X;
                hash = hash * 397 ^ Y;
                return hash;
            }
        }

        public override string ToString()
        {
            return Zoom + "/" + X + "/" + Y;
        }
    }

    public readonly struct VisibleTile
    {
        public readonly TileKey Key;
        public readonly int DisplayX;
        public readonly int DisplayY;
        public readonly double Priority;

        public VisibleTile(TileKey key, int displayX, int displayY, double priority)
        {
            Key = key;
            DisplayX = displayX;
            DisplayY = displayY;
            Priority = priority;
        }
    }

    public static class TileCoverage
    {
        public static List<VisibleTile> Calculate(MapViewState view, Vector2 viewportSize, int minimumSourceZoom, int maximumSourceZoom)
        {
            int zoom = view.SourceZoom(minimumSourceZoom, maximumSourceZoom);
            int tileCount = 1 << zoom;
            double centerTileX = view.CenterX * tileCount;
            double centerTileY = view.CenterY * tileCount;
            double tileDisplaySize = MapViewState.TileSize * Math.Pow(2.0, view.Zoom - zoom);
            double halfTilesX = Math.Max(0.0, viewportSize.x) / (2.0 * tileDisplaySize);
            double halfTilesY = Math.Max(0.0, viewportSize.y) / (2.0 * tileDisplaySize);

            int minimumX = (int)Math.Floor(centerTileX - halfTilesX);
            int maximumX = (int)Math.Floor(centerTileX + halfTilesX);
            int minimumY = Math.Max(0, (int)Math.Floor(centerTileY - halfTilesY));
            int maximumY = Math.Min(tileCount - 1, (int)Math.Floor(centerTileY + halfTilesY));

            List<VisibleTile> result = new List<VisibleTile>();
            HashSet<TileKey> seenRequests = new HashSet<TileKey>();
            for (int displayY = minimumY; displayY <= maximumY; displayY++)
            {
                for (int displayX = minimumX; displayX <= maximumX; displayX++)
                {
                    int requestX = PositiveModulo(displayX, tileCount);
                    TileKey key = new TileKey(zoom, requestX, displayY);
                    if (!seenRequests.Add(key))
                    {
                        continue;
                    }

                    double deltaX = displayX + 0.5 - centerTileX;
                    double deltaY = displayY + 0.5 - centerTileY;
                    result.Add(new VisibleTile(key, displayX, displayY, deltaX * deltaX + deltaY * deltaY));
                }
            }

            result.Sort((left, right) => left.Priority.CompareTo(right.Priority));
            return result;
        }

        private static int PositiveModulo(int value, int divisor)
        {
            int remainder = value % divisor;
            return remainder < 0 ? remainder + divisor : remainder;
        }
    }
}
