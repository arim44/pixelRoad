using System.Collections.Generic;
using PixelRoad.Data;
using UnityEngine;

namespace PixelRoad.Geo
{
    public sealed class SpotSpatialIndex
    {
        private readonly Dictionary<Vector2Int, List<SpotRuntimeState>> cells = new Dictionary<Vector2Int, List<SpotRuntimeState>>();
        private readonly double cellSizeLat;
        private readonly double cellSizeLon;

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

        private Vector2Int ToCell(double latitude, double longitude)
        {
            int x = Mathf.FloorToInt((float)(longitude / cellSizeLon));
            int y = Mathf.FloorToInt((float)(latitude / cellSizeLat));
            return new Vector2Int(x, y);
        }
    }
}
