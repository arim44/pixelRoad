# Pixel Road Data Sources

## Demo data included in this repository

- `Assets/Resources/PixelRoad/Maps/gyeongbokgung_demo.png` is a hand-made, label-free schematic placeholder map generated for development. It is not derived from OpenStreetMap or another external map provider.
- `Assets/Resources/PixelRoad/Maps/gyeongbokgung_osm_label_free.png` is a label-free static map rendered from OpenStreetMap data via Overpass API. It contains roads, railways, water, green areas, and land-use polygons, but no text labels or POI icons.
- `Assets/Resources/PixelRoad/spots.csv` contains manually written sample points around Gyeongbokgung for MVP testing.

## Generated OSM map record

- Output file: `Assets/Resources/PixelRoad/Maps/gyeongbokgung_osm_label_free.png`
- Renderer script: `tools/render_label_free_osm_map.py`
- Data source: OpenStreetMap data fetched through Overpass API
- Generated date: 2026-06-29
- BBox: north `37.6700`, south `37.4900`, west `126.8600`, east `127.0900`
- Projection: WebMercator
- Visual rule: no map text labels, no provider POI icons
- Attribution: © OpenStreetMap contributors

## Generated Gangnam/Yeoksam OSM map record

- Output file: `Assets/Resources/PixelRoad/Maps/gangnam_osm_label_free.png`
- Renderer script: `tools/render_label_free_osm_map.py`
- Data source: OpenStreetMap data fetched through Overpass API
- Generated date: 2026-06-29
- Center: latitude `37.4969698129663`, longitude `127.039093501609`
- Radius target: approximately 10km
- BBox: north `37.5868009304654`, south `37.4071386954672`, west `126.92586845075705`, east `127.15231855246095`
- Projection: WebMercator
- Visual rule: no map text labels, no provider POI icons
- Attribution: © OpenStreetMap contributors

## Recommended production map workflow

For the contest demo, generate a label-free static map PNG from an OSM-based provider that explicitly allows static image storage. Do not bulk-download tiles from `tile.openstreetmap.org`.

Recommended option:

- Geoapify Static Maps API, with a custom no-label/no-POI style.
- Record the API URL, creation date, style settings, bounding box, and attribution here.

Required attribution checklist:

- OpenStreetMap contributors
- Provider name, for example Geoapify
- Any renderer or tile dataset required by the selected provider, for example OpenMapTiles if applicable

## BBox coordinate contract

The map image is treated as a north-up WebMercator image. `map_config.json` must include the exact latitude/longitude bounds of the PNG:

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

Marker placement uses BBox/WebMercator. Unlocking uses real geographic distance, not pixel distance.
