from __future__ import annotations

import argparse
import shutil
import sys
import zipfile
from pathlib import Path


SEED = 824401
RATIOS = {
    "concrete": 0.35,
    "floor": 0.20,
    "dust": 0.20,
    "rubble": 0.15,
    "trace": 0.07,
    "metal": 0.03,
}


def parse_args() -> argparse.Namespace:
    raw = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else sys.argv[1:]
    parser = argparse.ArgumentParser()
    parser.add_argument("--stage", choices=("maps", "blender"), required=True)
    parser.add_argument("--size", type=int, default=2048)
    parser.add_argument("--asset-root", type=Path, required=True)
    parser.add_argument("--source-root", type=Path, required=True)
    parser.add_argument("--qa-root", type=Path, required=True)
    parser.add_argument("--concept", type=Path)
    args = parser.parse_args(raw)
    args.asset_root = args.asset_root.resolve()
    args.source_root = args.source_root.resolve()
    args.qa_root = args.qa_root.resolve()
    if args.concept is not None:
        args.concept = args.concept.resolve()
    return args


def smoothstep(value):
    return value * value * (3.0 - 2.0 * value)


def periodic_noise(np, size: int, frequency: int, seed: int):
    rng = np.random.default_rng(seed)
    grid = rng.random((frequency, frequency), dtype=np.float32)
    coordinate = np.arange(size, dtype=np.float32) * (frequency / size)
    lower = np.floor(coordinate).astype(np.int32)
    upper = (lower + 1) % frequency
    blend = smoothstep(coordinate - lower)
    top_left = grid[lower[:, None], lower[None, :]]
    top_right = grid[lower[:, None], upper[None, :]]
    bottom_left = grid[upper[:, None], lower[None, :]]
    bottom_right = grid[upper[:, None], upper[None, :]]
    blend_x = blend[None, :]
    blend_y = blend[:, None]
    return (
        (top_left * (1.0 - blend_x) + top_right * blend_x) * (1.0 - blend_y)
        + (bottom_left * (1.0 - blend_x) + bottom_right * blend_x) * blend_y
    ).astype(np.float32)


def fractal_noise(np, size: int, bands, seed: int):
    result = np.zeros((size, size), dtype=np.float32)
    total = 0.0
    for index, (frequency, weight) in enumerate(bands):
        result += periodic_noise(np, size, frequency, seed + index * 977) * weight
        total += weight
    result /= total
    low = float(result.min())
    high = float(result.max())
    return (result - low) / max(high - low, 1e-6)


def periodic_voronoi(np, size: int, cells: int, seed: int):
    """Return periodic basin boundaries and a stable value per nearest basin cell."""
    rng = np.random.default_rng(seed)
    jitter = rng.uniform(0.16, 0.84, (cells, cells, 2)).astype(np.float32)
    values = rng.random((cells, cells), dtype=np.float32)
    axis = np.arange(size, dtype=np.float32) * (cells / size)
    grid_y = axis[:, None]
    grid_x = axis[None, :]
    base_y = np.floor(grid_y).astype(np.int32)
    base_x = np.floor(grid_x).astype(np.int32)
    nearest = np.full((size, size), np.inf, dtype=np.float32)
    second = np.full((size, size), np.inf, dtype=np.float32)
    cell_value = np.zeros((size, size), dtype=np.float32)
    for offset_y in (-1, 0, 1):
        source_y = (base_y + offset_y) % cells
        for offset_x in (-1, 0, 1):
            source_x = (base_x + offset_x) % cells
            seed_y = base_y + offset_y + jitter[source_y, source_x, 0]
            seed_x = base_x + offset_x + jitter[source_y, source_x, 1]
            distance = (seed_y - grid_y) ** 2 + (seed_x - grid_x) ** 2
            closer = distance < nearest
            second = np.where(closer, nearest, np.minimum(second, distance))
            cell_value = np.where(closer, values[source_y, source_x], cell_value)
            nearest = np.where(closer, distance, nearest)
    gap = np.maximum(np.sqrt(second) - np.sqrt(nearest), 0.0)
    boundary = np.exp(-((gap / 0.10) ** 2)).astype(np.float32)
    return boundary, cell_value


