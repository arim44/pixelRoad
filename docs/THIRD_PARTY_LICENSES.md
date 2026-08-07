# Third-party Licenses

## Current repository assets

- `Assets/Resources/PixelRoad/Maps/gyeongbokgung_demo.png` is generated specifically for this project and is not based on third-party map imagery.
- `Assets/Resources/PixelRoad/Maps/gyeongbokgung_osm_label_free.png` and `Assets/Resources/PixelRoad/Maps/gangnam_osm_label_free.png` are derived from OpenStreetMap data fetched through the Overpass API. Their generation records are in `docs/DATA_SOURCES.md`.
- The included CSV descriptions are manually written. Four Munjeong/Bupyeong landmark coordinates were manually curated from OpenStreetMap; their element identifiers and verification sources are recorded in `docs/DATA_SOURCES.md`.

## Included font

Font: Galmuri11.

- Project: https://github.com/quiple/galmuri
- License: SIL Open Font License 1.1
- Included file: `Assets/Resources/PixelRoad/Fonts/Galmuri11.ttf`
- License file: `Assets/Resources/PixelRoad/Fonts/Galmuri_OFL.md`

## Included TMP default resources

Unity TextMeshPro essential/default resources were imported so TMP has valid settings assets in clean checkouts.

- Included path: `Assets/TextMesh Pro/Resources`
- Included default font: Inter Regular
- License file: `Assets/TextMesh Pro/Resources/Fonts & Materials/Inter-LICENSE.txt`
- Included fallback font: Liberation Sans
- License file: `Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt`
- Included sprite sample: EmojiOne
- Attribution file: `Assets/TextMesh Pro/Sprites/EmojiOne Attribution.txt`

## Current and planned OSM-based maps

The repository already includes OSM-derived static map PNGs. Their required attribution is:

- © OpenStreetMap contributors

OSM Shortbread-based vector data is used only by the current Editor/development validation configuration; it is not yet the approved contest submission source or provider. The project includes its own MVT/PBF decoder and mesh renderer and did not add a third-party vector-map runtime library. Keep the current static PNG as the offline and rules-review fallback.

When a live vector-tile provider, style, dataset, renderer, or decoding library is selected, add its exact license and attribution text here before it is included in the submission. No future vector-tile library is listed as an included dependency by this section.

See `docs/CONTEST_COMPLIANCE.md` for the required approval gates.
