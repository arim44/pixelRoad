using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PixelRoad.Tests.PlayMode
{
    public sealed class RuntimeUiInteractionTests
    {
        private const string RuntimeViewTypeName = "PixelRoad.UI.PixelRoadRuntimeView, Assembly-CSharp";
        private const string MapConfigTypeName = "PixelRoad.Data.MapConfig, Assembly-CSharp";
        private const string SpotDefinitionTypeName = "PixelRoad.Data.SpotDefinition, Assembly-CSharp";
        private const string SpotRuntimeStateTypeName = "PixelRoad.Data.SpotRuntimeState, Assembly-CSharp";
        private const string AppTypeName = "PixelRoad.Runtime.PixelRoadApp";
        private const string PixelModePreferenceKey = "PixelRoad.MapPixelMode";

        private Type runtimeViewType;
        private object runtimeView;
        private GameObject canvasObject;
        private GameObject eventSystemObject;
        private bool hadPixelPreference;
        private int previousPixelPreference;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            hadPixelPreference = PlayerPrefs.HasKey(PixelModePreferenceKey);
            previousPixelPreference = PlayerPrefs.GetInt(PixelModePreferenceKey, 0);
            PlayerPrefs.SetInt(PixelModePreferenceKey, 0);

            // RuntimeInitializeOnLoadMethod가 만든 실제 앱을 먼저 제거한다. 앱이 들고 있는
            // viewport 참조를 남긴 채 아래 Canvas만 지우면 다음 프레임 Update에서 예외가 난다.
            DestroyAllComponentsNamed(AppTypeName);
            yield return null;

            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < canvases.Length; index++)
            {
                if (canvases[index] != null)
                {
                    UnityEngine.Object.Destroy(canvases[index].gameObject);
                }
            }

            DestroyAllComponentsNamed("UnityEngine.EventSystems.EventSystem");
            yield return null;

            runtimeViewType = Type.GetType(RuntimeViewTypeName, true);
            Type configType = Type.GetType(MapConfigTypeName, true);
            object config = Activator.CreateInstance(configType);
            SetPublicField(config, "enableLiveVectorMap", false);
            SetPublicField(config, "allowLiveVectorMapInRelease", false);
            SetPublicField(config, "enablePixelFilter", false);

            MethodInfo create = runtimeViewType.GetMethod(
                "Create",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(create, Is.Not.Null);

            // 정적 지도 폴백이 없으므로, 라이브 지도를 끈 구성은 의도적으로 오류 로그를 남긴다.
            LogAssert.Expect(
                LogType.Error,
                "[PixelRoad] 라이브 벡터 지도를 사용할 수 없습니다: map_config.json 의 enableLiveVectorMap 이 false 입니다.");
            runtimeView = create.Invoke(null, new[] { config });
            Assert.That(runtimeView, Is.Not.Null);

            Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            Assert.That(canvas, Is.Not.Null);
            canvasObject = canvas.gameObject;

            Component eventSystem = FindFirstComponentNamed("UnityEngine.EventSystems.EventSystem");
            Assert.That(eventSystem, Is.Not.Null);
            eventSystemObject = eventSystem.gameObject;

            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DestroyRuntimeSprites(canvasObject);
            DestroyPrivateUnityObject("pixelFont");

            if (canvasObject != null)
            {
                UnityEngine.Object.Destroy(canvasObject);
            }

            if (eventSystemObject != null)
            {
                UnityEngine.Object.Destroy(eventSystemObject);
            }

            if (hadPixelPreference)
            {
                PlayerPrefs.SetInt(PixelModePreferenceKey, previousPixelPreference);
            }
            else
            {
                PlayerPrefs.DeleteKey(PixelModePreferenceKey);
            }

            PlayerPrefs.Save();
            runtimeView = null;
            yield return null;
        }

        [UnityTest]
        [Category("Regression")]
        public IEnumerator RuntimeUi_ButtonsRemainInteractiveWithoutLiveMapOrNetwork()
        {
            Assert.That(
                FindComponentNamed(canvasObject, "UnityEngine.UI.GraphicRaycaster"),
                Is.Not.Null,
                "The runtime canvas must have a GraphicRaycaster so its controls can receive clicks.");

            Component eventSystem = FindFirstComponentNamed("UnityEngine.EventSystems.EventSystem");
            Assert.That(eventSystem, Is.Not.Null, "The runtime UI must create an EventSystem.");
            Component inputModule = FindComponentNamed(
                eventSystem.gameObject,
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule");
            Assert.That(inputModule, Is.Not.Null,
                "The new Input System UI module must be attached to the EventSystem.");
            Assert.That(((Behaviour)inputModule).isActiveAndEnabled, Is.True);
            Assert.That(ReadPublicProperty(inputModule, "actionsAsset"), Is.Not.Null,
                "The Input System UI module must have an actions asset.");
            AssertInputActionAssigned(inputModule, "point");
            AssertInputActionAssigned(inputModule, "leftClick");
            AssertInputActionAssigned(inputModule, "scrollWheel");
            Assert.That(FindFirstComponentNamed("PixelRoad.Mapping.LiveVectorMapRenderer"), Is.Null,
                "This regression test must stay network-free when live vector maps are disabled.");
            Assert.That((bool)ReadPublicProperty(runtimeView, "IsMapAvailable"), Is.False,
                "No live map renderer means the view must report the map as unavailable.");

            Transform mapNotice = FindRequiredTransform("MapNotice");
            Assert.That(mapNotice.gameObject.activeSelf, Is.True,
                "The map notice must explain why no map is drawn once the static fallback is gone.");
            Assert.That(ReadChildText(mapNotice.gameObject), Does.Contain("지도를 표시할 수 없습니다"));

            bool codexRequested = false;
            Action codexHandler = () =>
            {
                codexRequested = true;
                bool currentlyVisible = (bool)InvokeView("IsCodexVisible");
                InvokeView("SetCodexVisible", !currentlyVisible);
            };
            EventInfo codexEvent = runtimeViewType.GetEvent("CodexRequested");
            Assert.That(codexEvent, Is.Not.Null);
            codexEvent.AddEventHandler(runtimeView, codexHandler);

            Assert.That((bool)InvokeView("IsCodexVisible"), Is.False);
            InvokeButton("CodexButton");
            yield return null;
            Assert.That(codexRequested, Is.True, "The Codex button did not raise its request event.");
            Assert.That((bool)InvokeView("IsCodexVisible"), Is.True,
                "The Codex panel did not become visible after clicking the Codex button.");

            Component pixelToggle = FindRequiredComponent("PixelToggle", "UnityEngine.UI.Button");
            Assert.That(ReadChildText(pixelToggle.gameObject), Is.EqualTo("픽셀 OFF"));
            Assert.That(ReadPrivateBool("pixelFilterEnabled"), Is.False);

            InvokeButton("PixelToggle");
            yield return null;
            Assert.That(ReadChildText(pixelToggle.gameObject), Is.EqualTo("픽셀 ON"));
            Assert.That(ReadPrivateBool("pixelFilterEnabled"), Is.True);

            InvokeButton("PixelToggle");
            yield return null;
            Assert.That(ReadChildText(pixelToggle.gameObject), Is.EqualTo("픽셀 OFF"));
            Assert.That(ReadPrivateBool("pixelFilterEnabled"), Is.False);

            object spotState = CreateSpotState();
            MethodInfo addSpotMarker = runtimeViewType.GetMethod("AddSpotMarker");
            Assert.That(addSpotMarker, Is.Not.Null);
            addSpotMarker.Invoke(runtimeView, new[] { spotState, null });
            yield return null;

            Transform marker = FindRequiredTransform("Spot_runtime_ui_spot");
            Assert.That(marker.parent.name, Is.EqualTo("MapMarkerOverlay"),
                "Spot markers must live on the live-map marker overlay.");
            Assert.That(
                FindComponentNamed(marker.gameObject, "PixelRoad.UI.MapMarkerTapTarget"),
                Is.Not.Null,
                "Spot markers must use MapMarkerTapTarget so taps survive the map pan drag handler.");
            Assert.That(FindComponentNamed(marker.gameObject, "UnityEngine.UI.Button"), Is.Null,
                "A plain Button on a marker loses its click to the viewport drag handler on touch devices.");
            Assert.That(marker.gameObject.activeSelf, Is.False,
                "Without a live map there is no projection, so markers must stay hidden.");

            InvokeView("SetCodexVisible", true);
            Assert.That((bool)InvokeView("IsCodexVisible"), Is.True);

            InvokeButton("Codex_runtime_ui_spot");
            yield return null;
            Assert.That((bool)InvokeView("IsCodexVisible"), Is.False,
                "Selecting a Codex card must close the Codex panel.");
        }

        private object CreateSpotState()
        {
            Type definitionType = Type.GetType(SpotDefinitionTypeName, true);
            object definition = Activator.CreateInstance(
                definitionType,
                "runtime_ui_spot",
                "UI 테스트 거점",
                "도감 카드 상호작용 검증용 거점",
                "테스트",
                37.579617d,
                126.977041d,
                50f,
                "test",
                true);

            Type stateType = Type.GetType(SpotRuntimeStateTypeName, true);
            return Activator.CreateInstance(stateType, definition, true);
        }

        private object InvokeView(string methodName, params object[] arguments)
        {
            MethodInfo method = runtimeViewType.GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Missing runtime-view method: " + methodName);
            return method.Invoke(runtimeView, arguments);
        }

        private bool ReadPrivateBool(string fieldName)
        {
            FieldInfo field = runtimeViewType.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing runtime-view field: " + fieldName);
            return (bool)field.GetValue(runtimeView);
        }

        private void DestroyPrivateUnityObject(string fieldName)
        {
            if (runtimeView == null || runtimeViewType == null)
            {
                return;
            }

            FieldInfo field = runtimeViewType.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field?.GetValue(runtimeView) is UnityEngine.Object value && value != null)
            {
                UnityEngine.Object.Destroy(value);
            }
        }

        private static void SetPublicField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, "Missing config field: " + fieldName);
            field.SetValue(target, value);
        }

        private static void InvokeButton(string objectName)
        {
            Component button = FindRequiredComponent(objectName, "UnityEngine.UI.Button");
            PropertyInfo onClickProperty = button.GetType().GetProperty("onClick");
            Assert.That(onClickProperty, Is.Not.Null);
            object onClick = onClickProperty.GetValue(button);
            Assert.That(onClick, Is.Not.Null);
            MethodInfo invoke = onClick.GetType().GetMethod("Invoke", Type.EmptyTypes);
            Assert.That(invoke, Is.Not.Null);
            invoke.Invoke(onClick, null);
        }

        private static string ReadChildText(GameObject root)
        {
            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                if (component == null || component.GetType().FullName != "TMPro.TextMeshProUGUI")
                {
                    continue;
                }

                PropertyInfo textProperty = component.GetType().GetProperty("text");
                Assert.That(textProperty, Is.Not.Null);
                return Convert.ToString(textProperty.GetValue(component));
            }

            Assert.Fail("No TextMeshPro label was found under " + root.name + ".");
            return string.Empty;
        }

        private static void AssertInputActionAssigned(Component inputModule, string propertyName)
        {
            object actionReference = ReadPublicProperty(inputModule, propertyName);
            Assert.That(actionReference, Is.Not.Null,
                "The Input System UI module is missing its " + propertyName + " action reference.");
            Assert.That(ReadPublicProperty(actionReference, "action"), Is.Not.Null,
                "The Input System UI module has no action assigned to " + propertyName + ".");
        }

        private static object ReadPublicProperty(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null,
                target.GetType().FullName + " is missing property " + propertyName + ".");
            return property.GetValue(target);
        }

        private static Component FindRequiredComponent(string objectName, string componentTypeName)
        {
            Transform transform = FindRequiredTransform(objectName);
            Component component = FindComponentNamed(transform.gameObject, componentTypeName);
            Assert.That(component, Is.Not.Null,
                objectName + " is missing component " + componentTypeName + ".");
            return component;
        }

        private static Transform FindRequiredTransform(string objectName)
        {
            Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (transforms[index] != null && transforms[index].name == objectName)
                {
                    return transforms[index];
                }
            }

            Assert.Fail("Runtime UI object was not found: " + objectName);
            return null;
        }

        private static Component FindFirstComponentNamed(string fullTypeName)
        {
            Component[] components = UnityEngine.Object.FindObjectsByType<Component>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < components.Length; index++)
            {
                if (components[index] != null && components[index].GetType().FullName == fullTypeName)
                {
                    return components[index];
                }
            }

            return null;
        }

        private static Component FindComponentNamed(GameObject gameObject, string fullTypeName)
        {
            if (gameObject == null)
            {
                return null;
            }

            Component[] components = gameObject.GetComponents<Component>();
            for (int index = 0; index < components.Length; index++)
            {
                if (components[index] != null && components[index].GetType().FullName == fullTypeName)
                {
                    return components[index];
                }
            }

            return null;
        }

        private static void DestroyAllComponentsNamed(string fullTypeName)
        {
            Component[] components = UnityEngine.Object.FindObjectsByType<Component>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                if (component != null && component.GetType().FullName == fullTypeName)
                {
                    UnityEngine.Object.Destroy(component.gameObject);
                }
            }
        }

        private static void DestroyRuntimeSprites(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            HashSet<Sprite> sprites = new HashSet<Sprite>();
            HashSet<Texture2D> textures = new HashSet<Texture2D>();
            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                if (component == null || component.GetType().FullName != "UnityEngine.UI.Image")
                {
                    continue;
                }

                PropertyInfo spriteProperty = component.GetType().GetProperty("sprite");
                if (spriteProperty?.GetValue(component) is Sprite sprite && sprite != null)
                {
                    sprites.Add(sprite);
                    if (sprite.texture != null)
                    {
                        textures.Add(sprite.texture);
                    }
                }
            }

            foreach (Sprite sprite in sprites)
            {
                UnityEngine.Object.Destroy(sprite);
            }

            foreach (Texture2D texture in textures)
            {
                UnityEngine.Object.Destroy(texture);
            }
        }
    }
}
