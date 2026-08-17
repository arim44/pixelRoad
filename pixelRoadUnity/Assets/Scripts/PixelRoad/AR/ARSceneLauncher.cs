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
        private const string ARSceneName = "ARScene";
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

        public static IEnumerator LoadARScene(
            IReadOnlyList<SpotRuntimeState> nearbySpots,
            GeoLocation currentLocation)
        {
            List<ARLandmarkSnapshot> snapshot = BuildSnapshot(nearbySpots);
            ARHandoff.Prepare(snapshot, nearbySpots, currentLocation);

            LoadingScreenView loading = LoadingScreenView.Create();

            // ARSession이 시작되면 기기에 따라 나침반 센서를 독점해 이후 Input.compass가 멈추는 경우가 많다.
            // 아직 ARScene(및 ARSession)이 뜨기 전인 로딩 화면 동안 미리 나침반을 예열해서 진북 기준값을
            // 확보해 둔다. ARScene 쪽에서는 이 값을 시작 기준으로 삼고, 라이브 나침반이 살아있는 기기에서는
            // 그걸로 계속 보정한다.
            Input.compass.enabled = true;

            AsyncOperation operation = SceneManager.LoadSceneAsync(ARSceneName);
            operation.allowSceneActivation = false;
            while (operation.progress < 0.9f)
            {
                loading.SetProgress(operation.progress / 0.9f);
                CaptureCompassHeading();
                yield return null;
            }

            loading.SetProgress(1f);
            float settleElapsed = 0f;
            while (settleElapsed < SettleSeconds)
            {
                CaptureCompassHeading();
                settleElapsed += Time.deltaTime;
                yield return null;
            }

            operation.allowSceneActivation = true;
        }

        private static void CaptureCompassHeading()
        {
            if (!Input.compass.enabled)
            {
                return;
            }

            bool hasLocationFix = Input.location.status == LocationServiceStatus.Running;
            float heading = hasLocationFix ? Input.compass.trueHeading : Input.compass.magneticHeading;
            ARHandoff.SetInitialHeading(heading);
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
