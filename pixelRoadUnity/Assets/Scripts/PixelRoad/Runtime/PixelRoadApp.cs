using System.Collections;
using System.Collections.Generic;
using PixelRoad.Data;
using PixelRoad.Geo;
using PixelRoad.Location;
using PixelRoad.UI;
using UnityEngine;

namespace PixelRoad.Runtime
{
    public sealed class PixelRoadApp : MonoBehaviour
    {
        private const string ConfigResourcePath = "PixelRoad/map_config";

        [SerializeField]
        private bool centerOnFirstLocation = true;

        private MapConfig config;
        private PixelRoadRuntimeView view;
        private ILocationProvider locationProvider;
        private VisitRepository visitRepository;
        private SpotSpatialIndex spatialIndex;
        private readonly List<SpotRuntimeState> spots = new List<SpotRuntimeState>();
        private GeoLocationSample currentLocation;
        private float unlockQueryRadiusMeters;
        private bool centeredOnce;

        /// <summary>
        /// AR 등 다른 기능에서 공유해 쓰는 현재 위치. Input.location을 중복 시작하지 않는다.
        /// </summary>
        public GeoLocationSample CurrentLocation
        {
            get { return currentLocation; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<PixelRoadApp>() != null)
            {
                return;
            }

            GameObject appObject = new GameObject("PixelRoadApp");
            appObject.AddComponent<PixelRoadApp>();
        }

        private IEnumerator Start()
        {
            Application.targetFrameRate = 60;

            if (!LoadData())
            {
                yield break;
            }

            view = PixelRoadRuntimeView.Create(config);
            view.CodexRequested += ToggleCodex;
            for (int i = 0; i < spots.Count; i++)
            {
                view.AddSpotMarker(spots[i], SelectSpot);
            }

            RefreshProgress();
            view.CenterOnLocation(config.editorStartLatitude, config.editorStartLongitude);

#if UNITY_EDITOR
            locationProvider = new SimulatedLocationProvider(
                config.editorStartLatitude,
                config.editorStartLongitude,
                config.editorMoveSpeedMetersPerSecond,
                config.editorFastMoveMultiplier);
#else
            locationProvider = new UnityGpsLocationProvider(config);
#endif
            yield return StartCoroutine(locationProvider.Start());
            view.SetLocationStatus(locationProvider.StatusText);
        }

        private void Update()
        {
            if (locationProvider == null || view == null)
            {
                return;
            }

            locationProvider.Tick(Time.deltaTime);
            currentLocation = locationProvider.Current;
            view.SetLocationStatus(BuildLocationStatus());

            if (!currentLocation.IsValid)
            {
                return;
            }

            view.UpdateUserLocation(currentLocation);
            if (centerOnFirstLocation && !centeredOnce)
            {
                view.CenterOnLocation(currentLocation.Latitude, currentLocation.Longitude);
                centeredOnce = true;
            }

#if UNITY_EDITOR
            if (config.editorFollowSimulatedLocation)
            {
                view.CenterOnLocation(currentLocation.Latitude, currentLocation.Longitude);
            }
#endif

            CheckVisits();
        }

        private void OnDestroy()
        {
            if (locationProvider != null)
            {
                locationProvider.Stop();
            }
        }

        private bool LoadData()
        {
            TextAsset configAsset = Resources.Load<TextAsset>(ConfigResourcePath);
            if (configAsset == null)
            {
                Debug.LogError("[PixelRoad] Missing Resources/" + ConfigResourcePath + ".json");
                return false;
            }

            config = JsonUtility.FromJson<MapConfig>(configAsset.text);
            if (config == null || config.bounds == null || !config.bounds.IsValid())
            {
                Debug.LogError("[PixelRoad] Invalid map_config.json bounds.");
                return false;
            }

            TextAsset landmarksAsset = Resources.Load<TextAsset>(config.landmarksJsonResourcePath);
            if (landmarksAsset == null)
            {
                Debug.LogError("[PixelRoad] Missing landmarks JSON: " + config.landmarksJsonResourcePath);
                return false;
            }

            visitRepository = new VisitRepository();
            List<SpotDefinition> definitions;
            try
            {
                definitions = LandmarkJsonParser.Parse(landmarksAsset.text, config.defaultUnlockRadiusMeters);
            }
            catch (System.FormatException exception)
            {
                Debug.LogError("[PixelRoad] Invalid landmarks.json: " + exception.Message);
                return false;
            }

            spots.Clear();
            float largestRadius = config.defaultUnlockRadiusMeters;
            for (int i = 0; i < definitions.Count; i++)
            {
                SpotDefinition definition = definitions[i];
                largestRadius = Mathf.Max(largestRadius, definition.RadiusMeters);
                spots.Add(new SpotRuntimeState(
                    definition,
                    definition.InitiallyUnlocked || visitRepository.HasVisited(definition.LandmarkId)));
            }

            unlockQueryRadiusMeters = Mathf.Max(config.defaultUnlockRadiusMeters, largestRadius);
            double centerLatitude = (config.bounds.northLat + config.bounds.southLat) * 0.5;
            spatialIndex = new SpotSpatialIndex(spots, centerLatitude, Mathf.Max(100f, largestRadius * 2f));
            return true;
        }

        private void CheckVisits()
        {
            List<SpotRuntimeState> nearby = spatialIndex.Query(currentLocation.Latitude, currentLocation.Longitude, unlockQueryRadiusMeters);
            bool changed = false;
            for (int i = 0; i < nearby.Count; i++)
            {
                SpotRuntimeState state = nearby[i];
                double distance = GeoProjection.DistanceMeters(
                    currentLocation.Latitude,
                    currentLocation.Longitude,
                    state.Definition.Latitude,
                    state.Definition.Longitude);
                if (distance > state.Definition.RadiusMeters)
                {
                    continue;
                }

                bool wasUnlocked = state.IsUnlocked;
                if (!visitRepository.RecordVisit(state.Definition.LandmarkId, System.DateTime.Now))
                {
                    continue;
                }

                if (!wasUnlocked)
                {
                    state.Unlock();
                    view.UpdateSpotState(state);
                    view.SelectSpot(state, currentLocation);
                    changed = true;
                }
            }

            if (changed)
            {
                RefreshProgress();
            }
        }

        private void SelectSpot(SpotRuntimeState state)
        {
            view.SelectSpot(state, currentLocation);
        }

        private void ToggleCodex()
        {
            view.SetCodexVisible(!view.IsCodexVisible());
        }

        private void RefreshProgress()
        {
            int unlocked = 0;
            for (int i = 0; i < spots.Count; i++)
            {
                if (spots[i].IsUnlocked)
                {
                    unlocked++;
                }
            }

            view.SetProgress(unlocked, spots.Count);
        }

        private string BuildLocationStatus()
        {
            if (!currentLocation.IsValid)
            {
                return locationProvider.StatusText;
            }

            return string.Format(
                "{0} | {1:0.000000}, {2:0.000000}",
                locationProvider.StatusText,
                currentLocation.Latitude,
                currentLocation.Longitude);
        }
    }
}
