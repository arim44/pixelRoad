using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace PixelRoad.Tests.EditMode
{
    public sealed class SlippyMapProjectionTests
    {
        [TestCase(37.5665, 126.9780)]
        [TestCase(-33.8688, 151.2093)]
        [TestCase(0.0, -179.9)]
        [TestCase(85.0, 12.5)]
        public void LatitudeLongitude_RoundTrips(double latitude, double longitude)
        {
            object world = MappingApi.CallStatic(
                "PixelRoad.Mapping.SlippyMapProjection",
                "LatLonToWorld",
                new[] { typeof(double), typeof(double) },
                latitude,
                longitude);

            object[] arguments = { world, 0.0, 0.0 };
            MappingApi.CallStatic(
                "PixelRoad.Mapping.SlippyMapProjection",
                "WorldToLatLon",
                new[] { world.GetType(), typeof(double).MakeByRefType(), typeof(double).MakeByRefType() },
                arguments);

            Assert.That((double)arguments[1], Is.EqualTo(latitude).Within(1e-7));
            Assert.That(WrappedLongitudeDelta((double)arguments[2], longitude), Is.EqualTo(0.0).Within(1e-7));
        }

        [Test]
        public void Projection_ClampsLatitudeAndWrapsLongitude()
        {
            object world = MappingApi.CallStatic(
                "PixelRoad.Mapping.SlippyMapProjection",
                "LatLonToWorld",
                new[] { typeof(double), typeof(double) },
                90.0,
                540.0);

            Assert.That(MappingApi.Field<double>(world, "X"), Is.EqualTo(0.0).Within(1e-12));
            Assert.That(MappingApi.Field<double>(world, "Y"), Is.EqualTo(0.0).Within(1e-9));

            double negativeWrapped = (double)MappingApi.CallStatic(
                "PixelRoad.Mapping.SlippyMapProjection",
                "WrapWorldX",
                new[] { typeof(double) },
                -0.25);
            double positiveWrapped = (double)MappingApi.CallStatic(
                "PixelRoad.Mapping.SlippyMapProjection",
                "WrapWorldX",
                new[] { typeof(double) },
                1.25);

            Assert.That(negativeWrapped, Is.EqualTo(0.75).Within(1e-12));
            Assert.That(positiveWrapped, Is.EqualTo(0.25).Within(1e-12));
        }

        private static double WrappedLongitudeDelta(double left, double right)
        {
            double delta = (left - right) % 360.0;
            if (delta > 180.0)
            {
                delta -= 360.0;
            }
            else if (delta < -180.0)
            {
                delta += 360.0;
            }

            return delta;
        }
    }

    public sealed class MapViewStateTests
    {
        [Test]
        public void Pan_ChangesCenterByScreenDeltaAtCurrentScale()
        {
            object view = MappingApi.CreateMapView(37.5, 127.0, 10f, 5f, 18f);
            double beforeX = MappingApi.Property<double>(view, "CenterX");
            double beforeY = MappingApi.Property<double>(view, "CenterY");
            double pixelsPerWorld = MappingApi.Property<double>(view, "PixelsPerWorld");
            Vector2 delta = new Vector2(128f, -64f);

            MappingApi.Call(view, "Pan", new[] { typeof(Vector2) }, delta);

            double expectedX = MappingApi.WrapWorldX(beforeX - delta.x / pixelsPerWorld);
            double expectedY = Math.Max(0.0, Math.Min(1.0, beforeY + delta.y / pixelsPerWorld));
            Assert.That(MappingApi.Property<double>(view, "CenterX"), Is.EqualTo(expectedX).Within(1e-12));
            Assert.That(MappingApi.Property<double>(view, "CenterY"), Is.EqualTo(expectedY).Within(1e-12));
        }

        [Test]
        public void ZoomAt_PreservesWorldCoordinateUnderAnchor()
        {
            object view = MappingApi.CreateMapView(37.5, 127.0, 11f, 5f, 18f);
            Vector2 anchor = new Vector2(173.25f, -91.5f);
            object before = MappingApi.Call(view, "LocalToWorld", new[] { typeof(Vector2) }, anchor);

            MappingApi.Call(view, "ZoomAt", new[] { typeof(float), typeof(Vector2) }, 2.75f, anchor);

            object after = MappingApi.Call(view, "LocalToWorld", new[] { typeof(Vector2) }, anchor);
            Assert.That(MappingApi.Field<double>(after, "X"), Is.EqualTo(MappingApi.Field<double>(before, "X")).Within(1e-10));
            Assert.That(MappingApi.Field<double>(after, "Y"), Is.EqualTo(MappingApi.Field<double>(before, "Y")).Within(1e-10));
            Assert.That(MappingApi.Property<float>(view, "Zoom"), Is.GreaterThan(11f));
        }

        [Test]
        public void ZoomAt_RespectsConfiguredBounds()
        {
            object view = MappingApi.CreateMapView(0.0, 0.0, 10f, 8f, 12f);

            MappingApi.Call(view, "ZoomAt", new[] { typeof(float), typeof(Vector2) }, 1024f, Vector2.zero);
            Assert.That(MappingApi.Property<float>(view, "Zoom"), Is.EqualTo(12f));

            MappingApi.Call(view, "ZoomAt", new[] { typeof(float), typeof(Vector2) }, 1f / 1024f, Vector2.zero);
            Assert.That(MappingApi.Property<float>(view, "Zoom"), Is.EqualTo(8f));
        }
    }

    public sealed class TileCoverageTests
    {
        [Test]
        public void Calculate_ReturnsOnlyViewportIntersectionsAndNearestFirst()
        {
            object view = MappingApi.CreateMapView(0.0, 0.0, 2f, 0f, 20f);
            MappingApi.SetWorldCenter(view, 0.6, 0.6);
            Vector2 viewport = new Vector2(500f, 500f);

            List<ReflectedTile> tiles = MappingApi.CalculateTiles(view, viewport, 2, 2);

            Assert.That(tiles, Has.Count.EqualTo(9));
            Assert.That(tiles.Select(tile => tile.Key).Distinct().Count(), Is.EqualTo(tiles.Count));
            Assert.That(tiles.Select(tile => tile.DisplayX), Is.EquivalentTo(new[] { 1, 1, 1, 2, 2, 2, 3, 3, 3 }));
            Assert.That(tiles.Select(tile => tile.DisplayY), Is.EquivalentTo(new[] { 1, 1, 1, 2, 2, 2, 3, 3, 3 }));

            for (int index = 1; index < tiles.Count; index++)
            {
                Assert.That(tiles[index].Priority, Is.GreaterThanOrEqualTo(tiles[index - 1].Priority));
            }

            const double centerTile = 2.4;
            const double tilePixels = 256.0;
            foreach (ReflectedTile tile in tiles)
            {
                double centerX = (tile.DisplayX + 0.5 - centerTile) * tilePixels;
                double centerY = (tile.DisplayY + 0.5 - centerTile) * tilePixels;
                Assert.That(centerX - tilePixels / 2.0, Is.LessThan(viewport.x / 2.0));
                Assert.That(centerX + tilePixels / 2.0, Is.GreaterThan(-viewport.x / 2.0));
                Assert.That(centerY - tilePixels / 2.0, Is.LessThan(viewport.y / 2.0));
                Assert.That(centerY + tilePixels / 2.0, Is.GreaterThan(-viewport.y / 2.0));
            }
        }

        [Test]
        public void Calculate_WrapsRequestXAtDateLineButKeepsDisplayCoordinates()
        {
            object view = MappingApi.CreateMapView(0.0, 0.0, 2f, 0f, 20f);
            MappingApi.SetWorldCenter(view, 0.999, 0.375);

            List<ReflectedTile> tiles = MappingApi.CalculateTiles(view, new Vector2(400f, 100f), 2, 2);

            Assert.That(tiles, Has.Count.EqualTo(2));
            Assert.That(tiles.Select(tile => tile.DisplayX), Is.EquivalentTo(new[] { 3, 4 }));
            Assert.That(tiles.Select(tile => tile.X), Is.EquivalentTo(new[] { 3, 0 }));
            Assert.That(tiles.All(tile => tile.Y == 1 && tile.Zoom == 2), Is.True);
        }

        [Test]
        public void Calculate_ClampsRowsAtMercatorNorthEdge()
        {
            object view = MappingApi.CreateMapView(0.0, 0.0, 2f, 0f, 20f);
            MappingApi.SetWorldCenter(view, 0.5, 0.0);

            List<ReflectedTile> tiles = MappingApi.CalculateTiles(view, new Vector2(100f, 600f), 2, 2);

            Assert.That(tiles, Is.Not.Empty);
            Assert.That(tiles.All(tile => tile.Y >= 0 && tile.Y < 4), Is.True);
            Assert.That(tiles.Select(tile => tile.Y).Distinct(), Is.EquivalentTo(new[] { 0, 1 }));
        }
    }

    public sealed class TileDiskCacheTests
    {
        [Test]
        public void ClearExpiredOrTrim_RemovesEntryAtDeterministicExpiryTime()
        {
            using (CacheFixture cache = new CacheFixture(1))
            {
                object key = MappingApi.CreateTileKey(13, 6987, 3175);
                object entry = MappingApi.CreateCacheEntry(new byte[] { 1, 3, 3, 7 }, long.MaxValue, "test-etag", "test-date");
                MappingApi.CacheWrite(cache.Instance, key, entry);
                Assert.That(MappingApi.CacheTryRead(cache.Instance, key, out object read), Is.True);
                Assert.That(MappingApi.Field<byte[]>(read, "Data"), Is.EqualTo(new byte[] { 1, 3, 3, 7 }));

                MappingApi.Call(cache.Instance, "ClearExpiredOrTrim", new[] { typeof(long) }, long.MaxValue);

                Assert.That(MappingApi.CacheTryRead(cache.Instance, key, out _), Is.False);
            }
        }

        [Test]
        public void Write_TrimsAnEntryThatExceedsConfiguredByteBudget()
        {
            using (CacheFixture cache = new CacheFixture(1))
            {
                object key = MappingApi.CreateTileKey(13, 6988, 3175);
                byte[] oversizedPayload = new byte[1024 * 1024 + 128];
                object entry = MappingApi.CreateCacheEntry(oversizedPayload, long.MaxValue, null, null);

                MappingApi.CacheWrite(cache.Instance, key, entry);

                Assert.That(MappingApi.CacheTryRead(cache.Instance, key, out _), Is.False);
            }
        }

        private sealed class CacheFixture : IDisposable
        {
            public readonly object Instance;
            private readonly string directory;

            public CacheFixture(int maxMegabytes)
            {
                Type cacheType = MappingApi.Type("PixelRoad.Mapping.TileDiskCache");
                Instance = Activator.CreateInstance(
                    cacheType,
                    "editmode-test-" + Guid.NewGuid().ToString("N"),
                    maxMegabytes,
                    true);
                directory = (string)cacheType
                    .GetField("cacheDirectory", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(Instance);
            }

            public void Dispose()
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }
    }

    internal readonly struct ReflectedTile
    {
        public readonly string Key;
        public readonly int Zoom;
        public readonly int X;
        public readonly int Y;
        public readonly int DisplayX;
        public readonly int DisplayY;
        public readonly double Priority;

        public ReflectedTile(object tile)
        {
            object key = MappingApi.Field<object>(tile, "Key");
            Key = key.ToString();
            Zoom = MappingApi.Field<int>(key, "Zoom");
            X = MappingApi.Field<int>(key, "X");
            Y = MappingApi.Field<int>(key, "Y");
            DisplayX = MappingApi.Field<int>(tile, "DisplayX");
            DisplayY = MappingApi.Field<int>(tile, "DisplayY");
            Priority = MappingApi.Field<double>(tile, "Priority");
        }
    }

    internal static class MappingApi
    {
        public static Type Type(string fullName)
        {
            Type type = AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, "Could not find runtime type " + fullName + ". Ensure Assembly-CSharp compiled successfully.");
            return type;
        }

        public static object CreateMapView(double latitude, double longitude, float zoom, float minimumZoom, float maximumZoom)
        {
            return Activator.CreateInstance(Type("PixelRoad.Mapping.MapViewState"), latitude, longitude, zoom, minimumZoom, maximumZoom);
        }

        public static object CreateTileKey(int zoom, int x, int y)
        {
            return Activator.CreateInstance(Type("PixelRoad.Mapping.TileKey"), zoom, x, y);
        }

        public static object CreateCacheEntry(byte[] data, long expiresUnix, string etag, string lastModified)
        {
            object entry = Activator.CreateInstance(Type("PixelRoad.Mapping.TileCacheEntry"));
            entry.GetType().GetField("Data").SetValue(entry, data);
            entry.GetType().GetField("ExpiresUnix").SetValue(entry, expiresUnix);
            entry.GetType().GetField("ETag").SetValue(entry, etag);
            entry.GetType().GetField("LastModified").SetValue(entry, lastModified);
            return entry;
        }

        public static void SetWorldCenter(object view, double x, double y)
        {
            Type worldType = Type("PixelRoad.Mapping.WorldMercatorPoint");
            object world = Activator.CreateInstance(worldType, x, y);
            Call(view, "SetWorldCenter", new[] { worldType }, world);
        }

        public static double WrapWorldX(double value)
        {
            return (double)CallStatic(
                "PixelRoad.Mapping.SlippyMapProjection",
                "WrapWorldX",
                new[] { typeof(double) },
                value);
        }

        public static List<ReflectedTile> CalculateTiles(object view, Vector2 viewport, int minimumZoom, int maximumZoom)
        {
            object result = CallStatic(
                "PixelRoad.Mapping.TileCoverage",
                "Calculate",
                new[] { view.GetType(), typeof(Vector2), typeof(int), typeof(int) },
                view,
                viewport,
                minimumZoom,
                maximumZoom);
            return ((IEnumerable)result).Cast<object>().Select(tile => new ReflectedTile(tile)).ToList();
        }

        public static void CacheWrite(object cache, object key, object entry)
        {
            Call(cache, "Write", new[] { key.GetType(), entry.GetType() }, key, entry);
        }

        public static bool CacheTryRead(object cache, object key, out object entry)
        {
            Type entryType = Type("PixelRoad.Mapping.TileCacheEntry");
            object[] arguments = { key, null };
            bool found = (bool)Call(cache, "TryRead", new[] { key.GetType(), entryType.MakeByRefType() }, arguments);
            entry = arguments[1];
            return found;
        }

        public static object CallStatic(string typeName, string methodName, Type[] parameterTypes, params object[] arguments)
        {
            MethodInfo method = Type(typeName).GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static,
                null,
                parameterTypes,
                null);
            Assert.That(method, Is.Not.Null, "Could not find method " + typeName + "." + methodName + ".");
            return method.Invoke(null, arguments);
        }

        public static object Call(object target, string methodName, Type[] parameterTypes, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Instance,
                null,
                parameterTypes,
                null);
            Assert.That(method, Is.Not.Null, "Could not find method " + target.GetType().FullName + "." + methodName + ".");
            return method.Invoke(target, arguments);
        }

        public static T Property<T>(object target, string name)
        {
            return (T)target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance).GetValue(target);
        }

        public static T Field<T>(object target, string name)
        {
            return (T)target.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance).GetValue(target);
        }
    }
}
