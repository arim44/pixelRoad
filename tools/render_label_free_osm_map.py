import argparse
import json
import math
import time
import urllib.parse
import urllib.request
from pathlib import Path

from PIL import Image, ImageDraw


DEFAULT_OVERPASS_URL = "https://overpass-api.de/api/interpreter"


def main():
    parser = argparse.ArgumentParser(description="Render a label-free OSM-based PNG map for Pixel Road.")
    parser.add_argument("--config", default="pixelRoadUnity/Assets/Resources/PixelRoad/map_config.json")
    parser.add_argument("--output", default="pixelRoadUnity/Assets/Resources/PixelRoad/Maps/gyeongbokgung_osm_label_free.png")
    parser.add_argument("--cache", default="tools/cache/gyeongbokgung_osm_label_free.json")
    parser.add_argument("--size", type=int, default=2048)
    parser.add_argument("--refresh", action="store_true")
    parser.add_argument("--grid", action="store_true")
    parser.add_argument("--endpoint", default=DEFAULT_OVERPASS_URL)
    args = parser.parse_args()

    config_path = Path(args.config)
    output_path = Path(args.output)
    cache_path = Path(args.cache)
    config = json.loads(config_path.read_text(encoding="utf-8"))
    bounds = config["bounds"]

    data = load_or_fetch_osm(bounds, cache_path, args.refresh, args.endpoint)
    image = render_map(data, bounds, args.size, args.grid)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    image.save(output_path)
    print(f"wrote {output_path.resolve()}")
    print(f"osm elements: {len(data.get('elements', []))}")


def load_or_fetch_osm(bounds, cache_path, refresh, endpoint):
    if cache_path.exists() and not refresh:
        return json.loads(cache_path.read_text(encoding="utf-8"))

    query = build_query(bounds)
    encoded = urllib.parse.urlencode({"data": query}).encode("utf-8")
    request = urllib.request.Request(
        endpoint,
        data=encoded,
        headers={"User-Agent": "PixelRoadRenderer/0.1 (label-free contest demo)"},
    )

    start = time.time()
    with urllib.request.urlopen(request, timeout=240) as response:
        raw = response.read()

    cache_path.parent.mkdir(parents=True, exist_ok=True)
    cache_path.write_bytes(raw)
    print(f"downloaded {len(raw)} bytes in {time.time() - start:.1f}s")
    return json.loads(raw.decode("utf-8"))


def build_query(bounds):
    south = bounds["southLat"]
    west = bounds["westLon"]
    north = bounds["northLat"]
    east = bounds["eastLon"]
    bbox = f"{south},{west},{north},{east}"
    return f"""[out:json][timeout:180];
(
  way["natural"="water"]({bbox});
  way["waterway"]({bbox});
  way["landuse"~"forest|grass|meadow|recreation_ground|residential|commercial|industrial|retail|cemetery|military|railway"]({bbox});
  way["leisure"~"park|garden|nature_reserve|pitch|sports_centre"]({bbox});
  way["amenity"~"university|school|hospital"]({bbox});
  way["railway"~"rail|subway|light_rail|tram"]({bbox});
  way["highway"]({bbox});
);
out tags geom;"""


def render_map(data, bounds, size, draw_grid):
    palette = {
        "background": (107, 139, 86),
        "residential": (119, 135, 98),
        "commercial": (135, 124, 94),
        "industrial": (118, 116, 103),
        "green": (80, 135, 80),
        "deep_green": (58, 111, 68),
        "water": (46, 130, 142),
        "road_shadow": (54, 48, 43),
        "major_road": (213, 179, 112),
        "minor_road": (181, 162, 116),
        "footway": (210, 200, 160),
        "rail": (49, 45, 48),
    }

    image = Image.new("RGB", (size, size), palette["background"])
    draw = ImageDraw.Draw(image)

    elements = [element for element in data.get("elements", []) if element.get("type") == "way" and "geometry" in element]

    draw_polygons(draw, elements, bounds, size, palette)
    draw_water(draw, elements, bounds, size, palette)
    draw_railways(draw, elements, bounds, size, palette)
    draw_roads(draw, elements, bounds, size, palette)
    if draw_grid:
        add_subtle_pixel_texture(draw, size)
    return image


def draw_polygons(draw, elements, bounds, size, palette):
    for element in elements:
        tags = element.get("tags", {})
        if not is_closed(element):
            continue

        fill = polygon_color(tags, palette)
        if fill is None:
            continue

        points = points_for(element, bounds, size)
        if len(points) >= 3:
            draw.polygon(points, fill=fill)


