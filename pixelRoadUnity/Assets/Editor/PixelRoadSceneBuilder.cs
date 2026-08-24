using System.Collections.Generic;
using PixelRoad.Runtime;
using PixelRoad.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace PixelRoad.Editor
{
    /// <summary>
    /// 로딩 씬을 코드로 만들고 Build Settings에 등록한다.
    ///
    /// 씬을 손으로 만들면 카메라 설정이나 씬 순서가 사람마다 달라져서, UI 프리팹과 같은 방식으로
    /// 메뉴 한 번에 재생성할 수 있게 했다.
    /// 현재 열려 있는 씬을 건드리지 않도록 새 씬은 항상 Additive로 열고, 저장한 뒤 바로 닫는다.
    /// </summary>
    public static class PixelRoadSceneBuilder
    {
        private const string ScenesFolder = "Assets/Scenes";
        private const string LoadingScenePath = ScenesFolder + "/Loading.unity";
        private const string MapScenePath = ScenesFolder + "/MapScene.unity";
        private const string UiRootPrefabPath = "Assets/Resources/PixelRoad/UI/PixelRoadUIRoot.prefab";
        private const string LoadingPrefabPath = "Assets/Resources/PixelRoad/UI/LoadingUIRoot.prefab";

        private static readonly Color AppBackground = new Color32(41, 35, 27, 255);

        /// <summary>
        /// 로딩 씬이 없을 때만 만든다. 이미 있으면 손댄 내용을 지우지 않고 Build Settings만 맞춘다.
        /// 일괄 재구성(<c>Rebuild UI Prefabs And Scenes</c>)에서 쓰는 안전한 진입점이다.
        /// </summary>
        public static void EnsureLoadingScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LoadingScenePath) != null)
            {
                Debug.Log("[PixelRoad] 로딩 씬이 이미 있어 그대로 둡니다: " + LoadingScenePath);
                RegisterBuildScenes();
                return;
            }

            RebuildLoadingScene();
        }

        /// <summary>
        /// 로딩 씬을 처음부터 다시 만든다. 카메라·LoadingBoot·로딩 오버레이를 배치하고 저장한 뒤 Build Settings까지 맞춘다.
        /// </summary>
        [MenuItem("Tools/Pixel Road/Rebuild Loading Scene")]
        public static void RebuildLoadingScene()
        {
            // 열려 있는 씬을 지키려고 Additive로 만드는 게 기본이다. 다만 배치 모드(-batchmode)의
            // 시작 상태인 "저장되지 않은 무제 씬"에서는 Additive 생성이 거부되므로 그때만 Single을 쓴다.
            bool canUseAdditive = !string.IsNullOrEmpty(SceneManager.GetActiveScene().path);
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                canUseAdditive ? NewSceneMode.Additive : NewSceneMode.Single);

            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = AppBackground;
            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            SceneManager.MoveGameObjectToScene(cameraObject, scene);

            GameObject boot = new GameObject("LoadingBoot", typeof(LoadingSceneController));
            SceneManager.MoveGameObjectToScene(boot, scene);

            // 오버레이는 LoadingBoot의 자식이어야 DontDestroyOnLoad로 씬 전환을 함께 넘어간다.
            LoadingUiBindings loadingUi = InstantiatePrefabInScene<LoadingUiBindings>(
                LoadingPrefabPath,
                scene,
                "LoadingUIRoot");
            if (loadingUi == null)
            {
                CloseIfAdditive(scene, canUseAdditive);
                return;
            }

            loadingUi.transform.SetParent(boot.transform, false);
            AssignReference(boot.GetComponent<LoadingSceneController>(), "ui", loadingUi);

            if (!EditorSceneManager.SaveScene(scene, LoadingScenePath))
            {
                Debug.LogError("[PixelRoad] 로딩 씬을 저장하지 못했습니다: " + LoadingScenePath);
                CloseIfAdditive(scene, canUseAdditive);
                return;
            }

            CloseIfAdditive(scene, canUseAdditive);
            RegisterBuildScenes();
            Debug.Log("[PixelRoad] 로딩 씬을 만들고 Build Settings에 등록했습니다: " + LoadingScenePath);
        }

        /// <summary>
        /// 지도 씬을 "런타임 생성 없음" 구성으로 맞춘다.
        ///
        /// 1) 프리팹과 겹치는 빈 Canvas·EventSystem 제거
        /// 2) `PixelRoadUIRoot` 프리팹 인스턴스 배치(없을 때만)
        /// 3) `PixelRoadApp` 오브젝트 배치 후 `uiBindings` 참조 연결
        ///
        /// 카메라 등 나머지 오브젝트는 건드리지 않는다.
        /// </summary>
        [MenuItem("Tools/Pixel Road/Setup Map Scene")]
        public static void SetupMapScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MapScenePath) == null)
            {
                Debug.LogWarning("[PixelRoad] 지도 씬 파일이 없습니다: " + MapScenePath);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(MapScenePath, OpenSceneMode.Additive);

            PixelRoadUiBindings bindings = FindInScene<PixelRoadUiBindings>(scene);
            int removed = RemoveDuplicateUiObjects(scene);

            if (bindings == null)
            {
                bindings = InstantiatePrefabInScene<PixelRoadUiBindings>(
                    UiRootPrefabPath,
                    scene,
                    "PixelRoadUIRoot");
                if (bindings == null)
                {
                    EditorSceneManager.CloseScene(scene, true);
                    return;
                }
            }

            PixelRoadApp app = FindInScene<PixelRoadApp>(scene);
            if (app == null)
            {
                GameObject appObject = new GameObject("PixelRoadApp", typeof(PixelRoadApp));
                SceneManager.MoveGameObjectToScene(appObject, scene);
                app = appObject.GetComponent<PixelRoadApp>();
            }

            AssignReference(app, "uiBindings", bindings);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
            Debug.Log("[PixelRoad] 지도 씬을 구성했습니다(중복 UI " + removed
                + "개 제거, PixelRoadUIRoot + PixelRoadApp 배치): " + MapScenePath);
        }

        /// <summary>
        /// Additive로 연 씬만 닫는다. Single로 만든 씬은 유일하게 열린 씬이라 닫을 수 없다.
        /// </summary>
        private static void CloseIfAdditive(Scene scene, bool wasAdditive)
        {
            if (wasAdditive && scene.IsValid())
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>프리팹이 들고 오는 Canvas·EventSystem과 겹치는 씬 오브젝트를 지운다.</summary>
        private static int RemoveDuplicateUiObjects(Scene scene)
        {
            int removed = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                GameObject candidate = roots[index];
                if (candidate.GetComponentInChildren<PixelRoadUiBindings>(true) != null)
                {
                    continue;
                }

                if (candidate.GetComponent<Canvas>() == null
                    && candidate.GetComponent<EventSystem>() == null)
                {
                    continue;
                }

                Object.DestroyImmediate(candidate);
                removed++;
            }

            return removed;
        }

        /// <summary>씬 안에서 해당 컴포넌트를 찾는다. 비활성 오브젝트도 포함한다.</summary>
        private static T FindInScene<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                T found = roots[index].GetComponentInChildren<T>(true);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>
        /// 프리팹을 지정 씬에 인스턴스로 배치하고 원하는 컴포넌트를 돌려준다. 프리팹이나 컴포넌트가 없으면 null이다.
        /// </summary>
        private static T InstantiatePrefabInScene<T>(string prefabPath, Scene scene, string objectName)
            where T : Component
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError("[PixelRoad] 프리팹을 찾지 못했습니다: " + prefabPath
                    + ". UI 프리팹은 에셋으로 직접 관리하므로 파일이 지워졌다면 버전 관리에서 되돌리세요.");
                return null;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = objectName;
            T component = instance.GetComponent<T>();
            if (component == null)
            {
                Debug.LogError("[PixelRoad] " + prefabPath + " 에 " + typeof(T).Name + " 컴포넌트가 없습니다.");
                Object.DestroyImmediate(instance);
                return null;
            }

            return component;
        }

        /// <summary>씬 오브젝트의 직렬화 필드에 참조를 넣는다.</summary>
        private static void AssignReference(Object target, string fieldName, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError("[PixelRoad] " + target.GetType().Name
                    + " 에 직렬화 필드 '" + fieldName + "' 가 없습니다.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Build Settings를 로딩 씬 → 지도 씬 순서로 맞춘다.</summary>
        [MenuItem("Tools/Pixel Road/Register Build Scenes")]
        public static void RegisterBuildScenes()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
            AddSceneIfExists(scenes, LoadingScenePath);
            AddSceneIfExists(scenes, MapScenePath);

            if (scenes.Count == 0)
            {
                Debug.LogWarning("[PixelRoad] 등록할 씬을 찾지 못했습니다.");
                return;
            }

            EditorBuildSettings.scenes = scenes.ToArray();

            // 배치 모드(-executeMethod -quit)에서는 명시적으로 저장해야 EditorBuildSettings.asset에 남는다.
            AssetDatabase.SaveAssets();

            System.Text.StringBuilder order = new System.Text.StringBuilder();
            for (int index = 0; index < scenes.Count; index++)
            {
                if (index > 0)
                {
                    order.Append(" → ");
                }

                order.Append(System.IO.Path.GetFileNameWithoutExtension(scenes[index].path));
            }

            Debug.Log("[PixelRoad] Build Settings 씬 순서: " + order);
        }

        /// <summary>씬 파일이 실제로 있을 때만 빌드 목록에 넣는다.</summary>
        private static void AddSceneIfExists(List<EditorBuildSettingsScene> scenes, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                Debug.LogWarning("[PixelRoad] 씬 파일이 없습니다: " + path);
                return;
            }

            scenes.Add(new EditorBuildSettingsScene(path, true));
        }
    }
}
