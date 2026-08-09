using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace PixelRoad.Tests.EditMode
{
    public sealed class VectorTileProviderTests
    {
        [Test]
        public void Provider_AcceptsOnlyCompleteHttpsTemplateAndEnforcesTileRange()
        {
            object valid = CreateProvider(
                "contest-provider",
                "https://tiles.example.test/vector/{z}/{x}/{y}.mvt",
                5,
                14,
                "com.example.Pixel Road\r\n");

            Assert.That(MappingApi.Property<bool>(valid, "IsValid"), Is.True);
            Assert.That(MappingApi.Property<string>(valid, "RequestedWithHeaderValue"), Is.EqualTo("com.example.Pixel-Road"));
            Assert.That(
                (string)MappingApi.Type("PixelRoad.Mapping.VectorTileProvider")
                    .GetField("RequestedWithHeaderName", BindingFlags.Public | BindingFlags.Static)
                    .GetRawConstantValue(),
                Is.EqualTo("X-Requested-With"));

            Assert.That(TryBuildUrl(valid, 13, 6987, 3175, out string url, out string error), Is.True);
            Assert.That(url, Is.EqualTo("https://tiles.example.test/vector/13/6987/3175.mvt"));
            Assert.That(error, Is.Null);

            Assert.That(TryBuildUrl(valid, 4, 0, 0, out _, out error), Is.False);
            Assert.That(error, Does.Contain("zoom"));
            Assert.That(TryBuildUrl(valid, 13, 8192, 0, out _, out error), Is.False);
            Assert.That(error, Does.Contain("coordinates"));

            object insecure = CreateProvider(
                "contest-provider",
                "http://tiles.example.test/{z}/{x}/{y}.mvt",
                5,
                14,
                "com.example.pixelroad");
            Assert.That(MappingApi.Property<bool>(insecure, "IsValid"), Is.False);
            Assert.That(MappingApi.Property<string>(insecure, "ValidationError"), Does.Contain("HTTPS"));
        }

        [TestCase("https://tiles.example.test/{z}/{x}/fixed.mvt")]
        [TestCase("https://tiles.example.test/{z}/{x}/{y}/{y}.mvt")]
        [TestCase("https://tiles.example.test/{z}/{x}/{y}/{token}.mvt")]
        [TestCase("https://user:secret@tiles.example.test/{z}/{x}/{y}.mvt")]
        public void Provider_RejectsAmbiguousOrUnsafeTemplates(string template)
        {
            object provider = CreateProvider("contest-provider", template, 0, 14, "PixelRoad");

            Assert.That(MappingApi.Property<bool>(provider, "IsValid"), Is.False);
            Assert.That(MappingApi.Property<string>(provider, "ValidationError"), Is.Not.Empty);
        }

        [Test]
        public void CacheHeaders_RespectRevalidationMaxAgeExpiresAndFallback()
        {
            const long now = 1_900_000_000L;
            Type providerType = MappingApi.Type("PixelRoad.Mapping.VectorTileProvider");

            Dictionary<string, string> maxAgeHeaders = new Dictionary<string, string>
            {
                { "cache-control", "public, max-age=120" },
                { "etag", "  \"tile-v2\"  " },
                { "LAST-MODIFIED", " Wed, 21 Oct 2015 07:28:00 GMT " },
                { "Expires", "Mon, 21 Oct 2030 07:28:00 GMT" }
            };
            object maxAge = MappingApi.CallStatic(
                providerType.FullName,
                "ParseResponseCacheHeaders",
                new[] { typeof(IDictionary<string, string>), typeof(long) },
                maxAgeHeaders,
                now);
            Assert.That(MappingApi.Field<long>(maxAge, "ExpiresUnixSeconds"), Is.EqualTo(now + 120L));
            Assert.That(MappingApi.Field<string>(maxAge, "ETag"), Is.EqualTo("\"tile-v2\""));
            Assert.That(MappingApi.Field<string>(maxAge, "LastModified"), Is.EqualTo("Wed, 21 Oct 2015 07:28:00 GMT"));

            long noCache = (long)MappingApi.CallStatic(
                providerType.FullName,
                "CalculateExpiryUnixSeconds",
                new[] { typeof(long), typeof(string), typeof(string) },
                now,
                "max-age=3600, no-cache",
                null);
            Assert.That(noCache, Is.EqualTo(now));

            long expiresUnix = new DateTimeOffset(2030, 10, 21, 7, 28, 0, TimeSpan.Zero).ToUnixTimeSeconds();
            long expires = (long)MappingApi.CallStatic(
                providerType.FullName,
                "CalculateExpiryUnixSeconds",
                new[] { typeof(long), typeof(string), typeof(string) },
                now,
                null,
                "Mon, 21 Oct 2030 07:28:00 GMT");
            Assert.That(expires, Is.EqualTo(expiresUnix));

            long fallback = (long)MappingApi.CallStatic(
                providerType.FullName,
                "CalculateExpiryUnixSeconds",
                new[] { typeof(long), typeof(string), typeof(string) },
                now,
                null,
                null);
            long defaultLifetime = (long)providerType
                .GetField("DefaultCacheLifetimeSeconds", BindingFlags.Public | BindingFlags.Static)
                .GetRawConstantValue();
            Assert.That(fallback, Is.EqualTo(now + defaultLifetime));
        }

        [Test]
        public void CacheHeaders_ExpiresZeroMeansImmediatelyExpired()
        {
            const long now = 1_900_000_000L;
            Dictionary<string, string> headers = new Dictionary<string, string>
            {
                { "Expires", "0" }
            };

            object metadata = MappingApi.CallStatic(
                "PixelRoad.Mapping.VectorTileProvider",
                "ParseResponseCacheHeaders",
                new[] { typeof(IDictionary<string, string>), typeof(long) },
                headers,
                now);

            Assert.That(MappingApi.Field<long>(metadata, "ExpiresUnixSeconds"), Is.EqualTo(now));
        }

        [Test]
        public void CacheHeaders_AgeReducesMaxAgeToRemainingLifetime()
        {
            const long now = 1_900_000_000L;
            Dictionary<string, string> headers = new Dictionary<string, string>
            {
                { "Cache-Control", "public, max-age=120" },
                { "Age", "45" }
            };

            object metadata = MappingApi.CallStatic(
                "PixelRoad.Mapping.VectorTileProvider",
                "ParseResponseCacheHeaders",
                new[] { typeof(IDictionary<string, string>), typeof(long) },
                headers,
                now);

            Assert.That(MappingApi.Field<long>(metadata, "ExpiresUnixSeconds"), Is.EqualTo(now + 75L));
        }

        private static object CreateProvider(
            string providerId,
            string template,
            int minimumZoom,
            int maximumZoom,
            string applicationIdentifier)
        {
            Type configType = MappingApi.Type("PixelRoad.Data.MapConfig");
            object config = Activator.CreateInstance(configType);
            configType.GetField("vectorTileProviderId").SetValue(config, providerId);
            configType.GetField("vectorTileUrlTemplate").SetValue(config, template);
            configType.GetField("vectorTileMinZoom").SetValue(config, minimumZoom);
            configType.GetField("vectorTileMaxZoom").SetValue(config, maximumZoom);

            Type providerType = MappingApi.Type("PixelRoad.Mapping.VectorTileProvider");
            return Activator.CreateInstance(providerType, config, applicationIdentifier);
        }

        private static bool TryBuildUrl(
            object provider,
            int zoom,
            int x,
            int y,
            out string url,
            out string error)
        {
            object[] arguments = { zoom, x, y, null, null };
            bool success = (bool)MappingApi.Call(
                provider,
                "TryBuildTileUrl",
                new[]
                {
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(string).MakeByRefType(),
                    typeof(string).MakeByRefType()
                },
                arguments);
            url = (string)arguments[3];
            error = (string)arguments[4];
            return success;
        }
    }

    public sealed class MvtDecoderTests
    {
        [Test]
        public void Decode_ParsesHandcraftedPointLineAndPolygon()
        {
            object tile = Decode(HandcraftedMvt.CompleteTile(includeLabelLayer: true));

            object land = FindLayer(tile, "land");
            object streets = FindLayer(tile, "streets");
            object labels = FindLayer(tile, "place_labels");
            Assert.That(land, Is.Not.Null);
            Assert.That(streets, Is.Not.Null);
            Assert.That(labels, Is.Not.Null);

            object polygon = FirstFeature(land);
            AssertGeometry(polygon, expectedType: 3, expectedPointCount: 4, expectedClosed: true);
            Assert.That(PropertyValue(polygon, "kind"), Is.EqualTo("residential"));

            object line = FirstFeature(streets);
            AssertGeometry(line, expectedType: 2, expectedPointCount: 3, expectedClosed: false);
            Assert.That(PropertyValue(line, "kind"), Is.EqualTo("primary"));

            object point = FirstFeature(labels);
            AssertGeometry(point, expectedType: 1, expectedPointCount: 1, expectedClosed: false);
            Assert.That(PropertyValue(point, "name"), Is.EqualTo("Contest Label"));

            object pointPath = First(MappingApi.Property<object>(point, "Paths"));
            object decodedPoint = First(MappingApi.Property<object>(pointPath, "Points"));
            Assert.That(MappingApi.Property<int>(decodedPoint, "X"), Is.EqualTo(2000));
            Assert.That(MappingApi.Property<int>(decodedPoint, "Y"), Is.EqualTo(1800));
        }

        [Test]
        public void Decode_AllowedLayerFilterDropsLabelsBeforeMeshWork()
        {
            object filter = MappingApi.CallStatic(
                "PixelRoad.Mapping.VectorTileMeshBuilder",
                "CreateSupportedLayerFilter",
                Type.EmptyTypes);
            ISet<string> supported = (ISet<string>)filter;
            Assert.That(supported, Does.Contain("land"));
            Assert.That(supported, Does.Contain("streets"));
            Assert.That(supported, Does.Not.Contain("place_labels"));
            Assert.That(supported.Any(name => name.IndexOf("label", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);

            object filteredTile = Decode(HandcraftedMvt.CompleteTile(includeLabelLayer: true), supported);
            List<object> layers = Items(MappingApi.Property<object>(filteredTile, "Layers"));
            Assert.That(layers.Select(layer => MappingApi.Property<string>(layer, "Name")), Is.EquivalentTo(new[] { "land", "streets" }));
            Assert.That(FindLayer(filteredTile, "place_labels"), Is.Null);
        }

        [Test]
        public void Decode_RejectsTruncatedAndInvalidGeometryWithoutPartialResult()
        {
            byte[] valid = HandcraftedMvt.CompleteTile(includeLabelLayer: true);
            byte[] truncated = valid.Take(valid.Length - 1).ToArray();
            AssertDecodeFormatException(truncated);

            byte[] missingCoordinate = HandcraftedMvt.TileWithRawFeature(
                "streets",
                geometryType: 2,
                geometry: new uint[] { 9, 0 });
            AssertDecodeFormatException(missingCoordinate);

            byte[] unknownCommand = HandcraftedMvt.TileWithRawFeature(
                "place_labels",
                geometryType: 1,
                geometry: new uint[] { 8 });
            AssertDecodeFormatException(unknownCommand);

            byte[] oddTags = HandcraftedMvt.TileWithRawFeature(
                "land",
                geometryType: 3,
                geometry: HandcraftedMvt.PolygonGeometry,
                tags: new uint[] { 0 });
            AssertDecodeFormatException(oddTags);
        }

        private static object Decode(byte[] data, ISet<string> allowedLayers = null)
        {
            return MappingApi.CallStatic(
                "PixelRoad.Mapping.MvtDecoder",
                "Decode",
                new[] { typeof(byte[]), typeof(ISet<string>) },
                data,
                allowedLayers);
        }

        internal static object FindLayer(object tile, string name)
        {
            return MappingApi.Call(tile, "FindLayer", new[] { typeof(string) }, name);
        }

        internal static object FirstFeature(object layer)
        {
            return First(MappingApi.Property<object>(layer, "Features"));
        }

        private static void AssertGeometry(object feature, int expectedType, int expectedPointCount, bool expectedClosed)
        {
            object geometryType = MappingApi.Property<object>(feature, "GeometryType");
            Assert.That(Convert.ToInt32(geometryType), Is.EqualTo(expectedType));
            object path = First(MappingApi.Property<object>(feature, "Paths"));
            Assert.That(MappingApi.Property<bool>(path, "IsClosed"), Is.EqualTo(expectedClosed));
            Assert.That(Items(MappingApi.Property<object>(path, "Points")), Has.Count.EqualTo(expectedPointCount));
        }

        private static object PropertyValue(object feature, string key)
        {
            IDictionary properties = (IDictionary)MappingApi.Property<object>(feature, "Properties");
            return properties[key];
        }

        private static object First(object enumerable)
        {
            return Items(enumerable).First();
        }

        private static List<object> Items(object enumerable)
        {
            return ((IEnumerable)enumerable).Cast<object>().ToList();
        }

        private static void AssertDecodeFormatException(byte[] data)
        {
            TargetInvocationException wrapper = Assert.Throws<TargetInvocationException>(() => Decode(data));
            Assert.That(wrapper.InnerException, Is.TypeOf<FormatException>());
        }
    }

    public sealed class VectorTileMeshBuilderTests
    {
        [Test]
        public void Build_IgnoresLabelsAndProducesValidLandAndStreetMesh()
        {
            object fullTile = Decode(HandcraftedMvt.CompleteTile(includeLabelLayer: true));
            object noLabelsTile = Decode(HandcraftedMvt.CompleteTile(includeLabelLayer: false));
            object landOnlyTile = Decode(HandcraftedMvt.LandOnlyTile());

            object fullMesh = Build(fullTile);
            object noLabelsMesh = Build(noLabelsTile);
            object landOnlyMesh = Build(landOnlyTile);

            Vector3[] vertices = MappingApi.Field<Vector3[]>(fullMesh, "Vertices");
            int[] triangles = MappingApi.Field<int[]>(fullMesh, "Triangles");
            Color32[] colors = MappingApi.Field<Color32[]>(fullMesh, "Colors");
            Vector2[] tileUvs = MappingApi.Field<Vector2[]>(fullMesh, "TileUvs");

            Assert.That(MappingApi.Property<bool>(fullMesh, "IsEmpty"), Is.False);
            Assert.That(vertices.Length, Is.GreaterThan(0));
            Assert.That(triangles.Length, Is.GreaterThan(0));
            Assert.That(triangles.Length % 3, Is.Zero);
            Assert.That(colors, Has.Length.EqualTo(vertices.Length));
            Assert.That(tileUvs, Has.Length.EqualTo(vertices.Length));
            Assert.That(triangles.All(index => index >= 0 && index < vertices.Length), Is.True);
            Assert.That(vertices.All(IsFinite), Is.True);
            Assert.That(tileUvs.All(IsFinite), Is.True);
            Assert.That(tileUvs.All(uv => uv.x >= -0.01f && uv.x <= 1.01f && uv.y >= -0.01f && uv.y <= 1.01f), Is.True);

            int noLabelVertexCount = MappingApi.Field<Vector3[]>(noLabelsMesh, "Vertices").Length;
            int noLabelTriangleCount = MappingApi.Field<int[]>(noLabelsMesh, "Triangles").Length;
            Assert.That(vertices.Length, Is.EqualTo(noLabelVertexCount), "A label layer must not contribute mesh vertices.");
            Assert.That(triangles.Length, Is.EqualTo(noLabelTriangleCount), "A label layer must not contribute mesh indices.");

            int landVertexCount = MappingApi.Field<Vector3[]>(landOnlyMesh, "Vertices").Length;
            Assert.That(landVertexCount, Is.GreaterThan(0));
            Assert.That(vertices.Length, Is.GreaterThan(landVertexCount), "The streets layer must add visible geometry beyond the land polygon.");
        }

        private static object Decode(byte[] data)
        {
            return MappingApi.CallStatic(
                "PixelRoad.Mapping.MvtDecoder",
                "Decode",
                new[] { typeof(byte[]), typeof(ISet<string>) },
                data,
                null);
        }

        private static object Build(object tile)
        {
            return MappingApi.CallStatic(
                "PixelRoad.Mapping.VectorTileMeshBuilder",
                "Build",
                new[] { tile.GetType() },
                tile);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    internal static class HandcraftedMvt
    {
        public static readonly uint[] PolygonGeometry =
        {
            Command(1, 1), ZigZag(512), ZigZag(512),
            Command(2, 3),
            ZigZag(3072), ZigZag(0),
            ZigZag(0), ZigZag(3072),
            ZigZag(-3072), ZigZag(0),
            Command(7, 1)
        };

        private static readonly uint[] StreetGeometry =
        {
            Command(1, 1), ZigZag(512), ZigZag(2048),
            Command(2, 2),
            ZigZag(1536), ZigZag(-1024),
            ZigZag(1536), ZigZag(1024)
        };

        private static readonly uint[] PointGeometry =
        {
            Command(1, 1), ZigZag(2000), ZigZag(1800)
        };

        public static byte[] CompleteTile(bool includeLabelLayer)
        {
            List<byte[]> layers = new List<byte[]>
            {
                Layer(
                    "land",
                    Feature(101, 3, PolygonGeometry, new uint[] { 0, 0 }),
                    "kind",
                    "residential"),
                Layer(
                    "streets",
                    Feature(202, 2, StreetGeometry, new uint[] { 0, 0 }),
                    "kind",
                    "primary")
            };
            if (includeLabelLayer)
            {
                layers.Add(Layer(
                    "place_labels",
                    Feature(303, 1, PointGeometry, new uint[] { 0, 0 }),
                    "name",
                    "Contest Label"));
            }

            return Tile(layers.ToArray());
        }

        public static byte[] LandOnlyTile()
        {
            return Tile(Layer(
                "land",
                Feature(101, 3, PolygonGeometry, new uint[] { 0, 0 }),
                "kind",
                "residential"));
        }

        public static byte[] TileWithRawFeature(
            string layerName,
            uint geometryType,
            uint[] geometry,
            uint[] tags = null)
        {
            return Tile(Layer(
                layerName,
                Feature(1, geometryType, geometry, tags),
                tags == null ? null : "kind",
                tags == null ? null : "test"));
        }

        private static byte[] Tile(params byte[][] layers)
        {
            List<byte> tile = new List<byte>();
            foreach (byte[] layer in layers)
            {
                AddLengthDelimited(tile, 3, layer);
            }

            return tile.ToArray();
        }

        private static byte[] Layer(
            string name,
            byte[] feature,
            string key = null,
            string value = null)
        {
            List<byte> layer = new List<byte>();
            AddString(layer, 1, name);
            AddLengthDelimited(layer, 2, feature);
            if (key != null)
            {
                AddString(layer, 3, key);
                List<byte> valueMessage = new List<byte>();
                AddString(valueMessage, 1, value);
                AddLengthDelimited(layer, 4, valueMessage.ToArray());
            }

            AddVarintField(layer, 5, 4096);
            AddVarintField(layer, 15, 2);
            return layer.ToArray();
        }

        private static byte[] Feature(ulong id, uint geometryType, uint[] geometry, uint[] tags)
        {
            List<byte> feature = new List<byte>();
            AddVarintField(feature, 1, id);
            if (tags != null)
            {
                AddPackedUInt32(feature, 2, tags);
            }

            AddVarintField(feature, 3, geometryType);
            AddPackedUInt32(feature, 4, geometry);
            return feature.ToArray();
        }

        private static void AddString(List<byte> destination, int fieldNumber, string value)
        {
            AddLengthDelimited(destination, fieldNumber, System.Text.Encoding.UTF8.GetBytes(value));
        }

        private static void AddPackedUInt32(List<byte> destination, int fieldNumber, IEnumerable<uint> values)
        {
            List<byte> packed = new List<byte>();
            foreach (uint value in values)
            {
                AddVarint(packed, value);
            }

            AddLengthDelimited(destination, fieldNumber, packed.ToArray());
        }

        private static void AddLengthDelimited(List<byte> destination, int fieldNumber, byte[] payload)
        {
            AddVarint(destination, (ulong)((fieldNumber << 3) | 2));
            AddVarint(destination, (ulong)payload.Length);
            destination.AddRange(payload);
        }

        private static void AddVarintField(List<byte> destination, int fieldNumber, ulong value)
        {
            AddVarint(destination, (ulong)(fieldNumber << 3));
            AddVarint(destination, value);
        }

        private static void AddVarint(List<byte> destination, ulong value)
        {
            do
            {
                byte next = (byte)(value & 0x7fUL);
                value >>= 7;
                if (value != 0UL)
                {
                    next |= 0x80;
                }

                destination.Add(next);
            }
            while (value != 0UL);
        }

        private static uint Command(int id, int repeatCount)
        {
            return (uint)((repeatCount << 3) | id);
        }

        private static uint ZigZag(int value)
        {
            return unchecked((uint)((value << 1) ^ (value >> 31)));
        }
    }
}
