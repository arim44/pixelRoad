using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace PixelRoad.Tests.EditMode
{
    public sealed class UiPrefabTests
    {
        [Test]
        public void UiPrefabs_HaveAllDesignerBindingsAndMarkerContract()
        {
            GameObject root = Resources.Load<GameObject>("PixelRoad/UI/PixelRoadUIRoot");
            Assert.That(root, Is.Not.Null);
            Component bindings = FindComponent(root, "PixelRoad.UI.PixelRoadUiBindings");
            Assert.That(bindings, Is.Not.Null);
            MethodInfo validate = bindings.GetType().GetMethod("ValidateReferences");
            Assert.That(validate, Is.Not.Null);
            Assert.DoesNotThrow(() => validate.Invoke(bindings, null));

            Assert.That(ReadProperty<RectTransform>(bindings, "MapViewport").name, Is.EqualTo("MapViewport"));
            Assert.That(ReadProperty<RectTransform>(bindings, "MarkerRoot").name, Is.EqualTo("MapMarkerOverlay"));
            Assert.That(ReadProperty<RectTransform>(bindings, "CodexContent").name, Is.EqualTo("Content"));
            Assert.That(ReadProperty<GameObject>(bindings, "CodexPanel").activeSelf, Is.False);
            Assert.That(FindChild(root.transform, "ZoomControls"), Is.Null);
            Assert.That(FindChild(root.transform, "ZoomIn"), Is.Null);
            Assert.That(FindChild(root.transform, "ZoomOut"), Is.Null);

            Component marker = ReadProperty<Component>(bindings, "LandmarkMarkerPrefab");
            Assert.That(ReadProperty<Image>(marker, "Icon"), Is.Not.Null);
            Assert.That(ReadProperty<Component>(marker, "TapTarget"), Is.Not.Null);
            Assert.That(marker.GetComponent<Button>(), Is.Null,
                "LandmarkMarker must use MapMarkerTapTarget instead of Button.");

            Component card = ReadProperty<Component>(bindings, "LandmarkCardPrefab");
            Assert.That(ReadProperty<Button>(card, "Button"), Is.Not.Null);
            Assert.That(ReadProperty<Image>(card, "Icon"), Is.Not.Null);
            Assert.That(ReadProperty<Component>(card, "NameText"), Is.Not.Null);
            Assert.That(ReadProperty<Component>(card, "CategoryText"), Is.Not.Null);
            Assert.That(ReadProperty<Component>(card, "DescriptionText"), Is.Not.Null);
        }

        private static Component FindComponent(GameObject root, string fullTypeName)
        {
            Component[] components = root.GetComponents<Component>();
            for (int index = 0; index < components.Length; index++)
            {
                if (components[index] != null && components[index].GetType().FullName == fullTypeName)
                {
                    return components[index];
                }
            }

            return null;
        }

        private static Transform FindChild(Transform root, string objectName)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (transforms[index] != null && transforms[index].name == objectName)
                {
                    return transforms[index];
                }
            }

            return null;
        }

        private static T ReadProperty<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null);
            object value = property.GetValue(target);
            Assert.That(value, Is.AssignableTo<T>());
            return (T)value;
        }
    }
}
