using System.Collections.Generic;
using PixelRoad.Data;
using UnityEngine;

namespace PixelRoad.Geo
{
    /// <summary>
    /// 랜드마크를 격자 칸에 나눠 담아 두는 공간 인덱스.
    /// 위치가 갱신될 때마다 전체를 훑지 않고 주변 칸만 보기 위한 것이다.
    /// </summary>
    public sealed class SpotSpatialIndex
    {
        private readonly Dictionary<Vector2Int, List<SpotRuntimeState>> cells = new Dictionary<Vector2Int, List<SpotRuntimeState>>();
        private readonly double cellSizeLat;
        private readonly double cellSizeLon;

        /// <summary>
        /// 스팟 목록을 격자에 나눠 담는다. 경도 간격은 위도에 따라 좁아지므로
        /// <paramref name="centerLatitude"/>를 기준으로 보정한다.
        /// </summary>
        public SpotSpatialIndex(IReadOnlyList<SpotRuntimeState> spots, double centerLatitude, float cellSizeMeters)
        {
            double safeCellSize = System.Math.Max(50.0, cellSizeMeters);
            cellSizeLat = safeCellSize / 111320.0;
            double lonScale = System.Math.Cos(centerLatitude * System.Math.PI / 180.0);
            cellSizeLon = safeCellSize / System.Math.Max(1.0, 111320.0 * System.Math.Abs(lonScale));

            for (int i = 0; i < spots.Count; i++)
            {
                Vector2Int key = ToCell(spots[i].Definition.Latitude, spots[i].Definition.Longitude);
                if (!cells.TryGetValue(key, out List<SpotRuntimeState> bucket))
                {
                    bucket = new List<SpotRuntimeState>();
                    cells.Add(key, bucket);
                }

                bucket.Add(spots[i]);
            }
        }

        /// <summary>
        /// 주변 칸에 속한 후보 스팟을 모아 준다. 정확한 거리 판정은 호출한 쪽에서 한다.
        /// </summary>
        public List<SpotRuntimeState> Query(double latitude, double longitude, float radiusMeters)
        {
            List<SpotRuntimeState> results = new List<SpotRuntimeState>();
            Vector2Int center = ToCell(latitude, longitude);
            int range = Mathf.Max(1, Mathf.CeilToInt(radiusMeters / 50f));

            for (int y = center.y - range; y <= center.y + range; y++)
            {
                for (int x = center.x - range; x <= center.x + range; x++)
                {
                    Vector2Int key = new Vector2Int(x, y);
                    if (!cells.TryGetValue(key, out List<SpotRuntimeState> bucket))
                    {
                        continue;
                    }

                    results.AddRange(bucket);
                }
            }

            return results;
        }

        /// <summary>좌표가 속한 격자 칸 좌표를 구한다.</summary>
        private Vector2Int ToCell(double latitude, double longitude)
        {
            int x = Mathf.FloorToInt((float)(longitude / cellSizeLon));
            int y = Mathf.FloorToInt((float)(latitude / cellSizeLat));
            return new Vector2Int(x, y);
        }
    }
}
