using System;
using System.Collections;
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

            // 다른 테스트가 남긴 앱 인스턴스를 먼저 제거한다. 앱이 들고 있는 viewport 참조를
            // 남긴 채 아래 Canvas만 지우면 다음 프레임 Update에서 예외가 난다.
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

            // 런타임은 UI를 만들지 않는다. 씬이 하는 일(프리팹 배치)을 테스트가 대신 한다.
            GameObject uiPrefab = Resources.Load<GameObject>("PixelRoad/UI/PixelRoadUIRoot");
            Assert.That(uiPrefab, Is.Not.Null, "PixelRoadUIRoot.prefab was not found in Resources.");
            canvasObject = UnityEngine.Object.Instantiate(uiPrefab);
            canvasObject.name = "PixelRoadUIRoot";
            Component bindings = FindComponentNamed(canvasObject, "PixelRoad.UI.PixelRoadUiBindings");
            Assert.That(bindings, Is.Not.Null, "PixelRoadUIRoot.prefab has no PixelRoadUiBindings.");

            // 정적 지도 폴백이 없으므로, 라이브 지도를 끈 구성은 의도적으로 오류 로그를 남긴다.
            LogAssert.Expect(
                LogType.Error,
                "[PixelRoad] 라이브 벡터 지도를 사용할 수 없습니다: map_config.json 의 enableLiveVectorMap 이 false 입니다.");
            runtimeView = create.Invoke(null, new[] { config, bindings });
            Assert.That(runtimeView, Is.Not.Null);

            Assert.That(canvasObject.GetComponent<Canvas>(), Is.Not.Null,
                "The prefab instance must be the only canvas the runtime uses.");

            Component eventSystem = FindFirstComponentNamed("UnityEngine.EventSystems.EventSystem");
            Assert.That(eventSystem, Is.Not.Null);
            eventSystemObject = eventSystem.gameObject;

            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // 마커·유저 스프라이트는 모두 프리팹과 Resources 에셋이라 여기서 해제하지 않는다.
            // 런타임이 만들던 도형 텍스처가 없어졌기 때문이다.
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
                "The prefab canvas must have a GraphicRaycaster so its controls can receive clicks.");
            Assert.That(
                FindComponentNamed(canvasObject, "PixelRoad.UI.PixelRoadUiBindings"),
                Is.Not.Null,
                "The canvas must come from PixelRoadUIRoot.prefab, not from a runtime-built GameObject.");

            Component eventSystem = FindFirstComponentNamed("UnityEngine.EventSystems.EventSystem");
            Assert.That(eventSystem, Is.Not.Null,
                "PixelRoadUIRoot.prefab must ship its own EventSystem; the runtime no longer creates one.");
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

            // 와이어프레임의 하단 GNB. 지도/도감은 바로 쓸 수 있고, 리포트와 AR은 조건을 만족하기 전까지 비활성이다.
            Assert.That(ReadInteractable("MapTab"), Is.True, "The map tab must always be usable.");
            Assert.That(ReadInteractable("CodexTab"), Is.True, "The codex tab must always be usable.");
            Assert.That(ReadInteractable("ReportTab"), Is.False,
                "The AI report tab must start disabled until at least one landmark is unlocked.");
            Assert.That(ReadInteractable("ArTab"), Is.False,
                "The AR tab must start disabled until the user is inside an AR radius.");

            Transform badge = FindRequiredTransform("Badge");
            Assert.That(badge.gameObject.activeSelf, Is.False,
                "The report badge must be hidden until a new unlock is pending.");

            // 랜드마크 배너는 선택된 랜드마크가 있을 때만 보인다.
            Transform banner = FindRequiredTransform("LandmarkBanner");
            Assert.That(banner.gameObject.activeSelf, Is.False,
                "The landmark banner must stay hidden while nothing is selected.");

            Assert.That((bool)InvokeView("IsCodexVisible"), Is.False);
            InvokeView("SetCodexVisible", true);
            yield return null;
            Assert.That((bool)InvokeView("IsCodexVisible"), Is.True,
                "The Codex panel did not become visible.");

            InvokeView("SetCodexVisible", false);
            yield return null;
            Assert.That((bool)InvokeView("IsCodexVisible"), Is.False);

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

            // 카드 상세 보기가 도감 위에 겹쳐 뜨므로 카드를 눌러도 도감은 열린 채로 있어야 한다.
            InvokeButton("Codex_runtime_ui_spot");
            yield return null;
            Assert.That((bool)InvokeView("IsCodexVisible"), Is.True,
                "The Codex must stay open behind the card detail overlay.");
            Transform detail = FindRequiredTransform("CardDetail");
            Assert.That(detail.gameObject.activeSelf, Is.True,
                "Selecting an unlocked Codex card must open the card detail overlay.");
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

        private static bool ReadInteractable(string objectName)
        {
            Component button = FindRequiredComponent(objectName, "UnityEngine.UI.Button");
            PropertyInfo property = button.GetType().GetProperty(
                "interactable",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null);
            return (bool)property.GetValue(button);
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

    }
}