def periodic_component(np, image):
    """Moisan-style periodic-plus-smooth decomposition."""
    height, width = image.shape[:2]
    channels = image.shape[2] if image.ndim == 3 else 1
    work = image[..., None] if image.ndim == 2 else image
    yy = np.arange(height, dtype=np.float32)[:, None]
    xx = np.arange(width, dtype=np.float32)[None, :]
    denominator = 2.0 * np.cos(2.0 * np.pi * xx / width) + 2.0 * np.cos(2.0 * np.pi * yy / height) - 4.0
    denominator[0, 0] = 1.0
    output = np.empty_like(work, dtype=np.float32)
    for channel in range(channels):
        source = work[..., channel].astype(np.float32)
        boundary = np.zeros_like(source)
        vertical_difference = source[-1, :] - source[0, :]
        horizontal_difference = source[:, -1] - source[:, 0]
        boundary[0, :] += vertical_difference
        boundary[-1, :] -= vertical_difference
        boundary[:, 0] += horizontal_difference
        boundary[:, -1] -= horizontal_difference
        smooth = np.fft.ifft2(np.fft.fft2(boundary) / denominator).real.astype(np.float32)
        output[..., channel] = source - smooth
    return output[..., 0] if image.ndim == 2 else output


def gaussian(np, array, radius: float):
    """Periodic frequency-domain Gaussian blur."""
    height, width = array.shape
    frequency_y = np.fft.fftfreq(height).astype(np.float32)[:, None]
    frequency_x = np.fft.rfftfreq(width).astype(np.float32)[None, :]
    kernel = np.exp(-2.0 * (np.pi**2) * (radius**2) * (frequency_x * frequency_x + frequency_y * frequency_y))
    transformed = np.fft.rfft2(array)
    return np.fft.irfft2(transformed * kernel, s=array.shape).real.astype(np.float32)


def pick_top(np, score, available, count: int):
    flat = score.ravel().copy()
    available_flat = available.ravel()
    flat[~available_flat] = -np.inf
    count = max(0, min(int(count), int(available_flat.sum())))
    chosen = np.zeros_like(available_flat, dtype=bool)
    if count:
        positions = np.argpartition(flat, flat.size - count)[flat.size - count :]
        chosen[positions] = True
    return chosen.reshape(score.shape)


def save_rgb(Image, np, path: Path, rgb):
    path.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(np.clip(rgb * 255.0 + 0.5, 0, 255).astype(np.uint8), mode="RGB").save(
        path, format="PNG", optimize=True
    )


def save_rgba(Image, np, path: Path, rgba):
    path.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(np.clip(rgba * 255.0 + 0.5, 0, 255).astype(np.uint8), mode="RGBA").save(
        path, format="PNG", optimize=True
    )


def make_ora(np, Image, source_root: Path, base_rgb, masks):
    temp = source_root / "_ora_layers"
    temp.mkdir(parents=True, exist_ok=True)
    merged_path = temp / "mergedimage.png"
    save_rgb(Image, np, merged_path, base_rgb)
    thumbnail_path = temp / "thumbnail.png"
    Image.open(merged_path).resize((256, 256), Image.Resampling.LANCZOS).save(thumbnail_path, format="PNG")

    layer_entries = [("Authored Seamless BaseColor", merged_path, "visible")]
    for index, name in enumerate(("concrete", "floor", "dust", "rubble", "trace", "metal")):
        gray = np.repeat(masks[name][..., None], 3, axis=2)
        rgba = np.concatenate((gray, np.ones((*gray.shape[:2], 1), dtype=np.float32)), axis=2)
        path = temp / f"mask_{index:02d}_{name}.png"
        save_rgba(Image, np, path, rgba)
        layer_entries.append((f"{name.title()} Coverage Mask", path, "hidden"))

    size = base_rgb.shape[0]
    ora_path = source_root / "Ruins_Golden_Master.ora"
    stack_layers = []
    for layer_index, (name, path, visibility) in enumerate(reversed(layer_entries)):
        stack_layers.append(
            f'<layer name="{name}" src="data/layer_{layer_index:02d}.png" visibility="{visibility}" '
            'composite-op="svg:src-over" opacity="1.0"/>'
        )
    stack_xml = (
        '<?xml version="1.0" encoding="UTF-8"?>\n'
        f'<image version="0.0.1" w="{size}" h="{size}" name="Ruins Golden Master">\n'
        '  <stack name="Ruins Golden Source">\n    '
        + "\n    ".join(stack_layers)
        + "\n  </stack>\n</image>\n"
    )
    with zipfile.ZipFile(ora_path, "w") as archive:
        archive.writestr("mimetype", "image/openraster", compress_type=zipfile.ZIP_STORED)
        archive.writestr("stack.xml", stack_xml)
        archive.write(merged_path, "mergedimage.png")
        archive.write(thumbnail_path, "Thumbnails/thumbnail.png")
        for layer_index, (_, path, _) in enumerate(reversed(layer_entries)):
            archive.write(path, f"data/layer_{layer_index:02d}.png")
    shutil.rmtree(temp)
    return ora_path


