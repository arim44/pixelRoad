using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelRoad.Mapping
{
    /// <summary>
    /// 타일 하나를 가리키는 좌표 식별자. 캐시와 요청 관리의 사전 키로 쓰이므로 값 비교가 가능하다.
    /// </summary>
    public readonly struct TileKey : IEquatable<TileKey>
    {
        public readonly int Zoom;
        public readonly int X;
        public readonly int Y;

        /// <summary>줌 레벨과 타일 좌표로 키를 만든다.</summary>
        public TileKey(int zoom, int x, int y)
        {
            Zoom = zoom;
            X = x;
            Y = y;
        }

        /// <summary>세 좌표 값이 모두 같은지 비교한다.</summary>
        public bool Equals(TileKey other)
        {
            return Zoom == other.Zoom && X == other.X && Y == other.Y;
        }

        /// <summary>박싱된 값과의 비교를 같은 규칙으로 처리한다.</summary>
        public override bool Equals(object obj)
        {
            return obj is TileKey other && Equals(other);
        }

        /// <summary>사전 조회 성능을 위해 세 좌표를 섞어 해시를 만든다.</summary>
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

        /// <summary>로그와 오브젝트 이름에 쓰기 좋은 z/x/y 문자열을 만든다.</summary>
        public override string ToString()
        {
            return Zoom + "/" + X + "/" + Y;
        }
    }

    /// <summary>
    /// 화면에 배치될 타일 하나의 정보. 요청 키와 화면상 위치를 함께 들고 있어
    /// 경도 방향으로 감싸인(wrap) 타일도 올바른 자리에 그릴 수 있다.
    /// </summary>
    public readonly struct VisibleTile
    {
        public readonly TileKey Key;
        public readonly int DisplayX;
        public readonly int DisplayY;
        public readonly double Priority;

        /// <summary>요청 키, 화면 배치 좌표, 중심까지의 거리 우선순위를 묶는다.</summary>
        public VisibleTile(TileKey key, int displayX, int displayY, double priority)
        {
            Key = key;
            DisplayX = displayX;
            DisplayY = displayY;
            Priority = priority;
        }
    }

    /// <summary>
    /// 현재 뷰 상태에서 실제로 보이는 타일 목록을 계산한다. 네트워크 요청량을 화면 범위로 제한하는 역할을 한다.
    /// </summary>
    public static class TileCoverage
    {
        /// <summary>
        /// 뷰포트를 덮는 타일들을 구해 중심에서 가까운 순으로 정렬해 돌려준다.
        /// 같은 타일이 좌우로 반복돼도 요청은 한 번만 나가도록 중복을 걸러낸다.
        /// </summary>
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

        /// <summary>음수 좌표도 항상 0 이상으로 접어 경도 방향 순환을 처리한다.</summary>
        private static int PositiveModulo(int value, int divisor)
        {
            int remainder = value % divisor;
            return remainder < 0 ? remainder + divisor : remainder;
        }
    }
}
