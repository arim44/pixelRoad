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

        /// <summary>AR 조건을 다시 계산할 최소 이동 거리(m). 이보다 덜 움직였으면 결과가 같다고 본다.</summary>
        private const float ArRecheckDistanceMeters = 10f;

        /// <summary>못 쓰는 탭을 눌렀을 때 띄우는 안내가 저절로 사라지기까지의 시간(초).</summary>
        private const float BlockedNoticeSeconds = 3f;

        /// <summary>AR 탭이 잠겨 있을 때 눌렀을 경우의 안내 문구.</summary>
        private const string ArBlockedMessage = "랜드마크 근처에서 사용할 수 있습니다";

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

        /// <summary>AR 표시 반경. 랜드마크마다 여기에 그 랜드마크의 방문 반경을 더한 값이 AR 허용 반경이다.</summary>
        private float arDisplayRadiusMeters;

        /// <summary>리포트 요청이 도는 중인지. 해금이 연달아 일어나도 요청이 겹치지 않게 막는다.</summary>
        private Coroutine reportRequestRoutine;

        /// <summary>요청을 보낸 시점의 해금 개수. 응답이 오면 이 값으로 저장한다.</summary>
        private int requestedUnlockCount;

        /// <summary>마지막으로 AR 조건을 계산한 위치와 그때의 선택 스팟. 같은 상황이면 다시 계산하지 않는다.</summary>
        private double lastArCheckLatitude;
        private double lastArCheckLongitude;
        private bool hasCheckedArOnce;
        private SpotRuntimeState lastArCheckSelection;

        /// <summary>안내 문구를 지우는 타이머. 연달아 누르면 이전 것을 멈추고 다시 건다.</summary>
        private Coroutine blockedNoticeRoutine;

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
            view.GnbTabBlocked += HandleGnbTabBlocked;
            view.SpotFocusRequested += FocusOnSpot;
            view.ReportRetryRequested += RequestReport;

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

            // 위치를 아직 못 받은 동안에는 AR 조건을 판단할 수 없으므로 잠가 둔다.
            view.SetArTabAvailable(false);
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
            UpdateArAvailability();
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

            // 해금 알림이 떠 있으면 그것부터 닫는다. 알림을 보다가 앱이 꺼지면 곤란하다.
            if (view.IsUnlockDialogVisible)
            {
                view.DismissUnlockDialog();
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
            arDisplayRadiusMeters = ARSceneLauncher.LoadConfig().arDisplayRadiusMeters;
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

                    // 지도·도감·리포트 어느 화면을 보고 있어도 알리려면 배너만으로는 부족하다.
                    // 알림 창은 캔버스 맨 위로 올라오고, `확인`을 누르면 선택된 배너는 그대로 남는다.
                    view.ShowUnlockDialog(state.Definition);
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
        /// GNB 탭 처리. 지도·도감·리포트는 패널을 바꾸고, AR만 별도 씬으로 넘어간다.
        /// </summary>
        private void HandleGnbTabSelected(GnbTab tab)
        {
            switch (tab)
            {
                case GnbTab.Map:
                    view.SetCodexVisible(false);
                    view.SetReportVisible(false);
                    break;

                case GnbTab.Codex:
                    view.SetCodexVisible(true);
                    break;

                case GnbTab.Report:
                    OpenReport();
                    break;

                case GnbTab.Ar:
                    // AR 탭은 도감·리포트를 덮은 채로 씬을 넘기지 않도록 먼저 정리한다.
                    view.SetCodexVisible(false);
                    view.SetReportVisible(false);
                    view.OnClickARBtn();
                    break;
            }
        }

        /// <summary>
        /// 못 쓰는 탭을 눌렀을 때. 왜 못 쓰는지 지도 위 안내 문구로 알린다.
        /// 회색 탭이 아무 반응도 없으면 고장으로 보이기 때문이다.
        /// </summary>
        private void HandleGnbTabBlocked(GnbTab tab)
        {
            if (tab != GnbTab.Ar)
            {
                return;
            }

            ShowBlockedNotice(ArBlockedMessage);
        }

        /// <summary>안내 문구를 띄우고 정해진 시간 뒤에 지운다.</summary>
        private void ShowBlockedNotice(string message)
        {
            if (blockedNoticeRoutine != null)
            {
                StopCoroutine(blockedNoticeRoutine);
            }

            view.SetLocationStatus(message);
            blockedNoticeRoutine = StartCoroutine(ClearBlockedNoticeAfter(message));
        }

        /// <summary>
        /// 안내 문구를 지운다. 그사이 다른 문구로 바뀌었으면 남의 것을 지우지 않도록 그대로 둔다.
        /// </summary>
        private IEnumerator ClearBlockedNoticeAfter(string message)
        {
            yield return new WaitForSeconds(BlockedNoticeSeconds);
            blockedNoticeRoutine = null;
            if (view.CurrentLocationStatus == message)
            {
                view.ClearLocationStatus();
            }
        }

        /// <summary>
        /// 리포트 화면을 연다. 여는 순간 알림 뱃지를 끄고, 필요하면 분석을 시작한다.
        /// </summary>
        private void OpenReport()
        {
            ReportStateStore.MarkUpdateSeen();
            view.SetReportBadge(false);
            view.SetReportVisible(true);
            EnsureReportUpToDate();
        }

        private void OnARRequested()
        {
            if (!currentLocation.IsValid)
            {
                ShowBlockedNotice("GPS 위치가 필요합니다");
                return;
            }

            // ARScene은 arDisplayRadiusMeters + 랜드마크 자신의 방문 반경까지 벗어나야 핀을 숨기므로,
            // 넘겨줄 후보 목록도 그만큼 넉넉하게 조회해야 경계에 걸친 랜드마크가 누락되지 않는다.
            // unlockQueryRadiusMeters는 전체 랜드마크 중 가장 큰 방문 반경(이상)이라 상한으로 쓸 수 있다.
            List<SpotRuntimeState> nearby = spatialIndex.Query(
                currentLocation.Latitude,
                currentLocation.Longitude,
                arDisplayRadiusMeters + unlockQueryRadiusMeters);
            StartCoroutine(ARSceneLauncher.LoadARScene(nearby, currentLocation));
        }

        /// <summary>
        /// AR 탭을 지금 누를 수 있는지 다시 판단한다.
        ///
        /// AR 허용 반경은 랜드마크마다 <c>arDisplayRadiusMeters + 그 랜드마크의 visitRadius</c>다.
        /// - 허용 반경 안에 랜드마크가 하나도 없으면 AR 화면에 띄울 게 없으므로 잠근다.
        /// - 랜드마크를 고른 상태라면 그 랜드마크가 허용 반경 밖일 때 잠근다.
        ///   선택을 들고 AR로 넘어가면 대상이 없다고 판단해 AR이 바로 종료되기 때문이다.
        ///
        /// 위치가 거의 그대로이고 선택도 바뀌지 않았으면 다시 계산하지 않는다.
        /// </summary>
        private void UpdateArAvailability()
        {
            SpotRuntimeState selected = GlobalValue.SelectedSpot;
            if (hasCheckedArOnce
                && ReferenceEquals(selected, lastArCheckSelection)
                && GeoProjection.DistanceMeters(
                    lastArCheckLatitude,
                    lastArCheckLongitude,
                    currentLocation.Latitude,
                    currentLocation.Longitude) < ArRecheckDistanceMeters)
            {
                return;
            }

            hasCheckedArOnce = true;
            lastArCheckLatitude = currentLocation.Latitude;
            lastArCheckLongitude = currentLocation.Longitude;
            lastArCheckSelection = selected;

            view.SetArTabAvailable(selected != null
                ? IsWithinArRange(selected)
                : HasAnySpotWithinArRange());
        }

        /// <summary>허용 반경 안에 랜드마크가 하나라도 있는지. 찾는 즉시 멈춘다.</summary>
        private bool HasAnySpotWithinArRange()
        {
            // 격자 인덱스는 해금 반경(수십 m)에 맞춘 크기라 1.5km를 훑으면 오히려 칸 조회가 많아진다.
            // 랜드마크 수가 백 단위라 그냥 한 번 훑는 편이 싸고, 목록을 새로 만들지 않아 할당도 없다.
            for (int i = 0; i < spots.Count; i++)
            {
                if (IsWithinArRange(spots[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>해당 랜드마크가 AR 허용 반경 안에 있는지.</summary>
        private bool IsWithinArRange(SpotRuntimeState state)
        {
            double distance = GeoProjection.DistanceMeters(
                currentLocation.Latitude,
                currentLocation.Longitude,
                state.Definition.Latitude,
                state.Definition.Longitude);
            return distance <= arDisplayRadiusMeters + state.Definition.RadiusMeters;
        }

        /// <summary>
        /// 카드나 추천에서 `지도에서 보기`를 눌렀을 때. 해당 랜드마크로 지도를 옮기고 선택 상태로 만든다.
        /// </summary>
        private void FocusOnSpot(int landmarkId)
        {
            for (int i = 0; i < spots.Count; i++)
            {
                if (spots[i].Definition.LandmarkId != landmarkId)
                {
                    continue;
                }

                view.FocusOnSpot(spots[i], currentLocation);
                return;
            }

            Debug.LogWarning("[PixelRoad] 지도로 이동할 랜드마크를 찾지 못했습니다: " + landmarkId);
        }

        /// <summary>해금 개수를 다시 세어 진행도 표시와 리포트 화면을 갱신한다.</summary>
        private void RefreshProgress()
        {
            int unlocked = CountUnlocked();
            view.SetProgress(unlocked, spots.Count);
            view.SetReportSummary(unlocked, spots);
            view.SetReportBadge(ReportStateStore.HasUnreadUpdate);
            EnsureReportUpToDate();
        }

        /// <summary>현재 해금 개수.</summary>
        private int CountUnlocked()
        {
            int unlocked = 0;
            for (int i = 0; i < spots.Count; i++)
            {
                if (spots[i].IsUnlocked)
                {
                    unlocked++;
                }
            }

            return unlocked;
        }

        /// <summary>
        /// 리포트를 새로 받아야 하는지 판단해 요청하거나, 저장해 둔 결과를 그대로 보여 준다.
        ///
        /// 해금 개수가 지난번 요청 때와 같으면 분석 결과도 같다고 보고 서버를 부르지 않는다.
        /// 개수가 늘었을 때만(또는 저장된 결과가 없을 때만) 요청한다.
        /// </summary>
        private void EnsureReportUpToDate()
        {
            int unlocked = CountUnlocked();
            if (unlocked <= 0)
            {
                // 탭 자체는 항상 누를 수 있고, 여기서는 `탐험 기록이 없습니다` 화면을 보여 준다.
                view.SetReportState(ReportView.ReportScreenState.Empty, null, 0f);
                return;
            }

            if (reportRequestRoutine != null)
            {
                // 이미 요청이 돌고 있다. 응답이 오면 그때 화면을 맞춘다.
                return;
            }

            ReportResponse cached = ReportStateStore.LoadCachedReport();
            if (!ReportStateStore.NeedsRequest(unlocked) && cached != null)
            {
                view.SetReportState(ReportView.ReportScreenState.Completed, cached, 0f);
                return;
            }

            RequestReport();
        }

        /// <summary>리포트 분석을 요청한다. 재시도 버튼도 같은 경로를 쓴다.</summary>
        private void RequestReport()
        {
            if (reportRequestRoutine != null)
            {
                return;
            }

            requestedUnlockCount = CountUnlocked();
            if (requestedUnlockCount <= 0)
            {
                view.SetReportState(ReportView.ReportScreenState.Empty, null, 0f);
                return;
            }

            reportRequestRoutine = StartCoroutine(RunReportRequest());
        }

        /// <summary>
        /// 분석중 화면을 띄우고 응답을 기다린다.
        /// 성공하면 결과를 저장해 다음부터는 해금이 늘기 전까지 이 값을 그대로 쓴다.
        /// </summary>
        private IEnumerator RunReportRequest()
        {
            view.SetReportState(ReportView.ReportScreenState.Analyzing, null, 0f);

            // 첫 분석인지 갱신인지에 따라 완료 토스트를 띄울지가 갈린다. 저장 전에 미리 봐 둔다.
            bool isUpdate = ReportStateStore.LastReportedCount > 0;
            ReportResponse received = null;

            yield return ReportApiClient.Request(
                config,
                visitRepository.Records,
                spots,
                response => received = response,
                message => Debug.Log("[PixelRoad] 리포트 분석 실패: " + message));

            reportRequestRoutine = null;

            if (received == null)
            {
                view.SetReportState(ReportView.ReportScreenState.Failed, null, 0f);
                yield break;
            }

            ReportStateStore.SaveReport(requestedUnlockCount, received);
            view.SetReportState(
                ReportView.ReportScreenState.Completed,
                received,
                isUpdate ? config.reportToastAutoHideSeconds : 0f);
            view.SetReportBadge(ReportStateStore.HasUnreadUpdate);
        }
    }
}
