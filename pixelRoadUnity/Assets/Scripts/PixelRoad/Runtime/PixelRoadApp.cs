using System.Collections;
using System.Collections.Generic;
using PixelRoad.AR;
using PixelRoad.Data;
using PixelRoad.Geo;
using PixelRoad.Location;
using PixelRoad.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PixelRoad.Runtime
{
    /// <summary>
    /// 지도 씬의 진입점. 설정과 랜드마크 데이터를 읽어 뷰를 세우고, 위치 갱신과 방문 판정을 매 프레임 이어 준다.
    /// </summary>
    public sealed class PixelRoadApp : MonoBehaviour
    {
        /// <summary>인스펙터에서 편집하는 설정 에셋. 있으면 JSON보다 우선한다.</summary>
        private const string ConfigAssetResourcePath = "PixelRoad/MapConfig";

        /// <summary>설정 에셋이 없을 때 쓰는 예전 JSON 경로.</summary>
        private const string ConfigResourcePath = "PixelRoad/map_config";
        private const string MapSceneName = "MapScene";
        private const float ARLoadingFadeOutSeconds = 0.25f;

        /// <summary>
        /// 지도 씬에 배치된 UI 프리팹 인스턴스의 참조.
        /// 런타임에 프리팹을 찾거나 Instantiate 하지 않고, 씬에서 주입받는다.
        /// </summary>
        [SerializeField]
        private PixelRoadUiBindings uiBindings;

        private MapConfig config;
        private PixelRoadRuntimeView view;
        private ILocationProvider locationProvider;
        private VisitRepository visitRepository;
        private SpotSpatialIndex spatialIndex;
        private readonly List<SpotRuntimeState> spots = new List<SpotRuntimeState>();
        private GeoLocation currentLocation;
        private float unlockQueryRadiusMeters;

        /// <summary>종료 확인 창에서 승인했는지. 안드로이드의 종료 가로채기를 통과시키는 조건이다.</summary>
        private bool quitConfirmed;

        /// <summary>
        /// AR 등 다른 기능에서 공유해 쓰는 현재 위치. Input.location을 중복 시작하지 않는다.
        /// </summary>
        public GeoLocation CurrentLocation
        {
            get { return currentLocation; }
        }

        public List<SpotRuntimeState> Spots
        {
            get { return spots; }
        }

        // RuntimeInitializeOnLoadMethod는 앱 실행 중 딱 한 번만 실행되므로(최초 씬 로드 직후),
        // MapScene을 다시 불러왔을 때(ARScene에서 돌아오는 경우 등)도 자기 자신을 재생성하려면
        // SceneManager.sceneLoaded를 구독해야 한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterBootstrap()
        {
            SceneManager.sceneLoaded -= OnMapSceneLoaded;
            SceneManager.sceneLoaded += OnMapSceneLoaded;
        }

        private static void OnMapSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != MapSceneName || FindFirstObjectByType<PixelRoadApp>() != null)
            {
                return;
            }

            GameObject appObject = new GameObject("PixelRoadApp");
            appObject.AddComponent<PixelRoadApp>();
        }

        /// <summary>
        /// 데이터 로드 → 뷰 구성 → 위치 공급자 시작 순으로 앱을 띄운다. 도중에 실패해도 로딩 화면은 반드시 닫아 준다.
        /// </summary>
        private IEnumerator Start()
        {
            Application.targetFrameRate = 60;

            if (uiBindings == null)
            {
                // 씬에 PixelRoadUIRoot 프리팹 인스턴스를 두고 이 필드에 연결해야 한다.
                // 런타임 생성 경로를 없앴기 때문에 여기서 대신 만들어 주지 않는다.
                Debug.LogError(
                    "[PixelRoad] PixelRoadApp.uiBindings 가 비어 있습니다. "
                    + "MapScene 에 PixelRoadUIRoot 프리팹을 배치하고 참조를 연결하세요. "
                    + "(Tools > Pixel Road > Setup Map Scene)");
                AppReadySignal.RaiseMapReady();
                yield break;
            }

            if (!LoadData())
            {
                // 데이터를 못 읽으면 지도를 띄울 수 없다. 로딩 화면이 영원히 남지 않도록 신호는 보낸다.
                AppReadySignal.RaiseMapReady();
                yield break;
            }

            view = PixelRoadRuntimeView.Create(config, uiBindings);
            view.GnbTabSelected += HandleGnbTabSelected;
            view.QuitConfirmed += QuitApp;
            view.ARRequested += OnARRequested;

            // ARScene에서 뒤로가기로 돌아온 경우, 지도 UI가 다 세워진 지금 그 로딩 화면을 이어받아
            // 페이드아웃시킨다. ARScene에서 미리 끄면 씬이 바뀌기 전에 AR 화면이 잠깐 다시 보여
            // 깜빡이는 것처럼 보이는 문제가 있었다(AR 진입 때와 같은 이유).
            ARHandoff.PendingLoadingScreen?.FadeOutAndDestroy(ARLoadingFadeOutSeconds);
            ARHandoff.PendingLoadingScreen = null;

            for (int i = 0; i < spots.Count; i++)
            {
                view.AddSpotMarker(spots[i], SelectSpot);
            }

            view.BuildCodexFilters(spots);
            RefreshProgress();

            // 첫 위치를 받기 전까지는 설정 좌표를 보여 준다. 위치가 잡히면 추적이 중심을 옮긴다.
            view.CenterOnLocation(config.editorStartLatitude, config.editorStartLongitude);
            SignalReadyWhenMapIsUsable();

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
        }

        /// <summary>뒤로가기 입력을 살피고, 위치를 갱신한 뒤 방문 판정을 돌린다.</summary>
        private void Update()
        {
            HandleBackKey();

            if (locationProvider == null || view == null)
            {
                return;
            }

            locationProvider.Tick(Time.deltaTime);
            currentLocation = locationProvider.Current;

            if (!currentLocation.IsValid)
            {
                return;
            }

            // 지도 중심 추적은 뷰가 맡는다. 드래그로 풀리고 우하단 버튼으로 다시 켜진다.
            view.UpdateUserLocation(currentLocation);
            CheckVisits();
        }

        /// <summary>씬을 떠날 때 위치 서비스를 확실히 멈춘다.</summary>
        private void OnDestroy()
        {
            if (locationProvider != null)
            {
                locationProvider.Stop();
            }
        }

