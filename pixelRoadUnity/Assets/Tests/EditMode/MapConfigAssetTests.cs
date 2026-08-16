using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace PixelRoad.Tests.EditMode
{
    /// <summary>
    /// 런타임이 Resources/PixelRoad/MapConfig.asset 을 먼저 읽으므로,
    /// 에셋이 사라지거나 값이 망가지면 앱이 시작하지 못한다. 그 계약을 지킨다.
    ///
    /// 이 테스트 어셈블리는 Assembly-CSharp을 참조하지 않아 다른 테스트와 같이 리플렉션으로 접근한다.
    /// </summary>
    public sealed class MapConfigAssetTests
    {
        [Test]
        public void MapConfigAsset_LoadsFromResourcesAndConvertsToRuntimeConfig()
        {
            ScriptableObject asset = Resources.Load<ScriptableObject>("PixelRoad/MapConfig");
            Assert.That(asset, Is.Not.Null,
                "PixelRoadApp reads Resources/PixelRoad/MapConfig first; the asset must exist.");
            Assert.That(asset.GetType().FullName, Is.EqualTo("PixelRoad.Data.MapConfigAsset"));

            MethodInfo toMapConfig = asset.GetType().GetMethod("ToMapConfig");
            Assert.That(toMapConfig, Is.Not.Null, "MapConfigAsset must expose ToMapConfig().");
            object config = toMapConfig.Invoke(asset, null);
            Assert.That(config, Is.Not.Null);

            object bounds = ReadField<object>(config, "bounds");
            Assert.That(bounds, Is.Not.Null);
            MethodInfo isValid = bounds.GetType().GetMethod("IsValid");
            Assert.That((bool)isValid.Invoke(bounds, null), Is.True,
                "Bounds must satisfy north > south and east > west or the app refuses to start.");

            string landmarkPath = ReadField<string>(config, "landmarksJsonResourcePath");
            Assert.That(landmarkPath, Is.Not.Empty);
            Assert.That(Resources.Load<TextAsset>(landmarkPath), Is.Not.Null,
                "The landmark JSON path must point at an existing Resources asset.");

            Assert.That(ReadField<float>(config, "defaultUnlockRadiusMeters"), Is.GreaterThan(0f));
            Assert.That(ReadField<int>(config, "pixelBlockSize"), Is.GreaterThan(0));

            int markerSize = ReadField<int>(config, "spotMarkerPixelSize");
            Assert.That(markerSize, Is.GreaterThan(0));
            Assert.That(ReadField<int>(config, "userMarkerPixelSize"), Is.GreaterThan(0));
            Assert.That(ReadField<int>(config, "markerTapMinimumPixelSize"), Is.GreaterThanOrEqualTo(markerSize));

            float minimumZoom = ReadField<float>(config, "minimumMapZoom");
            float maximumZoom = ReadField<float>(config, "maximumMapZoom");
            Assert.That(minimumZoom, Is.LessThan(maximumZoom));
            Assert.That(ReadField<float>(config, "initialMapZoom"), Is.InRange(minimumZoom, maximumZoom));
            Assert.That(
                ReadField<int>(config, "vectorTileMinZoom"),
                Is.LessThanOrEqualTo(ReadField<int>(config, "vectorTileMaxZoom")));
            Assert.That(ReadField<string>(config, "vectorTileUrlTemplate"), Does.Contain("{z}"));
            Assert.That(ReadField<string>(config, "mapAttribution"), Is.Not.Empty,
                "The tile provider licence requires an attribution line.");
        }

        private static T ReadField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, "Missing MapConfig field: " + fieldName);
            object value = field.GetValue(target);
            Assert.That(value, Is.AssignableTo<T>());
            return (T)value;
        }
    }
}