def generate_maps(args: argparse.Namespace):
    import numpy as np
    from PIL import Image

    size = args.size
    args.asset_root.mkdir(parents=True, exist_ok=True)
    args.source_root.mkdir(parents=True, exist_ok=True)
    args.qa_root.mkdir(parents=True, exist_ok=True)
    if args.concept is None or not args.concept.is_file():
        raise FileNotFoundError("--concept must point to the user-approved Ruins concept image")

    with Image.open(args.concept) as concept_image:
        concept_image = concept_image.convert("RGB")
        crop_size = min(concept_image.size)
        left = (concept_image.width - crop_size) // 2
        top = (concept_image.height - crop_size) // 2
        concept_image = concept_image.crop((left, top, left + crop_size, top + crop_size))
        concept_image = concept_image.resize((size, size), Image.Resampling.LANCZOS)
        concept = np.asarray(concept_image).astype(np.float32) / 255.0

    luminance = concept[..., 0] * 0.2126 + concept[..., 1] * 0.7152 + concept[..., 2] * 0.0722
    broad_light = gaussian(np, luminance, max(10.0, size / 8.0))
    target_luminance = np.clip(
        0.32 + (luminance - broad_light) * 0.90 + (broad_light - broad_light.mean()) * 0.06,
        0.07,
        0.68,
    )
    flattened = np.clip(concept * (target_luminance / np.maximum(luminance, 0.05))[..., None], 0.0, 1.0)
    flattened = np.clip(periodic_component(np, flattened), 0.0, 1.0)

    remix_a = fractal_noise(np, size, [(7, 0.55), (13, 0.45)], SEED + 701)
    remix_b = fractal_noise(np, size, [(9, 0.52), (17, 0.48)], SEED + 751)
    variant_b = np.roll(np.rot90(flattened, 1), (size // 5, size // 3), axis=(0, 1))
    variant_c = np.roll(np.rot90(flattened, 3), (size // 3, size // 7), axis=(0, 1))
    weight_a = 0.60 + 0.15 * remix_a
    weight_b = 0.15 + 0.10 * remix_b
    weight_c = 0.10 + 0.08 * (1.0 - remix_a)
    weight_sum = weight_a + weight_b + weight_c
    flattened = (
        flattened * weight_a[..., None]
        + variant_b * weight_b[..., None]
        + variant_c * weight_c[..., None]
    ) / weight_sum[..., None]
    flattened = np.clip(periodic_component(np, flattened), 0.0, 1.0)

    luminance = flattened[..., 0] * 0.2126 + flattened[..., 1] * 0.7152 + flattened[..., 2] * 0.0722
    blur_small = gaussian(np, luminance, max(0.8, size / 1024.0 * 1.5))
    blur_medium = gaussian(np, luminance, max(2.0, size / 1024.0 * 12.0))
    blur_large = gaussian(np, luminance, max(5.0, size / 1024.0 * 56.0))
    high_band = luminance - blur_small
    medium_band = blur_small - blur_medium
    texture_energy = gaussian(np, np.abs(high_band), max(1.0, size / 1024.0 * 5.5))
    warmth = flattened[..., 0] - flattened[..., 2]
    grayness = 1.0 - (flattened.max(axis=2) - flattened.min(axis=2))
    rustness = flattened[..., 0] - 0.62 * flattened[..., 1] - 0.38 * flattened[..., 2]
    darkness = 1.0 - luminance
    independent_macro = fractal_noise(np, size, [(9, 0.45), (17, 0.33), (29, 0.22)], SEED + 1)
    independent_macro_b = fractal_noise(np, size, [(12, 0.46), (23, 0.32), (41, 0.22)], SEED + 101)
    independent_fine = fractal_noise(np, size, [(38, 0.43), (73, 0.34), (131, 0.23)], SEED + 201)

    total = size * size
    available = np.ones((size, size), dtype=bool)
    categories = {}
    trace_boundary, trace_value = periodic_voronoi(np, size, 11, SEED + 401)
    rubble_boundary, rubble_value = periodic_voronoi(np, size, 19, SEED + 451)
    trace_gate = smoothstep(np.clip((independent_macro_b - 0.48) / 0.30, 0.0, 1.0))
    raw_scores = {
        "concrete": grayness * 0.76 + luminance * 0.42 - warmth * 0.18 + independent_macro * 0.20,
        "floor": darkness * 1.08 + grayness * 0.26 - warmth * 0.24 + independent_macro_b * 0.18,
        "rubble": texture_energy * 1.18 + rubble_boundary * (0.30 + rubble_value * 0.20) + np.abs(medium_band) * 0.50,
        "trace": rustness * 0.90 + warmth * 0.20 + texture_energy * 0.18 + independent_macro_b * 0.12,
        "metal": rustness * 1.16 + darkness * 0.16 + texture_energy * 0.16 + independent_fine * 0.10,
    }
    scores = {
        "concrete": gaussian(np, raw_scores["concrete"], max(7.0, size / 2048.0 * 28.0)),
        "floor": gaussian(np, raw_scores["floor"], max(7.0, size / 2048.0 * 27.0)),
        "rubble": gaussian(np, raw_scores["rubble"], max(1.6, size / 2048.0 * 5.0)),
        "trace": gaussian(np, raw_scores["trace"], max(2.0, size / 2048.0 * 6.0)),
        "metal": gaussian(np, raw_scores["metal"], max(1.0, size / 2048.0 * 2.5)),
    }
    for name in ("metal", "trace", "rubble", "floor", "concrete"):
        categories[name] = pick_top(np, scores[name], available, round(total * RATIOS[name]))
        available &= ~categories[name]
    categories["dust"] = available

    radius_scale = size / 2048.0
    blur_radius = {
        "concrete": 8.0,
        "floor": 8.0,
        "dust": 7.0,
        "rubble": 3.2,
        "trace": 2.2,
        "metal": 1.5,
    }
    masks = {}
    for name, binary in categories.items():
        smoothed = gaussian(np, binary.astype(np.float32), max(0.7, radius_scale * blur_radius[name]))
        masks[name] = np.clip(periodic_component(np, smoothed), 0.0, 1.0)
    mask_sum = sum(masks.values())
    masks = {name: value / np.maximum(mask_sum, 1e-6) for name, value in masks.items()}

    def hex_rgb(value: str):
        value = value.lstrip("#")
        return np.array([int(value[index : index + 2], 16) / 255.0 for index in (0, 2, 4)], dtype=np.float32)

    anchors = {
        "concrete": hex_rgb("55514A"),
        "floor": hex_rgb("393936"),
        "dust": hex_rgb("756047"),
        "rubble": hex_rgb("5B5145"),
        "trace": hex_rgb("8A682E"),
        "metal": hex_rgb("68402A"),
    }
    mixes = {
        "concrete": 0.34,
        "floor": 0.38,
        "dust": 0.31,
        "rubble": 0.25,
        "trace": 0.08,
        "metal": 0.22,
    }
    smooth_factors = {
        "concrete": 0.24,
        "floor": 0.30,
        "dust": 0.10,
        "rubble": 0.06,
        "trace": 0.14,
        "metal": 0.10,
    }
    flattened_smooth = np.stack(
        [gaussian(np, flattened[..., channel], max(1.5, size / 2048.0 * 5.0)) for channel in range(3)],
        axis=2,
    )
    base = np.zeros((size, size, 3), dtype=np.float32)
    for name in RATIOS:
        source_color = flattened * (1.0 - smooth_factors[name]) + flattened_smooth * smooth_factors[name]
        tint = source_color * (1.0 - mixes[name]) + anchors[name] * mixes[name]
        tint *= (0.91 + 0.16 * independent_macro)[..., None]
        base += masks[name][..., None] * tint
    base = np.clip(periodic_component(np, base), 0.0, 1.0)

    basin_boundary, basin_value = periodic_voronoi(np, size, 10, SEED + 501)
    material_relief = np.tanh((medium_band * 3.0 + high_band * 0.48) * 2.2)
    height_layers = {
        "concrete": 0.520 + 0.014 * independent_macro + 0.010 * np.maximum(material_relief, 0.0),
        "floor": 0.475 + 0.010 * independent_macro - 0.005 * basin_boundary,
        "dust": 0.492 + 0.020 * independent_macro + 0.005 * np.maximum(material_relief, 0.0),
        "rubble": 0.548 + 0.024 * independent_fine + 0.010 * rubble_boundary,
        "trace": 0.500 + 0.008 * trace_value + 0.003 * independent_fine,
        "metal": 0.526 + 0.010 * independent_fine + 0.003 * trace_value,
    }
    height = np.zeros((size, size), dtype=np.float32)
    for name, values in height_layers.items():
        height += masks[name] * values
    height += material_relief * 0.008 + (independent_fine - 0.5) * 0.003
    height = gaussian(np, height, max(0.85, radius_scale * 1.45))
    height = np.clip(periodic_component(np, height), 0.22, 0.82)

    delta_x = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * 0.5
    delta_y = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * 0.5
    strength = 10.0 * (size / 512.0)
    normal_x = -delta_x * strength
    normal_y = delta_y * strength
    normal_z = np.ones_like(normal_x)
    normal_length = np.sqrt(normal_x * normal_x + normal_y * normal_y + normal_z * normal_z)
    normal = np.stack(
        (normal_x / normal_length, normal_y / normal_length, normal_z / normal_length), axis=2
    ) * 0.5 + 0.5

    metallic = np.clip(masks["metal"] * (0.48 + 0.24 * independent_fine), 0.0, 0.72)
    concavity = np.maximum(gaussian(np, height, max(1.0, radius_scale * 6.0)) - height, 0.0)
    ao = np.clip(
        1.0 - concavity * 3.7 - masks["rubble"] * 0.065 - texture_energy * masks["metal"] * 0.025,
        0.70,
        1.0,
    )
    detail = (
        masks["concrete"] * (0.48 + 0.24 * independent_fine)
        + masks["floor"] * (0.34 + 0.20 * independent_fine)
        + masks["dust"] * (0.28 + 0.21 * independent_fine)
        + masks["rubble"] * (0.70 + 0.27 * independent_fine)
        + masks["trace"] * (0.52 + 0.22 * independent_fine)
        + masks["metal"] * (0.64 + 0.25 * independent_fine)
    )
    smoothness = (
        masks["concrete"] * (0.15 + 0.12 * independent_fine)
        + masks["floor"] * (0.20 + 0.14 * independent_fine)
        + masks["dust"] * (0.09 + 0.08 * independent_fine)
        + masks["rubble"] * (0.11 + 0.10 * independent_fine)
        + masks["trace"] * (0.16 + 0.12 * independent_fine)
        + masks["metal"] * (0.28 + 0.20 * independent_fine)
    )
    mask_rgba = np.stack((metallic, ao, np.clip(detail, 0.0, 1.0), np.clip(smoothness, 0.0, 1.0)), axis=2)

    base_path = args.asset_root / "T_Terrain_Ruins_BaseColor.png"
    normal_path = args.asset_root / "T_Terrain_Ruins_Normal.png"
    mask_path = args.asset_root / "T_Terrain_Ruins_Mask.png"
    height_path = args.asset_root / "T_Terrain_Ruins_Height.png"
    save_rgb(Image, np, base_path, base)
    save_rgb(Image, np, normal_path, normal)
    save_rgba(Image, np, mask_path, mask_rgba)
    Image.fromarray(np.clip(height * 65535.0 + 0.5, 0, 65535).astype(np.uint16), mode="I;16").save(
        height_path, format="PNG", optimize=True
    )

    tile = Image.open(base_path).resize((size // 4, size // 4), Image.Resampling.LANCZOS)
    tiling = Image.new("RGB", (size, size))
    for row in range(4):
        for column in range(4):
            tiling.paste(tile, (column * size // 4, row * size // 4))
    tiling.save(args.qa_root / "QA_Terrain_Ruins_Tiling4x4.png", format="PNG", optimize=True)

    ora_path = make_ora(np, Image, args.source_root, base, masks)
    coverage = {name: int(categories[name].sum()) / total for name in RATIOS}
    print("MAPS_COMPLETE")
    print(f"SIZE={size}")
    print(f"ORA={ora_path}")
    for name in RATIOS:
        print(f"COVERAGE_{name.upper()}={coverage[name]:.8f}")
    print(f"HEIGHT_MIN={float(height.min()):.8f}")
    print(f"HEIGHT_MAX={float(height.max()):.8f}")
    print(f"SMOOTHNESS_MIN={float(smoothness.min()):.8f}")
    print(f"SMOOTHNESS_MAX={float(smoothness.max()):.8f}")


def build_blender(args: argparse.Namespace):
    import bpy
    from mathutils import Vector

    for datablocks in (bpy.data.objects, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for datablock in list(datablocks):
            datablocks.remove(datablock, do_unlink=True)

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1920
    scene.render.resolution_y = 1080
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.world.color = (0.025, 0.03, 0.032)
    scene["wastecity_asset"] = "Ruins terrain first-pass sample"
    scene["generator_seed"] = SEED
    scene["texture_resolution"] = args.size
    scene["coverage_ratios"] = "35/20/20/15/7/3"
    scene["gameplay_truth"] = "none"
    scene["authoring_blender"] = bpy.app.version_string
    scene["preview_renderer"] = "EEVEE"
    scene["approved_concept"] = "Ruins_Approved_AI_Concept_v001.png"

    world = scene.world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.018, 0.023, 0.026, 1.0)
    background.inputs["Strength"].default_value = 0.25

    material = bpy.data.materials.new("MAT_Terrain_Ruins_Golden_Preview")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    output.location = (680, 40)
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.location = (420, 40)
    bsdf.inputs["Specular IOR Level"].default_value = 0.28
    links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    texcoord = nodes.new("ShaderNodeTexCoord")
    texcoord.location = (-900, 100)

    def image_node(name, filename, y, colorspace):
        node = nodes.new("ShaderNodeTexImage")
        node.name = name
        node.label = name
        node.location = (-620, y)
        image = bpy.data.images.load(str(args.asset_root / filename), check_existing=True)
        image.colorspace_settings.name = colorspace
        node.image = image
        node.interpolation = "Linear"
        node.extension = "REPEAT"
        links.new(texcoord.outputs["UV"], node.inputs["Vector"])
        return node

    base = image_node("BaseColor_sRGB", "T_Terrain_Ruins_BaseColor.png", 330, "sRGB")
    normal = image_node("Normal_Linear", "T_Terrain_Ruins_Normal.png", 70, "Non-Color")
    packed = image_node("URP_Mask_Linear", "T_Terrain_Ruins_Mask.png", -190, "Non-Color")
    height = image_node("Height16_Linear", "T_Terrain_Ruins_Height.png", -450, "Non-Color")
    links.new(base.outputs["Color"], bsdf.inputs["Base Color"])
    separate = nodes.new("ShaderNodeSeparateColor")
    separate.location = (-300, -170)
    links.new(packed.outputs["Color"], separate.inputs["Color"])
    links.new(separate.outputs["Red"], bsdf.inputs["Metallic"])
    invert_smooth = nodes.new("ShaderNodeMath")
    invert_smooth.operation = "SUBTRACT"
    invert_smooth.inputs[0].default_value = 1.0
    invert_smooth.location = (160, -120)
    links.new(packed.outputs["Alpha"], invert_smooth.inputs[1])
    links.new(invert_smooth.outputs["Value"], bsdf.inputs["Roughness"])
    normal_map = nodes.new("ShaderNodeNormalMap")
    normal_map.space = "TANGENT"
    normal_map.inputs["Strength"].default_value = 0.68
    normal_map.location = (150, 160)
    links.new(normal.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], bsdf.inputs["Normal"])
    height.label = "16-bit source; not gameplay height"

    def scale_uv(obj, scale):
        layer = obj.data.uv_layers.active
        if layer:
            for loop in layer.data:
                loop.uv *= scale

    bpy.ops.mesh.primitive_plane_add(size=8.0, location=(0.0, 0.0, 0.0))
    plane = bpy.context.object
    plane.name = "Ruins_PBR_Plane_4x4"
    plane.data.materials.append(material)
    scale_uv(plane, 4.0)

    bpy.ops.mesh.primitive_uv_sphere_add(segments=96, ring_count=64, radius=1.15, location=(-1.9, 0.15, 1.18))
    sphere = bpy.context.object
    sphere.name = "Ruins_PBR_Sphere"
    sphere.data.materials.append(material)
    scale_uv(sphere, 2.0)
    bpy.ops.object.shade_smooth()
    sphere.hide_render = True

    def light(name, light_type, energy, location, color, size=4.0):
        data = bpy.data.lights.new(name=name, type=light_type)
        data.energy = energy
        data.color = color
        if light_type == "AREA":
            data.shape = "DISK"
            data.size = size
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        obj.location = location
        return obj

    light("Key_Area", "AREA", 700.0, (2.5, -3.5, 7.5), (1.0, 0.84, 0.68), 6.0)
    light("Fill_Area", "AREA", 280.0, (-4.5, 1.5, 5.0), (0.60, 0.62, 0.60), 6.0)
    light("Rim_Area", "AREA", 420.0, (4.0, 4.0, 6.0), (0.70, 0.72, 0.70), 5.0)

    camera_data = bpy.data.cameras.new("Camera_DefaultTiltedOrtho")
    camera = bpy.data.objects.new("Camera_DefaultTiltedOrtho", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 10.4

    def point_camera(location, target):
        camera.location = location
        direction = Vector(target) - camera.location
        camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()

    point_camera((6.2, -8.0, 7.4), (0.0, 0.0, 0.0))
    scene.render.filepath = str(args.qa_root / "QA_Terrain_Ruins_DefaultOrtho.png")
    bpy.ops.render.render(write_still=True)

    sphere.hide_render = False
    camera.data.type = "PERSP"
    camera.data.lens = 55
    point_camera((7.8, -10.8, 6.5), (-0.2, 0.0, 0.8))
    scene.render.filepath = str(args.qa_root / "QA_Terrain_Ruins_PBRCheck.png")
    bpy.ops.render.render(write_still=True)

    generator_text = bpy.data.texts.new("generate_ruins.py")
    generator_text.write(Path(__file__).read_text(encoding="utf-8"))
    notes = bpy.data.texts.new("README_Ruins_Golden.txt")
    notes.write(
        "Waste City Ruins terrain first-pass material sample.\n"
        "User-approved AI concept informed the BaseColor rebuild; seed 824401.\n"
        "Height and Mask were independently reconstructed; no grayscale channel forgery.\n"
        f"Authored and rendered with Blender {bpy.app.version_string} (EEVEE).\n"
        "No gameplay truth and no third-party stock texture input.\n"
        "Textures are packed for source portability.\n"
    )
    for image in bpy.data.images:
        if image.source == "FILE":
            image.pack()
    blend_path = args.source_root / "Ruins_Golden_Generator.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), compress=True)
    print(f"BLENDER_COMPLETE={blend_path}")


def main():
    args = parse_args()
    if args.stage == "maps":
        generate_maps(args)
    else:
        build_blender(args)


if __name__ == "__main__":
    main()
