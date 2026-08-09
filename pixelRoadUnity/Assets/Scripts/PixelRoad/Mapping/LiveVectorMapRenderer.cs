#if UNITY_EDITOR || DEVELOPMENT_BUILD || PIXELROAD_LIVE_VECTOR_MAP
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using PixelRoad.Data;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace PixelRoad.Mapping
{
    /// <summary>
    /// Fetches only the currently visible MVT tiles and renders them through a map-only camera.
    /// UI and location markers remain outside the RenderTexture, so pixel mode never degrades them.
    /// </summary>
    public sealed class LiveVectorMapRenderer : MonoBehaviour, ILiveMapController
    {
        private const int VectorMapLayer = 30;
        private const int MaximumDownloadedTileBytes = 16 * 1024 * 1024;
        private const int MaximumRenderTextureDimension = 2048;

        private readonly Dictionary<TileKey, ActiveTile> activeTiles = new Dictionary<TileKey, ActiveTile>();
        private readonly Dictionary<TileKey, CachedMesh> memoryCache = new Dictionary<TileKey, CachedMesh>();
        private readonly Dictionary<TileKey, RequestContext> pendingRequests = new Dictionary<TileKey, RequestContext>();
        private readonly HashSet<TileKey> requiredKeys = new HashSet<TileKey>();
        private readonly List<TileKey> requestQueue = new List<TileKey>();
        private readonly Dictionary<TileKey, VisibleTile> visibleByKey = new Dictionary<TileKey, VisibleTile>();

        private MapConfig config;
        private RectTransform viewport;
        private RawImage output;
        private MapViewState view;
        private VectorTileProvider provider;
        private TileDiskCache diskCache;
        private Camera mapCamera;
        private GameObject cameraObject;
        private GameObject tileRoot;
        private Material tileMaterial;
        private RenderTexture renderTexture;
        private bool initialized;
        private bool pixelMode;
        private bool shuttingDown;
        private int sourceZoom = -1;
        private int originTileX;
        private int originTileY;
        private int renderWidth;
        private int renderHeight;
        private Vector2 lastViewportSize;
        private int loggedFailureCount;

        public event Action ViewChanged;
        public event Action FirstTileReady;

        public bool IsInitialized
        {
            get { return initialized; }
        }

        public bool HasRenderedTile { get; private set; }

        public string LastError { get; private set; }

        public bool Initialize(
            MapConfig mapConfig,
            RectTransform mapViewport,
            RawImage mapOutput,
            double startLatitude,
            double startLongitude)
        {
            if (initialized)
            {
                return true;
            }

            config = mapConfig;
            viewport = mapViewport;
            output = mapOutput;
            if (config == null || viewport == null || output == null)
            {
                return FailInitialization("Live vector map requires config, viewport, and output objects.");
            }

            if (!string.Equals(config.vectorTileSchema, "shortbread_v1", StringComparison.OrdinalIgnoreCase))
            {
                return FailInitialization(
                    "Unsupported vector tile schema '" + config.vectorTileSchema
                    + "'. This renderer currently requires shortbread_v1.");
            }

            provider = new VectorTileProvider(config);
            if (!provider.IsValid)
            {
                return FailInitialization(provider.ValidationError);
            }

            Shader shader = Shader.Find("PixelRoad/Vector Tile");
            if (shader == null)
            {
                return FailInitialization("PixelRoad/Vector Tile shader was not included in this build.");
            }

            view = new MapViewState(
                startLatitude,
                startLongitude,
                config.initialMapZoom,
                config.minimumMapZoom,
                config.maximumMapZoom);
            diskCache = new TileDiskCache(
                provider.ProviderId,
                Mathf.Max(1, config.maxDiskCacheMegabytes),
                config.enableDiskTileCache);

            tileMaterial = new Material(shader)
            {
                name = "PixelRoad Vector Tile Runtime Material",
                hideFlags = HideFlags.DontSave
            };

            tileRoot = new GameObject("PixelRoad Vector Tile Root");
            tileRoot.transform.position = Vector3.zero;
            tileRoot.transform.rotation = Quaternion.identity;
            tileRoot.transform.localScale = Vector3.one;

            cameraObject = new GameObject("PixelRoad Vector Map Camera");
            mapCamera = cameraObject.AddComponent<Camera>();
            mapCamera.orthographic = true;
            mapCamera.clearFlags = CameraClearFlags.SolidColor;
            mapCamera.backgroundColor = VectorTileMeshBuilder.BackgroundColor;
            mapCamera.nearClipPlane = 0.01f;
            mapCamera.farClipPlane = 30f;
            mapCamera.allowHDR = false;
            mapCamera.allowMSAA = false;
            mapCamera.useOcclusionCulling = false;
            mapCamera.cullingMask = 1 << VectorMapLayer;
            ExcludeVectorLayerFromOtherCameras();

            output.raycastTarget = false;
            output.color = Color.white;
            output.material = null;
            output.enabled = false;
            pixelMode = config.enablePixelFilter;
            initialized = true;
            Canvas.ForceUpdateCanvases();
            EnsureRenderTexture(true);
            RefreshViewAndTiles();
            return true;
        }

        public void Pan(Vector2 screenDelta)
        {
            if (!initialized)
            {
                return;
            }

            Canvas canvas = viewport.GetComponentInParent<Canvas>();
            float scaleFactor = canvas == null ? 1f : Mathf.Max(0.0001f, canvas.scaleFactor);
            view.Pan(screenDelta / scaleFactor);
            RefreshViewAndTiles();
        }

        public void ZoomAt(float factor, Vector2 screenPosition)
        {
            if (!initialized)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    viewport,
                    screenPosition,
                    null,
                    out Vector2 localPoint))
            {
                localPoint = Vector2.zero;
            }

            view.ZoomAt(factor, localPoint);
            RefreshViewAndTiles();
        }

        public void SetCenter(double latitude, double longitude)
        {
            if (!initialized)
            {
                return;
            }

            view.SetCenter(latitude, longitude);
            RefreshViewAndTiles();
        }

        public Vector2 LatLonToViewportLocal(double latitude, double longitude)
        {
            if (!initialized)
            {
                return Vector2.zero;
            }

            return view.WorldToLocal(SlippyMapProjection.LatLonToWorld(latitude, longitude));
        }

        public bool IsInViewport(double latitude, double longitude, float padding = 0f)
        {
            Vector2 local = LatLonToViewportLocal(latitude, longitude);
            Vector2 halfSize = viewport.rect.size * 0.5f;
            return Mathf.Abs(local.x) <= halfSize.x + padding && Mathf.Abs(local.y) <= halfSize.y + padding;
        }

        public void SetPixelMode(bool enabled)
        {
            if (!initialized || pixelMode == enabled)
            {
                return;
            }

            pixelMode = enabled;
            EnsureRenderTexture(true);
        }

        private void Update()
        {
            if (!initialized || shuttingDown)
            {
                return;
            }

            Vector2 viewportSize = viewport.rect.size;
            if ((viewportSize - lastViewportSize).sqrMagnitude > 0.25f)
            {
                EnsureRenderTexture(true);
                RefreshViewAndTiles();
            }
        }

        private void RefreshViewAndTiles()
        {
            if (!initialized || shuttingDown || viewport.rect.width <= 1f || viewport.rect.height <= 1f)
            {
                return;
            }

            EnsureRenderTexture(false);
            List<VisibleTile> visibleTiles = TileCoverage.Calculate(
                view,
                viewport.rect.size,
                provider.MinimumZoom,
                provider.MaximumZoom);

            sourceZoom = view.SourceZoom(provider.MinimumZoom, provider.MaximumZoom);
            int tileCount = 1 << sourceZoom;
            double centerTileX = view.CenterX * tileCount;
            double centerTileY = view.CenterY * tileCount;
            originTileX = (int)Math.Floor(centerTileX);
            originTileY = (int)Math.Floor(centerTileY);
            float centerLocalX = (float)(centerTileX - originTileX);
            float centerLocalY = (float)-(centerTileY - originTileY);
            mapCamera.transform.position = new Vector3(centerLocalX, centerLocalY, -10f);
            mapCamera.transform.rotation = Quaternion.identity;
            double tileDisplayPixels = MapViewState.TileSize * Math.Pow(2.0, view.Zoom - sourceZoom);
            mapCamera.orthographicSize = (float)(viewport.rect.height / (2.0 * tileDisplayPixels));

            requiredKeys.Clear();
            visibleByKey.Clear();
            for (int index = 0; index < visibleTiles.Count; index++)
            {
                VisibleTile visible = visibleTiles[index];
                requiredKeys.Add(visible.Key);
                visibleByKey[visible.Key] = visible;
            }

            RemoveInvisibleTiles();
            CancelInvisibleRequests();
            requestQueue.Clear();
            for (int index = 0; index < visibleTiles.Count; index++)
            {
                VisibleTile visible = visibleTiles[index];
                if (activeTiles.TryGetValue(visible.Key, out ActiveTile active))
                {
                    PositionTile(active.GameObject.transform, visible);
                    TouchMemoryEntry(visible.Key);
                    continue;
                }

                if (memoryCache.TryGetValue(visible.Key, out CachedMesh cached))
                {
                    ActivateTile(visible, cached.Mesh);
                    cached.LastUsedFrame = Time.frameCount;
                    continue;
                }

                if (!pendingRequests.ContainsKey(visible.Key))
                {
                    requestQueue.Add(visible.Key);
                }
            }

            lastViewportSize = viewport.rect.size;
            PumpRequestQueue();
            ViewChanged?.Invoke();
        }

        private void RemoveInvisibleTiles()
        {
            List<TileKey> remove = null;
            foreach (KeyValuePair<TileKey, ActiveTile> pair in activeTiles)
            {
                if (requiredKeys.Contains(pair.Key))
                {
                    continue;
                }

                if (remove == null)
                {
                    remove = new List<TileKey>();
                }

                remove.Add(pair.Key);
            }

            if (remove == null)
            {
                return;
            }

            for (int index = 0; index < remove.Count; index++)
            {
                TileKey key = remove[index];
                Destroy(activeTiles[key].GameObject);
                activeTiles.Remove(key);
            }
        }

        private void CancelInvisibleRequests()
        {
            foreach (KeyValuePair<TileKey, RequestContext> pair in pendingRequests)
            {
                if (requiredKeys.Contains(pair.Key) || pair.Value.Cancelled)
                {
                    continue;
                }

                pair.Value.Cancelled = true;
                pair.Value.Request?.Abort();
            }
        }

        private void PumpRequestQueue()
        {
            if (shuttingDown || !initialized)
            {
                return;
            }

            int maximumConcurrent = Mathf.Clamp(config.maxConcurrentTileRequests, 1, 8);
            while (pendingRequests.Count < maximumConcurrent && requestQueue.Count > 0)
            {
                TileKey key = requestQueue[0];
                requestQueue.RemoveAt(0);
                if (!requiredKeys.Contains(key)
                    || activeTiles.ContainsKey(key)
                    || memoryCache.ContainsKey(key)
                    || pendingRequests.ContainsKey(key))
                {
                    continue;
                }

                RequestContext context = new RequestContext();
                pendingRequests.Add(key, context);
                StartCoroutine(RequestTile(key, context));
            }
        }

        private IEnumerator RequestTile(TileKey key, RequestContext context)
        {
            long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            TileCacheEntry cachedEntry = null;
            bool hasCachedEntry = diskCache.TryRead(key, out cachedEntry);
            bool freshCache = hasCachedEntry && cachedEntry.ExpiresUnix > nowUnix;
            byte[] tileBytes = freshCache ? cachedEntry.Data : null;
            TileCacheEntry cacheToWrite = null;

            if (!freshCache && !context.Cancelled)
            {
                if (!provider.TryBuildTileUrl(key, out string url, out string urlError))
                {
                    RecordFailure(key, urlError);
                }
                else
                {
                    using (UnityWebRequest request = UnityWebRequest.Get(url))
                    {
                        context.Request = request;
                        request.timeout = 20;
                        request.SetRequestHeader(VectorTileProvider.RequestedWithHeaderName, provider.RequestedWithHeaderValue);
                        request.SetRequestHeader("Accept", "application/vnd.mapbox-vector-tile, application/x-protobuf");
                        if (hasCachedEntry)
                        {
                            if (!string.IsNullOrEmpty(cachedEntry.ETag))
                            {
                                request.SetRequestHeader("If-None-Match", cachedEntry.ETag);
                            }

                            if (!string.IsNullOrEmpty(cachedEntry.LastModified))
                            {
                                request.SetRequestHeader("If-Modified-Since", cachedEntry.LastModified);
                            }
                        }

                        yield return request.SendWebRequest();
                        context.Request = null;
                        if (!context.Cancelled)
                        {
                            IDictionary<string, string> headers = request.GetResponseHeaders();
                            VectorTileCacheMetadata metadata = VectorTileProvider.ParseResponseCacheHeaders(
                                headers,
                                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                            if (request.responseCode == 304 && hasCachedEntry)
                            {
                                tileBytes = cachedEntry.Data;
                                cacheToWrite = new TileCacheEntry
                                {
                                    Data = tileBytes,
                                    ExpiresUnix = metadata.ExpiresUnixSeconds,
                                    ETag = string.IsNullOrEmpty(metadata.ETag) ? cachedEntry.ETag : metadata.ETag,
                                    LastModified = string.IsNullOrEmpty(metadata.LastModified)
                                        ? cachedEntry.LastModified
                                        : metadata.LastModified
                                };
                            }
                            else if (request.result == UnityWebRequest.Result.Success
                                     && request.responseCode >= 200
                                     && request.responseCode < 300)
                            {
                                byte[] responseData = request.downloadHandler.data;
                                if (responseData != null && responseData.Length <= MaximumDownloadedTileBytes)
                                {
                                    tileBytes = responseData;
                                    cacheToWrite = new TileCacheEntry
                                    {
                                        Data = tileBytes,
                                        ExpiresUnix = metadata.ExpiresUnixSeconds,
                                        ETag = metadata.ETag,
                                        LastModified = metadata.LastModified
                                    };
                                }
                                else
                                {
                                    RecordFailure(key, "Downloaded tile exceeded the 16 MiB safety limit.");
                                }
                            }
                            else if (hasCachedEntry)
                            {
                                // A stale response is an intentional offline fallback. It is
                                // not promoted to fresh cache unless the server revalidates it.
                                tileBytes = cachedEntry.Data;
                            }
                            else
                            {
                                RecordFailure(key, request.error ?? ("HTTP " + request.responseCode));
                            }
                        }
                    }
                }
            }

            if (context.Cancelled || shuttingDown || tileBytes == null)
            {
                CompleteRequest(key);
                yield break;
            }

            Task<VectorTileMeshData> decodeTask = Task.Run(() =>
            {
                MvtTile tile = MvtDecoder.Decode(tileBytes, VectorTileMeshBuilder.CreateSupportedLayerFilter());
                return VectorTileMeshBuilder.Build(tile);
            });
            while (!decodeTask.IsCompleted && !shuttingDown)
            {
                yield return null;
            }

            if (shuttingDown)
            {
                CompleteRequest(key);
                yield break;
            }

            if (decodeTask.IsFaulted || decodeTask.IsCanceled)
            {
                Exception error = decodeTask.Exception == null
                    ? new Exception("Vector tile decoding was cancelled.")
                    : decodeTask.Exception.GetBaseException();
                RecordFailure(key, error.Message);
                if (freshCache)
                {
                    diskCache.Remove(key);
                }

                CompleteRequest(key);
                if (freshCache && requiredKeys.Contains(key))
                {
                    requestQueue.Insert(0, key);
                    PumpRequestQueue();
                }

                yield break;
            }

            VectorTileMeshData meshData = decodeTask.Result;
            Mesh mesh = CreateMesh(key, meshData);
            memoryCache[key] = new CachedMesh(mesh, Time.frameCount);
            if (cacheToWrite != null)
            {
                diskCache.Write(key, cacheToWrite);
            }

            TrimMemoryCache();
            if (requiredKeys.Contains(key) && visibleByKey.TryGetValue(key, out VisibleTile visible))
            {
                ActivateTile(visible, mesh);
            }

            CompleteRequest(key);
        }

        private void CompleteRequest(TileKey key)
        {
            pendingRequests.Remove(key);
            if (!shuttingDown)
            {
                PumpRequestQueue();
            }
        }

        private Mesh CreateMesh(TileKey key, VectorTileMeshData data)
        {
            Mesh mesh = new Mesh
            {
                name = "VectorTile " + key,
                indexFormat = data.Vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16,
                hideFlags = HideFlags.DontSave
            };
            mesh.vertices = data.Vertices;
            mesh.colors32 = data.Colors;
            mesh.uv = data.TileUvs;
            mesh.triangles = data.Triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private void ActivateTile(VisibleTile visible, Mesh mesh)
        {
            if (activeTiles.ContainsKey(visible.Key))
            {
                return;
            }

            GameObject tileObject = new GameObject("Tile " + visible.Key);
            tileObject.layer = VectorMapLayer;
            tileObject.transform.SetParent(tileRoot.transform, false);
            MeshFilter filter = tileObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = tileObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = tileMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            PositionTile(tileObject.transform, visible);
            activeTiles[visible.Key] = new ActiveTile(tileObject);

            if (!HasRenderedTile && mesh.vertexCount > 0)
            {
                HasRenderedTile = true;
                output.enabled = true;
                FirstTileReady?.Invoke();
            }
        }

        private void PositionTile(Transform tileTransform, VisibleTile visible)
        {
            tileTransform.localPosition = new Vector3(
                visible.DisplayX - originTileX,
                -(visible.DisplayY - originTileY),
                0f);
        }

        private void TouchMemoryEntry(TileKey key)
        {
            if (memoryCache.TryGetValue(key, out CachedMesh cached))
            {
                cached.LastUsedFrame = Time.frameCount;
            }
        }

        private void TrimMemoryCache()
        {
            int maximum = Mathf.Max(8, config.maxMemoryTileCount);
            while (memoryCache.Count > maximum)
            {
                TileKey candidate = default;
                CachedMesh candidateEntry = null;
                foreach (KeyValuePair<TileKey, CachedMesh> pair in memoryCache)
                {
                    if (activeTiles.ContainsKey(pair.Key) || pendingRequests.ContainsKey(pair.Key))
                    {
                        continue;
                    }

                    if (candidateEntry == null || pair.Value.LastUsedFrame < candidateEntry.LastUsedFrame)
                    {
                        candidate = pair.Key;
                        candidateEntry = pair.Value;
                    }
                }

                if (candidateEntry == null)
                {
                    return;
                }

                memoryCache.Remove(candidate);
                Destroy(candidateEntry.Mesh);
            }
        }

        private void EnsureRenderTexture(bool force)
        {
            if (viewport == null || output == null || mapCamera == null)
            {
                return;
            }

            Vector3[] corners = new Vector3[4];
            viewport.GetWorldCorners(corners);
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            int fullWidth = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(topRight.x - bottomLeft.x)));
            int fullHeight = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(topRight.y - bottomLeft.y)));
            int maximumDimension = Mathf.Min(SystemInfo.maxTextureSize, MaximumRenderTextureDimension);
            float fitScale = Mathf.Min(1f, maximumDimension / (float)Mathf.Max(fullWidth, fullHeight));
            fullWidth = Mathf.Max(1, Mathf.RoundToInt(fullWidth * fitScale));
            fullHeight = Mathf.Max(1, Mathf.RoundToInt(fullHeight * fitScale));

            int blockSize = pixelMode ? Mathf.Max(1, config.pixelBlockSize) : 1;
            int width = Mathf.Max(64, Mathf.CeilToInt(fullWidth / (float)blockSize));
            int height = Mathf.Max(64, Mathf.CeilToInt(fullHeight / (float)blockSize));
            if (!force && renderTexture != null && renderWidth == width && renderHeight == height)
            {
                return;
            }

            RenderTexture previous = renderTexture;
            renderTexture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
            {
                name = pixelMode ? "PixelRoad Map Pixel RT" : "PixelRoad Map Smooth RT",
                filterMode = pixelMode ? FilterMode.Point : FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false,
                hideFlags = HideFlags.DontSave
            };
            renderTexture.Create();
            renderWidth = width;
            renderHeight = height;
            mapCamera.targetTexture = renderTexture;
            output.texture = renderTexture;
            output.uvRect = new Rect(0f, 0f, 1f, 1f);
            if (previous != null)
            {
                previous.Release();
                Destroy(previous);
            }
        }

        private void ExcludeVectorLayerFromOtherCameras()
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            int inverseMask = ~(1 << VectorMapLayer);
            for (int index = 0; index < cameras.Length; index++)
            {
                if (cameras[index] != mapCamera)
                {
                    cameras[index].cullingMask &= inverseMask;
                }
            }
        }

        private bool FailInitialization(string message)
        {
            LastError = message;
            Debug.LogWarning("[PixelRoad] Live vector map disabled: " + message);
            return false;
        }

        private void RecordFailure(TileKey key, string message)
        {
            LastError = message;
            if (loggedFailureCount < 3)
            {
                loggedFailureCount++;
                Debug.LogWarning("[PixelRoad] Vector tile " + key + " failed: " + message);
            }
        }

        private void OnDestroy()
        {
            shuttingDown = true;
            foreach (KeyValuePair<TileKey, RequestContext> pair in pendingRequests)
            {
                pair.Value.Cancelled = true;
                pair.Value.Request?.Abort();
            }

            pendingRequests.Clear();
            foreach (KeyValuePair<TileKey, CachedMesh> pair in memoryCache)
            {
                if (pair.Value.Mesh != null)
                {
                    Destroy(pair.Value.Mesh);
                }
            }

            memoryCache.Clear();
            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }

            if (tileMaterial != null)
            {
                Destroy(tileMaterial);
            }

            if (tileRoot != null)
            {
                Destroy(tileRoot);
            }

            if (cameraObject != null)
            {
                Destroy(cameraObject);
            }
        }

        private sealed class ActiveTile
        {
            public readonly GameObject GameObject;

            public ActiveTile(GameObject gameObject)
            {
                GameObject = gameObject;
            }
        }

        private sealed class CachedMesh
        {
            public readonly Mesh Mesh;
            public int LastUsedFrame;

            public CachedMesh(Mesh mesh, int lastUsedFrame)
            {
                Mesh = mesh;
                LastUsedFrame = lastUsedFrame;
            }
        }

        private sealed class RequestContext
        {
            public UnityWebRequest Request;
            public bool Cancelled;
        }
    }
}
#endif
