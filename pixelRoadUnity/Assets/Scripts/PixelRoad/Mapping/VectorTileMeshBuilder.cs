using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelRoad.Mapping
{
    public sealed class VectorTileMeshData
    {
        public readonly Vector3[] Vertices;
        public readonly int[] Triangles;
        public readonly Color32[] Colors;
        public readonly Vector2[] TileUvs;

        public bool IsEmpty
        {
            get { return Vertices.Length == 0 || Triangles.Length == 0; }
        }

        public VectorTileMeshData(Vector3[] vertices, int[] triangles, Color32[] colors, Vector2[] tileUvs)
        {
            Vertices = vertices ?? Array.Empty<Vector3>();
            Triangles = triangles ?? Array.Empty<int>();
            Colors = colors ?? Array.Empty<Color32>();
            TileUvs = tileUvs ?? Array.Empty<Vector2>();
        }
    }

    /// <summary>
    /// Turns the label-free subset of the OSM Shortbread schema into a single tile mesh.
    /// The result contains only value types and arrays, so Build may run off the main thread.
    /// </summary>
    public static class VectorTileMeshBuilder
    {
        private const int MaximumVerticesPerTile = 500000;
        private const float MinimumSegmentLengthSquared = 0.0000000001f;

        public static readonly Color32 BackgroundColor = new Color32(107, 139, 86, 255);

        private static readonly Color32 Residential = new Color32(119, 135, 98, 255);
        private static readonly Color32 Commercial = new Color32(135, 124, 94, 255);
        private static readonly Color32 Industrial = new Color32(118, 116, 103, 255);
        private static readonly Color32 Green = new Color32(80, 135, 80, 255);
        private static readonly Color32 DeepGreen = new Color32(58, 111, 68, 255);
        private static readonly Color32 Water = new Color32(46, 130, 142, 255);
        private static readonly Color32 RoadShadow = new Color32(54, 48, 43, 255);
        private static readonly Color32 MajorRoad = new Color32(213, 179, 112, 255);
        private static readonly Color32 MinorRoad = new Color32(181, 162, 116, 255);
        private static readonly Color32 Footway = new Color32(210, 200, 160, 255);
        private static readonly Color32 Rail = new Color32(49, 45, 48, 255);
        private static readonly Color32 Boundary = new Color32(85, 78, 68, 255);

        public static ISet<string> CreateSupportedLayerFilter()
        {
            return new HashSet<string>(StringComparer.Ordinal)
            {
                "land",
                "sites",
                "water_polygons",
                "ocean",
                "water_lines",
                "street_polygons",
                "streets",
                "boundaries"
            };
        }

        public static VectorTileMeshData Build(MvtTile tile)
        {
            if (tile == null)
            {
                throw new ArgumentNullException(nameof(tile));
            }

            MeshAccumulator mesh = new MeshAccumulator(MaximumVerticesPerTile);
            AddPolygonLayer(tile.FindLayer("land"), mesh, LandColor, 0f);
            AddPolygonLayer(tile.FindLayer("sites"), mesh, SiteColor, -0.01f);
            AddPolygonLayer(tile.FindLayer("ocean"), mesh, _ => Water, -0.02f);
            AddPolygonLayer(tile.FindLayer("water_polygons"), mesh, _ => Water, -0.021f);
            AddPolygonLayer(tile.FindLayer("street_polygons"), mesh, _ => MinorRoad, -0.03f);
            AddLineLayer(tile.FindLayer("boundaries"), mesh, _ => new LineStyle(Boundary, 1.2f), -0.035f, false);
            AddLineLayer(tile.FindLayer("water_lines"), mesh, _ => new LineStyle(Water, 3f), -0.04f, false);
            AddStreetLayer(tile.FindLayer("streets"), mesh);
            return mesh.ToData();
        }

        private static void AddPolygonLayer(
            MvtLayer layer,
            MeshAccumulator mesh,
            Func<MvtFeature, Color32> colorSelector,
            float z)
        {
            if (layer == null || layer.Extent == 0U)
            {
                return;
            }

            float inverseExtent = 1f / layer.Extent;
            for (int featureIndex = 0; featureIndex < layer.Features.Count; featureIndex++)
            {
                MvtFeature feature = layer.Features[featureIndex];
                if (feature.GeometryType != MvtGeometryType.Polygon)
                {
                    continue;
                }

                Color32 color = colorSelector(feature);
                bool hasExteriorOrientation = false;
                for (int pathIndex = 0; pathIndex < feature.Paths.Count; pathIndex++)
                {
                    MvtPath path = feature.Paths[pathIndex];
                    if (path.IsClosed && SignedArea(path.Points) > 0.0)
                    {
                        hasExteriorOrientation = true;
                        break;
                    }
                }

                for (int pathIndex = 0; pathIndex < feature.Paths.Count; pathIndex++)
                {
                    MvtPath path = feature.Paths[pathIndex];
                    if (!path.IsClosed || path.Points.Count < 3)
                    {
                        continue;
                    }

                    double area = SignedArea(path.Points);
                    // MVT 2.x exterior rings are clockwise in screen coordinates,
                    // which produces a positive signed area with the original Y-down data.
                    // Inner rings are omitted instead of being incorrectly painted over.
                    if (hasExteriorOrientation && area <= 0.0)
                    {
                        continue;
                    }

                    AddPolygon(path.Points, inverseExtent, color, z, mesh);
                    if (mesh.IsFull)
                    {
                        return;
                    }
                }
            }
        }

        private static void AddStreetLayer(MvtLayer layer, MeshAccumulator mesh)
        {
            if (layer == null || layer.Extent == 0U)
            {
                return;
            }

            float inverseExtent = 1f / layer.Extent;
            for (int featureIndex = 0; featureIndex < layer.Features.Count; featureIndex++)
            {
                MvtFeature feature = layer.Features[featureIndex];
                if (feature.GeometryType != MvtGeometryType.LineString)
                {
                    continue;
                }

                LineStyle style = StreetStyle(feature);
                float width = style.WidthPixels / 256f;
                float casingWidth = (style.WidthPixels + 1.5f) / 256f;
                for (int pathIndex = 0; pathIndex < feature.Paths.Count; pathIndex++)
                {
                    IReadOnlyList<MvtPoint> points = feature.Paths[pathIndex].Points;
                    if (points.Count < 2)
                    {
                        continue;
                    }

                    AddStroke(points, inverseExtent, casingWidth, RoadShadow, -0.05f, mesh);
                    AddStroke(points, inverseExtent, width, style.Color, -0.052f, mesh);
                    if (mesh.IsFull)
                    {
                        return;
                    }
                }
            }
        }

        private static void AddLineLayer(
            MvtLayer layer,
            MeshAccumulator mesh,
            Func<MvtFeature, LineStyle> styleSelector,
            float z,
            bool addCasing)
        {
            if (layer == null || layer.Extent == 0U)
            {
                return;
            }

            float inverseExtent = 1f / layer.Extent;
            for (int featureIndex = 0; featureIndex < layer.Features.Count; featureIndex++)
            {
                MvtFeature feature = layer.Features[featureIndex];
                if (feature.GeometryType != MvtGeometryType.LineString)
                {
                    continue;
                }

                LineStyle style = styleSelector(feature);
                for (int pathIndex = 0; pathIndex < feature.Paths.Count; pathIndex++)
                {
                    IReadOnlyList<MvtPoint> points = feature.Paths[pathIndex].Points;
                    if (points.Count < 2)
                    {
                        continue;
                    }

                    if (addCasing)
                    {
                        AddStroke(points, inverseExtent, (style.WidthPixels + 1.5f) / 256f, RoadShadow, z, mesh);
                    }

                    AddStroke(points, inverseExtent, style.WidthPixels / 256f, style.Color, z - 0.002f, mesh);
                    if (mesh.IsFull)
                    {
                        return;
                    }
                }
            }
        }

        private static void AddPolygon(
            IReadOnlyList<MvtPoint> source,
            float inverseExtent,
            Color32 color,
            float z,
            MeshAccumulator mesh)
        {
            List<Vector2> points = new List<Vector2>(source.Count);
            for (int index = 0; index < source.Count; index++)
            {
                Vector2 point = new Vector2(source[index].X * inverseExtent, source[index].Y * inverseExtent);
                if (points.Count == 0 || (points[points.Count - 1] - point).sqrMagnitude > MinimumSegmentLengthSquared)
                {
                    points.Add(point);
                }
            }

            if (points.Count > 2 && (points[0] - points[points.Count - 1]).sqrMagnitude <= MinimumSegmentLengthSquared)
            {
                points.RemoveAt(points.Count - 1);
            }

            RemoveCollinearPoints(points);
            if (points.Count < 3 || !mesh.CanAdd(points.Count))
            {
                return;
            }

            List<int> localTriangles = Triangulate(points);
            if (localTriangles.Count == 0)
            {
                return;
            }

            int vertexStart = mesh.VertexCount;
            for (int index = 0; index < points.Count; index++)
            {
                Vector2 point = points[index];
                mesh.AddVertex(new Vector3(point.x, -point.y, z), color, point);
            }

            for (int index = 0; index < localTriangles.Count; index += 3)
            {
                mesh.AddTriangle(
                    vertexStart + localTriangles[index],
                    vertexStart + localTriangles[index + 1],
                    vertexStart + localTriangles[index + 2]);
            }
        }

        private static void AddStroke(
            IReadOnlyList<MvtPoint> source,
            float inverseExtent,
            float width,
            Color32 color,
            float z,
            MeshAccumulator mesh)
        {
            float halfWidth = Mathf.Max(0.00015f, width * 0.5f);
            for (int index = 1; index < source.Count; index++)
            {
                Vector2 fromUv = new Vector2(source[index - 1].X * inverseExtent, source[index - 1].Y * inverseExtent);
                Vector2 toUv = new Vector2(source[index].X * inverseExtent, source[index].Y * inverseExtent);
                Vector2 direction = toUv - fromUv;
                if (direction.sqrMagnitude <= MinimumSegmentLengthSquared || !mesh.CanAdd(4))
                {
                    continue;
                }

                direction.Normalize();
                Vector2 normal = new Vector2(-direction.y, direction.x) * halfWidth;
                int start = mesh.VertexCount;
                AddStrokeVertex(mesh, fromUv - normal, color, z);
                AddStrokeVertex(mesh, fromUv + normal, color, z);
                AddStrokeVertex(mesh, toUv + normal, color, z);
                AddStrokeVertex(mesh, toUv - normal, color, z);
                mesh.AddTriangle(start, start + 1, start + 2);
                mesh.AddTriangle(start, start + 2, start + 3);

                if (index < source.Count - 1 && mesh.CanAdd(4))
                {
                    AddSquareJoint(mesh, toUv, halfWidth, color, z);
                }
            }
        }

        private static void AddStrokeVertex(MeshAccumulator mesh, Vector2 tileUv, Color32 color, float z)
        {
            mesh.AddVertex(new Vector3(tileUv.x, -tileUv.y, z), color, tileUv);
        }

        private static void AddSquareJoint(MeshAccumulator mesh, Vector2 center, float radius, Color32 color, float z)
        {
            int start = mesh.VertexCount;
            AddStrokeVertex(mesh, center + new Vector2(-radius, 0f), color, z);
            AddStrokeVertex(mesh, center + new Vector2(0f, -radius), color, z);
            AddStrokeVertex(mesh, center + new Vector2(radius, 0f), color, z);
            AddStrokeVertex(mesh, center + new Vector2(0f, radius), color, z);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }

        private static List<int> Triangulate(List<Vector2> points)
        {
            int count = points.Count;
            List<int> result = new List<int>(Math.Max(0, (count - 2) * 3));
            if (count < 3)
            {
                return result;
            }

            List<int> remaining = new List<int>(count);
            bool counterClockwise = SignedArea(points) > 0f;
            if (counterClockwise)
            {
                for (int index = 0; index < count; index++)
                {
                    remaining.Add(index);
                }
            }
            else
            {
                for (int index = count - 1; index >= 0; index--)
                {
                    remaining.Add(index);
                }
            }

            int safety = count * count;
            while (remaining.Count > 2 && safety-- > 0)
            {
                bool clipped = false;
                for (int index = 0; index < remaining.Count; index++)
                {
                    int previous = remaining[(index + remaining.Count - 1) % remaining.Count];
                    int current = remaining[index];
                    int next = remaining[(index + 1) % remaining.Count];
                    Vector2 a = points[previous];
                    Vector2 b = points[current];
                    Vector2 c = points[next];
                    if (Cross(b - a, c - b) <= 0.00000001f)
                    {
                        continue;
                    }

                    bool containsPoint = false;
                    for (int otherIndex = 0; otherIndex < remaining.Count; otherIndex++)
                    {
                        int candidate = remaining[otherIndex];
                        if (candidate == previous || candidate == current || candidate == next)
                        {
                            continue;
                        }

                        if (PointInTriangle(points[candidate], a, b, c))
                        {
                            containsPoint = true;
                            break;
                        }
                    }

                    if (containsPoint)
                    {
                        continue;
                    }

                    result.Add(previous);
                    result.Add(current);
                    result.Add(next);
                    remaining.RemoveAt(index);
                    clipped = true;
                    break;
                }

                if (!clipped)
                {
                    result.Clear();
                    return result;
                }
            }

            return result;
        }

        private static bool PointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            float ab = Cross(b - a, point - a);
            float bc = Cross(c - b, point - b);
            float ca = Cross(a - c, point - c);
            return ab >= -0.00000001f && bc >= -0.00000001f && ca >= -0.00000001f;
        }

        private static void RemoveCollinearPoints(List<Vector2> points)
        {
            bool removed;
            do
            {
                removed = false;
                for (int index = points.Count - 1; index >= 0 && points.Count >= 3; index--)
                {
                    Vector2 previous = points[(index + points.Count - 1) % points.Count];
                    Vector2 current = points[index];
                    Vector2 next = points[(index + 1) % points.Count];
                    if (Mathf.Abs(Cross(current - previous, next - current)) <= 0.00000001f)
                    {
                        points.RemoveAt(index);
                        removed = true;
                    }
                }
            }
            while (removed && points.Count >= 3);
        }

        private static double SignedArea(IReadOnlyList<MvtPoint> points)
        {
            double area = 0.0;
            for (int index = 0; index < points.Count; index++)
            {
                MvtPoint current = points[index];
                MvtPoint next = points[(index + 1) % points.Count];
                area += (double)current.X * next.Y - (double)next.X * current.Y;
            }

            return area * 0.5;
        }

        private static float SignedArea(IReadOnlyList<Vector2> points)
        {
            float area = 0f;
            for (int index = 0; index < points.Count; index++)
            {
                Vector2 current = points[index];
                Vector2 next = points[(index + 1) % points.Count];
                area += current.x * next.y - next.x * current.y;
            }

            return area * 0.5f;
        }

        private static float Cross(Vector2 left, Vector2 right)
        {
            return left.x * right.y - left.y * right.x;
        }

        private static Color32 LandColor(MvtFeature feature)
        {
            string kind = GetKind(feature);
            switch (kind)
            {
                case "forest":
                case "garden":
                case "park":
                    return DeepGreen;
                case "grass":
                case "meadow":
                case "cemetery":
                    return Green;
                case "commercial":
                case "retail":
                    return Commercial;
                case "industrial":
                case "brownfield":
                case "railway":
                    return Industrial;
                case "residential":
                    return Residential;
                default:
                    return BackgroundColor;
            }
        }

        private static Color32 SiteColor(MvtFeature feature)
        {
            string kind = GetKind(feature);
            switch (kind)
            {
                case "sports_centre":
                    return Green;
                case "school":
                case "hospital":
                case "university":
                    return new Color32(128, 126, 96, 255);
                case "construction":
                case "parking":
                    return Industrial;
                default:
                    return Residential;
            }
        }

        private static LineStyle StreetStyle(MvtFeature feature)
        {
            string kind = GetKind(feature);
            switch (kind)
            {
                case "motorway":
                case "trunk":
                    return new LineStyle(MajorRoad, 7f);
                case "primary":
                    return new LineStyle(MajorRoad, 6f);
                case "secondary":
                    return new LineStyle(MajorRoad, 5f);
                case "tertiary":
                    return new LineStyle(MajorRoad, 4f);
                case "rail":
                case "subway":
                case "tram":
                    return new LineStyle(Rail, 2f);
                case "footway":
                case "cycleway":
                case "steps":
                case "path":
                    return new LineStyle(Footway, 1f);
                case "service":
                    return new LineStyle(MinorRoad, 1.2f);
                default:
                    return new LineStyle(MinorRoad, 2.2f);
            }
        }

        private static string GetKind(MvtFeature feature)
        {
            if (feature.Properties.TryGetValue("kind", out object value) && value != null)
            {
                return value.ToString();
            }

            return string.Empty;
        }

        private readonly struct LineStyle
        {
            public readonly Color32 Color;
            public readonly float WidthPixels;

            public LineStyle(Color32 color, float widthPixels)
            {
                Color = color;
                WidthPixels = widthPixels;
            }
        }

        private sealed class MeshAccumulator
        {
            private readonly int maximumVertices;
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int> triangles = new List<int>();
            private readonly List<Color32> colors = new List<Color32>();
            private readonly List<Vector2> tileUvs = new List<Vector2>();

            public int VertexCount
            {
                get { return vertices.Count; }
            }

            public bool IsFull
            {
                get { return vertices.Count >= maximumVertices; }
            }

            public MeshAccumulator(int maximumVertices)
            {
                this.maximumVertices = maximumVertices;
            }

            public bool CanAdd(int count)
            {
                return count >= 0 && vertices.Count <= maximumVertices - count;
            }

            public void AddVertex(Vector3 vertex, Color32 color, Vector2 tileUv)
            {
                vertices.Add(vertex);
                colors.Add(color);
                tileUvs.Add(tileUv);
            }

            public void AddTriangle(int first, int second, int third)
            {
                triangles.Add(first);
                triangles.Add(second);
                triangles.Add(third);
            }

            public VectorTileMeshData ToData()
            {
                return new VectorTileMeshData(
                    vertices.ToArray(),
                    triangles.ToArray(),
                    colors.ToArray(),
                    tileUvs.ToArray());
            }
        }
    }
}
