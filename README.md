# Pixel Road

Pixel Road is a Unity Android template for making a location-based map app from live vector tiles or a bundled PNG fallback, plus CSV landmark data.

## Current MVP

- Android-first Unity project
- Drag-driven, label-free OSM vector-tile map in Editor/development builds
- Bundled PNG offline/submission fallback
- WebMercator marker placement independent of map resolution
- CSV-driven historical spot and station data
- Foreground GPS unlock within each spot radius
- Optional smooth/pixel map rendering toggle; markers and UI remain sharp
- TMP UI with Galmuri pixel font
- Codex screen for unlocked spots

AR/camera mode and background geofence unlocking are planned as later features.

## Unity project

Open this folder in Unity:

```text
pixelRoadUnity
```

The startup scene is:

```text
Assets/Scenes/MapScene.unity
```

`PixelRoadApp` bootstraps itself at runtime, so the scene does not need manual inspector wiring for the MVP.

## Editor test controls

In Play Mode, the Unity Editor uses a simulated GPS location instead of device GPS.

- `WASD` or arrow keys: move the simulated GPS location
- `Shift` + movement key: move faster
- The status text shows the current simulated latitude/longitude
- `editorFollowSimulatedLocation` in `map_config.json` keeps the map centered on the simulated location

## Data files

Configuration:

```text
Assets/Resources/PixelRoad/map_config.json
```

Spot CSV:

```text
Assets/Resources/PixelRoad/spots.csv
```

Map PNG:

```text
Assets/Resources/PixelRoad/Maps/gangnam_osm_label_free.png
```

The active sample PNG is a label-free OSM-based static map centered near `37.4969698129663, 127.039093501609` with an approximately 10km radius coverage.

## Live vector map

`map_config.json` enables a provider-swappable, Shortbread-compatible MVT renderer for Editor and development validation. It requests only tiles intersecting the current viewport as the user drags or zooms, cancels obsolete requests, and keeps a bounded HTTP-aware cache. Labels and provider POI symbols are excluded from the rendered layer.

The current endpoint is the OSMF Shortbread service for technical validation only. `allowLiveVectorMapInRelease` is intentionally `false`, and non-development builds compile the live requester out unless `PIXELROAD_LIVE_VECTOR_MAP` is explicitly defined. A normal release/submission build therefore keeps using the PNG until the contest rules and production provider terms are approved. Both the compile symbol and config gate must be approved before a live release.

Smooth mode uses a full-resolution map-only RenderTexture. Pixel mode uses a lower-resolution, point-sampled map RenderTexture. The attribution, spot markers, GPS marker, and controls are separate UI layers and therefore remain readable in both modes.

## Verification

The offline EditMode suite covers projection, viewport-only tile coverage, disk caching, HTTP cache policy, MVT decoding, and mesh generation. The live integration test is opt-in so ordinary test runs never contact a tile service:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.18f1\Editor\Unity.exe' `
  -batchmode -projectPath 'C:\pixelRoad\pixelRoadUnity' `
  -runTests -testPlatform PlayMode `
  -testFilter 'PixelRoad.Tests.PlayMode.ShortbreadIntegrationTests.CurrentViewport_RendersAndSwitchesPixelMode' `
  -pixelRoadRunNetworkIntegration `
  -testResults 'C:\pixelRoad\Build\ShortbreadPlayModeResults.xml'
```

`Pixel Road > Build Android Development APK` creates a live-enabled development artifact. `Pixel Road > Build Android Offline Review APK` creates a non-development review artifact with live networking compiled out by default. Neither is an approved contest submission until the official rules and metadata are signed off.

## BBox map contract

The map image is treated as a north-up WebMercator image. Put the exact map bounds in `map_config.json`:

```json
{
  "bounds": {
    "northLat": 37.6700,
    "southLat": 37.4900,
    "westLon": 126.8600,
    "eastLon": 127.0900
  }
}
```

Marker display uses these bounds. Unlock radius checks use real geographic distance.

## CSV format

```csv
id,name,description,category,latitude,longitude,radiusMeters,icon,initialUnlocked
gyeongbokgung,경복궁,조선 왕조의 법궁입니다.,history,37.579617,126.977041,50,palace,true
```

Required columns:

- `id`
- `name`
- `description`
- `category`
- `latitude`
- `longitude`

Optional columns:

- `radiusMeters`, defaults to `defaultUnlockRadiusMeters`
- `icon`
- `initialUnlocked`

## Android location

Foreground location is implemented with Unity `Input.location`.

The Android library manifest at `Assets/Plugins/Android/PixelRoadLocationPermissions.androidlib/AndroidManifest.xml` adds foreground location permissions only:

- `ACCESS_COARSE_LOCATION`
- `ACCESS_FINE_LOCATION`

`enableBackgroundUnlock` exists in config as a future switch, but native Android geofence registration is not implemented in this MVP.

## OSM-based map guidance

Do not bulk-download tiles from `tile.openstreetmap.org` or `vector.openstreetmap.org`. The runtime has no prefetch/offline-download feature and requests only the visible MVT viewport.

Recommended production path:

1. Review the official contest rules and decide whether network access is permitted.
2. Select a vector provider or self-hosted dataset whose mobile, contest, cache, and quota terms match the release.
3. Keep the no-label/no-POI style and the bundled PNG fallback.
4. Record provider, endpoint/dataset version, access date, cache terms, and attribution in `docs/DATA_SOURCES.md`.

See `docs/CONTEST_COMPLIANCE.md` before enabling live tiles in a submission build.

## License

Project code is MIT licensed. Third-party asset notes are tracked in:

```text
docs/THIRD_PARTY_LICENSES.md
```
