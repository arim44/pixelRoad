using System.Collections;
using System.Collections.Generic;
using PixelRoad.Data;
using PixelRoad.Location;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PixelRoad.AR
{
    /// <summary>MapScene 쪽에서 ARScene으로의 전환을 담당한다: 스냅샷 준비, 로딩 화면 표시, 비동기 씬 로드.</summary>
    public static class ARSceneLauncher
    {
        private const string ArSceneName = "ARScene";
        private const string ConfigResourcePath = "PixelRoad/ar_config";
        private const float SettleSeconds = 0.15f;

        /// <summary>Resources/PixelRoad/ar_config.json 을 읽는다. 없거나 잘못돼도 기본값으로 안전하게 동작한다.</summary>
        public static ARConfig LoadConfig()
        {
            TextAsset asset = Resources.Load<TextAsset>(ConfigResourcePath);
            if (asset == null)
            {
                return new ARConfig();
            }

            ARConfig config = JsonUtility.FromJson<ARConfig>(asset.text);
            return config ?? new ARConfig();
        }

        public static IEnumerator LoadArScene(
            IReadOnlyList<SpotRuntimeState> nearbySpots,
            GeoLocationSample currentLocation)
        {
            List<ARLandmarkSnapshot> snapshot = BuildSnapshot(nearbySpots);
            ARHandoff.Prepare(snapshot, currentLocation);

            LoadingScreenView loading = LoadingScreenView.Create();
            AsyncOperation operation = SceneManager.LoadSceneAsync(ArSceneName);
            operation.allowSceneActivation = false;
            while (operation.progress < 0.9f)
            {
                loading.SetProgress(operation.progress / 0.9f);
                yield return null;
            }

            loading.SetProgress(1f);
            yield return new WaitForSeconds(SettleSeconds);
            operation.allowSceneActivation = true;
        }

        private static List<ARLandmarkSnapshot> BuildSnapshot(IReadOnlyList<SpotRuntimeState> spots)
        {
            List<ARLandmarkSnapshot> result = new List<ARLandmarkSnapshot>(spots.Count);
            for (int i = 0; i < spots.Count; i++)
            {
                SpotDefinition definition = spots[i].Definition;
                result.Add(new ARLandmarkSnapshot(
                    definition.Id,
                    definition.DisplayName,
                    definition.IconKey,
                    definition.Category,
                    definition.Latitude,
                    definition.Longitude,
                    spots[i].IsUnlocked));
            }

            return result;
        }
    }
}