def draw_water(draw, elements, bounds, size, palette):
    for element in elements:
        tags = element.get("tags", {})
        if tags.get("natural") != "water" and "waterway" not in tags:
            continue

        points = points_for(element, bounds, size)
        if len(points) < 2:
            continue

        if is_closed(element):
            draw.polygon(points, fill=palette["water"])
        else:
            draw.line(points, fill=palette["water"], width=6, joint="curve")


def draw_railways(draw, elements, bounds, size, palette):
    for element in elements:
        tags = element.get("tags", {})
        if "railway" not in tags:
            continue

        points = points_for(element, bounds, size)
        if len(points) >= 2:
            draw.line(points, fill=palette["rail"], width=4)


def draw_roads(draw, elements, bounds, size, palette):
    road_elements = [element for element in elements if "highway" in element.get("tags", {})]
    road_elements.sort(key=road_priority)
    for element in road_elements:
        tags = element.get("tags", {})
        highway = tags.get("highway", "")
        if skip_road(highway):
            continue

        points = points_for(element, bounds, size)
        if len(points) < 2:
            continue

        width = road_width(highway)
        color = road_color(highway, palette)
        draw.line(points, fill=palette["road_shadow"], width=width + 2)
        draw.line(points, fill=color, width=width)


def polygon_color(tags, palette):
    landuse = tags.get("landuse")
    leisure = tags.get("leisure")
    amenity = tags.get("amenity")
    natural = tags.get("natural")

    if natural == "water":
        return None

    if leisure in {"park", "garden", "nature_reserve", "pitch", "sports_centre"}:
        return palette["deep_green"] if leisure in {"park", "garden", "nature_reserve"} else palette["green"]

    if landuse in {"forest", "grass", "meadow", "recreation_ground", "cemetery"}:
        return palette["green"]

    if landuse == "residential":
        return palette["residential"]

    if landuse in {"commercial", "retail"}:
        return palette["commercial"]

    if landuse in {"industrial", "military", "railway"}:
        return palette["industrial"]

    if amenity in {"university", "school", "hospital"}:
        return (128, 126, 96)

    return None


def road_width(highway):
    if highway in {"motorway", "trunk"}:
        return 10
    if highway in {"primary"}:
        return 8
    if highway in {"secondary"}:
        return 7
    if highway in {"tertiary"}:
        return 5
    if highway in {"residential", "living_street", "unclassified"}:
        return 3
    if highway in {"service"}:
        return 1
    return 2


def road_color(highway, palette):
    if highway in {"motorway", "trunk", "primary", "secondary", "tertiary"}:
        return palette["major_road"]
    if highway in {"footway", "path", "pedestrian", "cycleway", "steps"}:
        return palette["footway"]
    return palette["minor_road"]


def skip_road(highway):
    return highway in {"footway", "path", "cycleway", "steps", "bridleway", "corridor", "escape", "raceway", "track"}


def road_priority(element):
    highway = element.get("tags", {}).get("highway", "")
    order = {
        "footway": 0,
        "path": 0,
        "cycleway": 0,
        "steps": 0,
        "service": 1,
        "residential": 2,
        "unclassified": 2,
        "living_street": 2,
        "tertiary": 3,
        "secondary": 4,
        "primary": 5,
        "trunk": 6,
        "motorway": 7,
    }
    return order.get(highway, 1)


def points_for(element, bounds, size):
    points = []
    for point in element.get("geometry", []):
        points.append(project(point["lat"], point["lon"], bounds, size))
    return points


def is_closed(element):
    geometry = element.get("geometry", [])
    if len(geometry) < 4:
        return False
    return geometry[0]["lat"] == geometry[-1]["lat"] and geometry[0]["lon"] == geometry[-1]["lon"]


def project(lat, lon, bounds, size):
    west_x = lon_to_mercator(bounds["westLon"])
    east_x = lon_to_mercator(bounds["eastLon"])
    north_y = lat_to_mercator(bounds["northLat"])
    south_y = lat_to_mercator(bounds["southLat"])
    x = lon_to_mercator(lon)
    y = lat_to_mercator(lat)

    normalized_x = (x - west_x) / (east_x - west_x)
    normalized_y = (y - north_y) / (south_y - north_y)
    return (round(normalized_x * (size - 1)), round(normalized_y * (size - 1)))


def lon_to_mercator(lon):
    return math.radians(lon)


def lat_to_mercator(lat):
    clamped = max(-85.05112878, min(85.05112878, lat))
    radians = math.radians(clamped)
    return math.log(math.tan(math.pi / 4.0 + radians / 2.0))


def add_subtle_pixel_texture(draw, size):
    for value in range(0, size, 32):
        draw.line([(value, 0), (value, size)], fill=(0, 0, 0), width=1)
        draw.line([(0, value), (size, value)], fill=(0, 0, 0), width=1)


if __name__ == "__main__":
    main()
