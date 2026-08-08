# Pixel Road Data Sources

## Demo data included in this repository

- `Assets/Resources/PixelRoad/spots.csv` contains manually curated demo points in Gangnam, Munjeong, and Bupyeong.
- `Assets/Resources/PixelRoad/Icons/` holds optional spot marker sprites. Names are matched against the CSV `icon` and `category` columns; see the folder README for the fallback order.

### Static PNG maps removed on 2026-08-08

The bundled static PNG maps under `Assets/Resources/PixelRoad/Maps/` were deleted and the runtime no longer loads a map image. The live vector map is now the only map surface, and `mapImageResourcePath` was removed from `map_config.json`. The generation records below are kept for provenance of the deleted files.

When no live map is available, the app now shows an on-screen notice instead of a fallback image, keeps the codex usable, and hides spot markers because no projection exists. See `docs/CONTEST_COMPLIANCE.md` for the effect on offline judging.

## Curated Munjeong and Bupyeong landmarks

Four landmarks near Munjeong Station and Bupyeong Station were added on 2026-08-07:

- Garden Five Life: OpenStreetMap way `170052586`; the facility and address were cross-checked against the [official Songpa-gu tourism page](https://www.songpa.go.kr/culture/detailInfo.do?key=5111&rcpp=6&resrceCd=TR0180-1002536&sc1=TR0180).
- Seoul Eastern District Court: OpenStreetMap way `471318814`; its current address and proximity to Munjeong Station were cross-checked against the [official court location page](https://sldongbu.scourt.go.kr/info/location/new/LocationViewAction.work?bub_cd=000211).
- Bupyeong Modoo Mall: OpenStreetMap node `4636778093`; the name, location at Bupyeong Station, and facility description were cross-checked against [VISITKOREA](https://english.visitkorea.or.kr/svc/whereToGo/locIntrdn/rgnContentsView.do?vcontsId=70941).
- Bupyeong Culture Street: OpenStreetMap way `457217779`; its current market listing and role in the shopping district were cross-checked against the [official Bupyeong-gu market guide](https://www.icbp.go.kr/main/life/economy/market.jsp) and [walking-tour guide](https://www.icbp.go.kr/tour/info/course_shopping.jsp).

Coordinates were manually retrieved from OpenStreetMap through Nominatim on 2026-08-07 and are stored directly in the CSV; the application does not call a geocoding service at runtime. The Bupyeong Modoo Mall point deliberately uses its accessible Exit 18 as the unlock anchor because the underground complex has no single surface center. OpenStreetMap attribution is already shown in the map UI and documented below. Since the static PNG maps were removed, every point is shown geographically only while the live vector map is available. All points remain listed in the codex and can still be unlocked by real geographic distance.

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

## Live vector-map validation and submission decision

- OSM Shortbread-based vector data is the current technical-validation target for viewport loading, rendering, caching, and smooth/pixel output tests.
- Development endpoint: `https://vector.openstreetmap.org/shortbread_v1/{z}/{x}/{y}.mvt`.
- Service policy reviewed: `https://operations.osmfoundation.org/policies/vector/` (2026-08-07).
- Data licence/attribution: `https://www.openstreetmap.org/copyright` (ODbL; `© OpenStreetMap contributors`).
- Runtime implementation: visible tiles only, center-priority queue, cancellation after a tile leaves the viewport, at most four concurrent requests by default, HTTP expiry/ETag/Last-Modified-aware disk cache, and no background prefetch or offline-download action.
- The renderer selects only label-free land, site, water, road, rail, and boundary layers. Labels, place names, and provider POI symbols are not rendered.
- Smooth mode renders the vector map to a map-only full-resolution RenderTexture. Pixel mode renders that same map layer to a reduced RenderTexture with point sampling. Markers, attribution, and the rest of the UI stay outside both outputs.
- This validation target is not the approved contest submission provider. The production provider, endpoint, service plan, cache policy, and required attribution remain undecided until the official contest rules and provider terms are available.
- The MVT/PBF decoder, triangulation, mesh builder, tile selection, and cache are implemented in project source; no external vector-tile decoding or rendering package was added.
- `allowLiveVectorMapInRelease` remains `false`, and non-development builds compile out the live requester unless `PIXELROAD_LIVE_VECTOR_MAP` is also deliberately defined. Both release gates must be approved for live submission use.
- The static PNG fallback was removed on 2026-08-08, so there is no offline map path. If contest rules disallow external APIs or an internet-dependent demo, a map source must be re-added before submission.
- Do not use `tile.openstreetmap.org` or Overpass as a drag-driven production tile backend.

Before approving a submission provider, record:

- Provider and dataset/style name and version
- Endpoint and access date, without committing secrets
- Mobile-app, contest-distribution, caching, retention, quota, and rate-limit terms
- Geographic coverage and offline/failure behavior
- Required OpenStreetMap, provider, renderer, and dataset attribution
- Privacy impact when tile requests follow the user's location

Required attribution checklist:

- OpenStreetMap contributors
- Selected provider name
- Any renderer, style, or tile dataset required by the selected provider

See `docs/CONTEST_COMPLIANCE.md` for the complete review gates. The official contest rules are not currently present in this repository, so this provider decision remains blocked.

## BBox coordinate contract

`map_config.json` still requires a valid `bounds` block, but it no longer describes a map image:

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

It should cover the area the spots span. The app uses its center latitude to size the unlock spatial-index grid and refuses to start if the block is invalid.

Marker placement now comes from the live vector map's slippy-map projection. Unlocking uses real geographic distance, not pixel distance.
