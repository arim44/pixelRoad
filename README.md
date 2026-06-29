# Pixel Road

Pixel Road is a Unity Android template for making a location-based pixel map app from a plain PNG map and CSV landmark data.

## Current MVP

- Android-first Unity project
- Label-free PNG map background
- BBox + WebMercator marker placement
- CSV-driven historical spot and station data
- Foreground GPS unlock within each spot radius
- Optional pixelated map rendering toggle
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

Do not bulk-download tiles from `tile.openstreetmap.org`.

Recommended production path:

1. Use an OSM-based static map provider that permits stored static images.
2. Create a custom no-label/no-POI style.
3. Export one PNG for the target BBox.
4. Record provider, URL, creation date, BBox, and attribution in `docs/DATA_SOURCES.md`.

Geoapify Static Maps is the current recommended provider candidate.

## License

Project code is MIT licensed. Third-party asset notes are tracked in:

```text
docs/THIRD_PARTY_LICENSES.md
```