#if !UNITY_EDITOR
        // 안드로이드는 뒤로가기를 누르면 플레이어가 곧바로 종료하려 든다. 확인 창을 먼저 띄우려고 가로챈다.
        // 에디터에서는 Play 종료까지 막아 버리므로 걸지 않는다.
        /// <summary>종료 가로채기를 건다.</summary>
        private void OnEnable()
        {
            Application.wantsToQuit += HandleWantsToQuit;
        }

        /// <summary>종료 가로채기를 푼다.</summary>
        private void OnDisable()
        {
            Application.wantsToQuit -= HandleWantsToQuit;
        }

        /// <summary>확인 창을 아직 통과하지 않았으면 종료를 막고 확인 창을 띄운다.</summary>
        private bool HandleWantsToQuit()
        {
            if (quitConfirmed || view == null)
            {
                return true;
            }

            view.ShowQuitDialog();
            return false;
        }
#endif

        /// <summary>
        /// 뒤로가기(안드로이드 Back = Escape) 처리.
        /// 종료 확인 창이 떠 있으면 닫고, 아니면 확인 창을 띄운다.
        /// </summary>
        private void HandleBackKey()
        {
            if (view == null || !WasBackPressedThisFrame())
            {
                return;
            }

            if (view.IsQuitDialogVisible)
            {
                view.HideQuitDialog();
                return;
            }

            view.ShowQuitDialog();
        }

        /// <summary>이번 프레임에 뒤로가기가 눌렸는지 확인한다. 입력 시스템 유무에 따라 읽는 곳이 다르다.</summary>
        private static bool WasBackPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        /// <summary>종료 확인 창에서 `확인`을 눌렀을 때.</summary>
        private void QuitApp()
        {
            quitConfirmed = true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// 로딩 화면을 닫아도 되는 시점을 알린다.
        /// 지도를 쓸 수 있으면 첫 타일이 그려질 때, 쓸 수 없는 구성이면 즉시 알린다.
        /// </summary>
        private void SignalReadyWhenMapIsUsable()
        {
            if (!view.IsMapAvailable || view.IsMapRendered)
            {
                AppReadySignal.RaiseMapReady();
                return;
            }

            view.MapRendered += AppReadySignal.RaiseMapReady;
        }

        /// <summary>
        /// 지도 설정을 읽는다. 인스펙터 에셋(MapConfig.asset)이 있으면 그것을 쓰고, 없으면 map_config.json 으로 넘어간다.
        /// </summary>
        private bool LoadConfig()
        {
            MapConfigAsset settings = Resources.Load<MapConfigAsset>(ConfigAssetResourcePath);
            if (settings != null)
            {
                config = settings.ToMapConfig();
            }
            else
            {
                TextAsset configJson = Resources.Load<TextAsset>(ConfigResourcePath);
                if (configJson == null)
                {
                    Debug.LogError("[PixelRoad] 지도 설정을 찾지 못했습니다. Resources/"
                        + ConfigAssetResourcePath + ".asset 또는 Resources/" + ConfigResourcePath + ".json 이 필요합니다.");
                    return false;
                }

                config = JsonUtility.FromJson<MapConfig>(configJson.text);
            }

            if (config == null || config.bounds == null || !config.bounds.IsValid())
            {
                Debug.LogError("[PixelRoad] 지도 설정의 좌표 범위가 올바르지 않습니다(북위 > 남위, 동경 > 서경).");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 설정과 랜드마크 JSON을 읽어 스팟 상태와 공간 인덱스를 만든다. 하나라도 없으면 false를 돌려준다.
        /// </summary>
        private bool LoadData()
        {
            if (!LoadConfig())
            {
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
                // 아이콘 키는 여기서 한 번만 해석해 SpotDefinition에 담아 둔다.
                // 조회 대상이 category 종류 수뿐이라 파싱 동안만 쓰고 버려도 비용이 없다.
                SpotIconLibrary iconKeySource = new SpotIconLibrary(
                    config.spotIconResourceFolder,
                    config.defaultSpotIconName,
                    config.placeholderThumbnailName);
                definitions = LandmarkJsonParser.Parse(
                    landmarksAsset.text,
                    config.defaultUnlockRadiusMeters,
                    iconKeySource.ResolveIconKey);
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

        /// <summary>
        /// 현재 위치 주변 스팟을 훑어 반경 안에 들어온 곳을 방문 처리한다. 처음 해금된 스팟은 카드로 보여 준다.
        /// </summary>
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

        /// <summary>마커를 눌렀을 때 해당 스팟 카드를 연다.</summary>
        private void SelectSpot(SpotRuntimeState state)
        {
            view.SelectSpot(state, currentLocation);
        }

        /// <summary>
        /// GNB 탭 처리. 현재는 지도와 도감만 동작한다.
        /// AI 탐험 리포트와 AR은 표시 상태만 관리하고 화면 이동은 아직 없다.
        /// </summary>
        private void HandleGnbTabSelected(GnbTab tab)
        {
            switch (tab)
            {
                case GnbTab.Map:
                    view.SetCodexVisible(false);
                    break;

                case GnbTab.Codex:
                    view.SetCodexVisible(true);
                    break;

                case GnbTab.Ar:
                    view.OnClickARBtn();
                    break;
            }
        }

        private void OnARRequested()
        {
            if (!currentLocation.IsValid)
            {
                view.SetLocationStatus("GPS 위치가 필요합니다");
                return;
            }

            ARConfig arConfig = ARSceneLauncher.LoadConfig();
            // ARScene은 arDisplayRadiusMeters + 랜드마크 자신의 방문 반경까지 벗어나야 핀을 숨기므로,
            // 넘겨줄 후보 목록도 그만큼 넉넉하게 조회해야 경계에 걸친 랜드마크가 누락되지 않는다.
            // unlockQueryRadiusMeters는 전체 랜드마크 중 가장 큰 방문 반경(이상)이라 상한으로 쓸 수 있다.
            List<SpotRuntimeState> nearby = spatialIndex.Query(
                currentLocation.Latitude,
                currentLocation.Longitude,
                arConfig.arDisplayRadiusMeters + unlockQueryRadiusMeters);
            StartCoroutine(ARSceneLauncher.LoadARScene(nearby, currentLocation));
        }

        /// <summary>해금 개수를 다시 세어 진행도 표시와 리포트 탭 상태를 갱신한다.</summary>
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
            view.SetReportTabState(
                ReportStateStore.IsReportAvailable(unlocked),
                ReportStateStore.HasPendingUpdate(unlocked));
        }
    }
}
