using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PixelRoad.Tests.PlayMode
{
    public sealed class ShortbreadIntegrationTests
    {
        private const string GuardName = "PixelRoad PlayMode Network Guard";
        private const string AppTypeName = "PixelRoad.Runtime.PixelRoadApp, Assembly-CSharp";
        private const string RendererTypeName = "PixelRoad.Mapping.LiveVectorMapRenderer";

        // A disabled app instance prevents unrelated/default PlayMode runs from issuing
        // network requests. The explicit integration test removes it before starting once.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallDefaultNetworkGuard()
        {
            // Never alter ordinary Editor Play Mode. This guard exists only for
            // command-line test runs where -runTests is present.
            if (!HasCommandLineSwitch("-runTests"))
            {
                return;
            }

            Type appType = Type.GetType(AppTypeName, false);
            if (appType == null || GameObject.Find(GuardName) != null)
            {
                return;
            }

            GameObject guard = new GameObject(GuardName);
            Behaviour app = guard.AddComponent(appType) as Behaviour;
            if (app != null)
            {
                app.enabled = false;
            }

            UnityEngine.Object.DontDestroyOnLoad(guard);
        }

        [UnityTest]
        [Category("Integration")]
        public IEnumerator CurrentViewport_RendersLiveTiles()
        {
            if (!HasCommandLineSwitch("-pixelRoadRunNetworkIntegration"))
            {
                Assert.Ignore(
                    "Network integration is opt-in. Pass -pixelRoadRunNetworkIntegration to request one visible viewport.");
            }

            GameObject guard = GameObject.Find(GuardName);
            if (guard != null)
            {
                UnityEngine.Object.Destroy(guard);
                yield return null;
            }

            Type appType = Type.GetType(AppTypeName, true);
            GameObject appObject = new GameObject("PixelRoad Integration App");
            appObject.AddComponent(appType);

            MonoBehaviour renderer = null;
            PropertyInfo hasRenderedTile = null;
            float deadline = Time.realtimeSinceStartup + 45f;
            while (Time.realtimeSinceStartup < deadline)
            {
                renderer = FindBehaviour(RendererTypeName);
                if (renderer != null)
                {
                    hasRenderedTile = renderer.GetType().GetProperty("HasRenderedTile");
                    if (hasRenderedTile != null && (bool)hasRenderedTile.GetValue(renderer))
                    {
                        break;
                    }
                }

                yield return null;
            }

            string lastError = renderer == null
                ? "renderer was not created"
                : Convert.ToString(renderer.GetType().GetProperty("LastError")?.GetValue(renderer));
            Assert.That(renderer, Is.Not.Null, "LiveVectorMapRenderer was not created.");
            Assert.That(
                hasRenderedTile != null && (bool)hasRenderedTile.GetValue(renderer),
                Is.True,
                "No Shortbread tile rendered within 45 seconds. Last error: " + lastError);

            GameObject outputObject = GameObject.Find("LiveVectorMap");
            Assert.That(outputObject, Is.Not.Null);
            Component rawImage = FindComponent(outputObject, "UnityEngine.UI.RawImage");
            Assert.That(rawImage, Is.Not.Null);
            PropertyInfo textureProperty = rawImage.GetType().GetProperty("texture");
            Assert.That(textureProperty, Is.Not.Null);

            yield return null;
            RenderTexture output = textureProperty.GetValue(rawImage) as RenderTexture;
            Assert.That(output, Is.Not.Null);
            Assert.That(output.IsCreated(), Is.True);
            Assert.That(output.filterMode, Is.EqualTo(FilterMode.Bilinear));

            GameObject attribution = GameObject.Find("MapAttribution");
            GameObject viewport = GameObject.Find("MapViewport");
            Assert.That(attribution, Is.Not.Null);
            Assert.That(viewport, Is.Not.Null);
            Assert.That(attribution.transform.IsChildOf(viewport.transform), Is.False,
                "Attribution must stay outside the map output.");

            FieldInfo requiredKeysField = renderer.GetType().GetField(
                "requiredKeys",
                BindingFlags.Instance | BindingFlags.NonPublic);
            object requiredKeys = requiredKeysField?.GetValue(renderer);
            PropertyInfo countProperty = requiredKeys?.GetType().GetProperty("Count");
            Assert.That(requiredKeys, Is.Not.Null);
            Assert.That(countProperty, Is.Not.Null);
            int visibleTileCount = (int)countProperty.GetValue(requiredKeys);
            Assert.That(visibleTileCount, Is.GreaterThan(0));
            Assert.That(visibleTileCount, Is.LessThanOrEqualTo(100));
            Debug.Log(
                "[PixelRoad Integration] visible tiles=" + visibleTileCount
                + ", output=" + output.width + "x" + output.height);

            UnityEngine.Object.Destroy(appObject);
        }

        private static MonoBehaviour FindBehaviour(string fullTypeName)
        {
            MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < behaviours.Length; index++)
            {
                if (behaviours[index] != null && behaviours[index].GetType().FullName == fullTypeName)
                {
                    return behaviours[index];
                }
            }

            return null;
        }

        private static Component FindComponent(GameObject gameObject, string fullTypeName)
        {
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

        private static bool HasCommandLineSwitch(string switchName)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length; index++)
            {
                if (string.Equals(arguments[index], switchName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
