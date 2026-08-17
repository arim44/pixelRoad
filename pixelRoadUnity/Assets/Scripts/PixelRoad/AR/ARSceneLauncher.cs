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
        private const string MapSceneName = "MapScene";
        private const string ConfigResourcePath = "PixelRoad/ar_config";

        // LoadingScreenView의 페이드인(FadeInSeconds)보다 짧으면 안 된다 - 씬 전환이 더 빨리 끝나면
        // 페이드인이 채 끝나기 전에 페이드아웃이 시작되며 방향이 뒤집혀 화면이 깜빡이는 것처럼 보인다.
        private const float SettleSeconds = 0.3f;

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
            yield return DriveSceneLoad(operation, loading, captureCompass: true);

            // 로딩 화면은 DontDestroyOnLoad라 씬이 바뀌어도 계속 떠 있다. ARSceneController가 자기 UI를
            // 다 세운 뒤 이어받아 페이드아웃시킨다 - 여기서 미리 끄면 전환 도중 지도 화면이 다시 보인다.
            ARHandoff.PendingLoadingScreen = loading;
            operation.allowSceneActivation = true;
        }

        /// <summary>ARScene 뒤로가기(또는 근처 랜드마크 없음 타임아웃)로 MapScene에 돌아갈 때 쓴다.</summary>
        public static IEnumerator LoadMapScene()
        {
            LoadingScreenView loading = LoadingScreenView.Create();

            AsyncOperation operation = SceneManager.LoadSceneAsync(MapSceneName);
            operation.allowSceneActivation = false;
            yield return DriveSceneLoad(operation, loading, captureCompass: false);

            // PixelRoadApp이 자기 UI를 다 세운 뒤 이어받아 페이드아웃시킨다(LoadARScene과 동일한 이유).
            ARHandoff.PendingLoadingScreen = loading;
            operation.allowSceneActivation = true;
        }

        /// <summary>씬 로드 진행률을 로딩 화면에 반영하며 90%까지, 그 뒤 SettleSeconds만큼 더 기다린다.</summary>
        private static IEnumerator DriveSceneLoad(AsyncOperation operation, LoadingScreenView loading, bool captureCompass)
        {
            while (operation.progress < 0.9f)
            {
                loading?.SetProgress(operation.progress / 0.9f);
                if (captureCompass)
                {
                    CaptureCompassHeading();
                }

                yield return null;
            }

            float settleElapsed = 0f;
            while (settleElapsed < SettleSeconds)
            {
                // 목표는 이미 100%지만, 표시 진행률이 서서히 따라가는 동안 계속 불러 줘야 애니메이션이 이어진다.
                loading?.SetProgress(1f);
                if (captureCompass)
                {
                    CaptureCompassHeading();
                }

                settleElapsed += Time.deltaTime;
                yield return null;
            }
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
                    definition.RadiusMeters,
                    spots[i].IsUnlocked));
            }

            return result;
        }
    }
}
