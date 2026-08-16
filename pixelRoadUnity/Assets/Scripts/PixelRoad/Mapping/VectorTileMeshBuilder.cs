using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelRoad.Mapping
{
    /// <summary>
    /// 메인 스레드에서 Mesh로 올리기만 하면 되는 순수 배열 형태의 메시 결과물.
    /// </summary>
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

        /// <summary>
        /// 실제로 그리는 레이어 이름 집합을 만든다. 디코딩 단계에 넘겨 불필요한 파싱을 건너뛴다.
        /// </summary>
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

        /// <summary>
        /// 타일의 레이어를 정해진 순서와 z값으로 쌓아 하나의 메시로 만든다. 순서가 곧 그리기 순서다.
        /// </summary>
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

        /// <summary>
        /// 면 레이어를 채워 넣는다. 링 방향으로 외곽선을 가려내고 구멍 링은 덧칠하지 않도록 건너뛴다.
        /// </summary>
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

        /// <summary>
        /// 도로 레이어를 그린다. 굵은 그림자선을 먼저 깔고 그 위에 본선을 얹어 테두리를 만든다.
        /// </summary>
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

        /// <summary>
        /// 도로가 아닌 일반 선 레이어를 그린다. 경계선이나 하천처럼 스타일이 단순한 경우에 쓴다.
        /// </summary>
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

        /// <summary>
        /// 링 하나를 0~1 타일 좌표로 옮기고 중복·일직선 점을 정리한 뒤 삼각분할해 메시에 넣는다.
        /// </summary>
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

        /// <summary>
        /// 선을 구간마다 사각형 띠로 확장해 두께를 만든다. 꺾이는 지점에는 이음새를 덧댄다.
        /// </summary>
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

        /// <summary>
        /// 타일 UV를 Y축이 뒤집힌 월드 좌표로 바꿔 정점 하나를 넣는다.
        /// </summary>
        private static void AddStrokeVertex(MeshAccumulator mesh, Vector2 tileUv, Color32 color, float z)
        {
            mesh.AddVertex(new Vector3(tileUv.x, -tileUv.y, z), color, tileUv);
        }

        /// <summary>
        /// 꺾인 지점에 마름모 조각을 덧대 선 구간 사이의 빈틈을 메운다.
        /// </summary>
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

        /// <summary>
        /// 단순 다각형을 귀 자르기(ear clipping)로 삼각분할한다. 진행이 막히면 빈 결과로 포기한다.
        /// </summary>
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

        /// <summary>
        /// 점이 삼각형 안에 있는지 본다. 귀 자르기에서 잘라도 되는 귀인지 판정할 때 쓴다.
        /// </summary>
        private static bool PointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            float ab = Cross(b - a, point - a);
            float bc = Cross(c - b, point - b);
            float ca = Cross(a - c, point - c);
            return ab >= -0.00000001f && bc >= -0.00000001f && ca >= -0.00000001f;
        }

        /// <summary>
        /// 일직선 위의 불필요한 점을 걷어낸다. 정점 수를 줄이고 삼각분할 실패도 줄인다.
        /// </summary>
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

        /// <summary>
        /// 타일 원좌표 링의 부호 있는 면적. 부호로 외곽 링과 구멍 링을 구분한다.
        /// </summary>
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

        /// <summary>
        /// 정규화된 좌표 링의 부호 있는 면적. 삼각분할 전 감는 방향을 맞추는 데 쓴다.
        /// </summary>
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

        /// <summary>
        /// 2차원 외적. 부호로 두 벡터의 회전 방향을 알 수 있다.
        /// </summary>
        private static float Cross(Vector2 left, Vector2 right)
        {
            return left.x * right.y - left.y * right.x;
        }

        /// <summary>
        /// 토지 용도(kind)에 맞는 바탕색을 고른다. 모르는 값은 기본 배경색으로 둔다.
        /// </summary>
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

        /// <summary>
        /// 학교, 병원 같은 시설 부지의 색을 고른다.
        /// </summary>
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

        /// <summary>
        /// 도로 등급에 따라 색과 굵기를 정한다. 등급이 높을수록 굵고 밝게 그린다.
        /// </summary>
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

        /// <summary>
        /// Shortbread 스키마의 kind 속성을 꺼낸다. 없으면 빈 문자열을 준다.
        /// </summary>
        private static string GetKind(MvtFeature feature)
        {
            if (feature.Properties.TryGetValue("kind", out object value) && value != null)
            {
                return value.ToString();
            }

            return string.Empty;
        }

        /// <summary>
        /// 선을 그릴 때 쓰는 색과 굵기 한 쌍. 굵기 단위는 256픽셀 타일 기준 픽셀이다.
        /// </summary>
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

        /// <summary>
        /// 정점과 삼각형을 모으는 버퍼. 정점 수 상한을 넘지 않도록 관리해 폭주를 막는다.
        /// </summary>
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

            /// <summary>
            /// 정점을 그만큼 더 넣어도 상한을 넘지 않는지 미리 확인한다.
            /// </summary>
            public bool CanAdd(int count)
            {
                return count >= 0 && vertices.Count <= maximumVertices - count;
            }

            /// <summary>
            /// 위치, 색, 타일 UV를 한 세트로 추가한다. 세 목록의 인덱스는 항상 짝을 이룬다.
            /// </summary>
            public void AddVertex(Vector3 vertex, Color32 color, Vector2 tileUv)
            {
                vertices.Add(vertex);
                colors.Add(color);
                tileUvs.Add(tileUv);
            }

            /// <summary>
            /// 정점 인덱스 세 개로 삼각형 하나를 추가한다.
            /// </summary>
            public void AddTriangle(int first, int second, int third)
            {
                triangles.Add(first);
                triangles.Add(second);
                triangles.Add(third);
            }

            /// <summary>
            /// 모아 둔 내용을 배열로 굳혀 결과 객체로 넘긴다.
            /// </summary>
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
