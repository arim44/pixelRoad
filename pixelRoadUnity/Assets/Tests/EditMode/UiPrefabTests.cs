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

            // 런타임은 Canvas·EventSystem을 만들지 않는다. 프리팹이 직접 들고 있어야 한다.
            Assert.That(root.GetComponent<Canvas>(), Is.Not.Null,
                "PixelRoadUIRoot must own its Canvas so the runtime never builds one.");
            Assert.That(root.GetComponent<CanvasScaler>(), Is.Not.Null);
            Assert.That(root.GetComponent<GraphicRaycaster>(), Is.Not.Null);
            Assert.That(ReadProperty<Component>(bindings, "EventSystem"), Is.Not.Null,
                "PixelRoadUIRoot must own its EventSystem so the runtime never builds one.");

            Assert.That(ReadProperty<RectTransform>(bindings, "MapViewport").name, Is.EqualTo("MapViewport"));
            Assert.That(ReadProperty<RectTransform>(bindings, "MarkerRoot").name, Is.EqualTo("MapMarkerOverlay"));
            Component codex = ReadProperty<Component>(bindings, "CodexView");
            Assert.That(ReadProperty<GameObject>(codex, "Root").activeSelf, Is.False);
            Assert.That(ReadProperty<Button>(bindings, "RecenterButton").gameObject.activeSelf, Is.False,
                "The recenter button must stay hidden until the user cancels follow by dragging.");
            Assert.That(FindChild(root.transform, "ZoomControls"), Is.Null);
            Assert.That(FindChild(root.transform, "ZoomIn"), Is.Null);
            Assert.That(FindChild(root.transform, "ZoomOut"), Is.Null);

            // 아이콘을 못 찾은 랜드마크는 프리팹에 박힌 기본 스프라이트로 그려진다.
            // 예전처럼 런타임에서 도형 텍스처를 만들지 않기 때문에 여기가 비면 흰 사각형이 된다.
            Assert.That(ReadProperty<Image>(bindings, "UserMarker").sprite, Is.Not.Null,
                "UserMarker must carry its sprite in the prefab.");

            Component marker = ReadProperty<Component>(bindings, "LandmarkMarkerPrefab");
            Assert.That(ReadProperty<Image>(marker, "Icon"), Is.Not.Null);
            Assert.That(ReadProperty<Image>(marker, "Icon").sprite, Is.Not.Null,
                "LandmarkMarker needs a fallback sprite for landmarks without a thumbnail icon.");
            Assert.That(ReadProperty<Component>(marker, "TapTarget"), Is.Not.Null);
            Assert.That(marker.GetComponent<Button>(), Is.Null,
                "LandmarkMarker must use MapMarkerTapTarget instead of Button.");

            // 도감 카드와 필터 칩은 개수가 데이터에 따라 변해서 CodexView가 들고 있는 프리팹으로 만든다.
            Component card = ReadProperty<Component>(codex, "CardPrefab");
            Assert.That(ReadProperty<Button>(card, "Button"), Is.Not.Null);
            Assert.That(ReadProperty<Image>(card, "Icon").sprite, Is.Not.Null,
                "LandmarkCodexCard needs a fallback sprite for landmarks without a thumbnail icon.");
            Assert.That(ReadProperty<Component>(card, "NameText"), Is.Not.Null);
            Assert.That(ReadProperty<Component>(card, "BadgeText"), Is.Not.Null);
            Assert.That(ReadProperty<Component>(card, "DescriptionText"), Is.Not.Null);
            Assert.That(ReadProperty<Image>(card, "LockIcon"), Is.Not.Null);

            Assert.That(ReadProperty<Component>(codex, "FilterChipPrefab"), Is.Not.Null);

            // 수집률 칸은 프리팹에 고정 개수로 있어야 한다. 런타임이 만들지 않는다.
            Assert.That(ReadProperty<RectTransform>(codex, "RateSegmentRow").childCount, Is.GreaterThan(0),
                "The collection-rate bar must ship its segments in the prefab.");

            Component detail = ReadProperty<Component>(codex, "Detail");
            Assert.That(ReadProperty<GameObject>(detail, "Root").activeSelf, Is.False,
                "The card detail overlay must start hidden.");

            // 상세 팝업이 도감 패널 안에 있으면 도감이 꺼진 지도 화면에서 `카드 보기`를 눌러도 아무것도 뜨지 않는다.
            Assert.That(detail.transform.parent, Is.EqualTo(root.transform),
                "The card detail overlay must sit directly under the canvas, not inside the codex panel.");
            Component codexRoot = ReadProperty<GameObject>(codex, "Root").transform;
            Assert.That(
                detail.transform.GetSiblingIndex(),
                Is.GreaterThan(codexRoot.transform.GetSiblingIndex()),
                "The card detail overlay must come after the codex panel so it draws over the codex and the GNB.");

            // 종료 확인 창은 도감 상세까지 덮어야 한다.
            Component quitDialog = ReadProperty<Component>(bindings, "QuitDialog");
            Assert.That(ReadProperty<GameObject>(quitDialog, "Root").activeSelf, Is.False,
                "The quit dialog must start hidden.");
            Assert.That(
                quitDialog.transform.GetSiblingIndex(),
                Is.GreaterThan(detail.transform.GetSiblingIndex()),
                "The quit dialog must come after the card detail overlay.");
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
