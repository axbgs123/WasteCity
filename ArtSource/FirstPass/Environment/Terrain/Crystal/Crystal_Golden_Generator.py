from __future__ import annotations

import argparse
import shutil
import sys
import zipfile
from pathlib import Path


SEED = 824219
RATIOS = {
    "soil": 0.65,
    "glass": 0.18,
    "crust": 0.10,
    "vein": 0.04,
    "edge": 0.03,
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
    for index, name in enumerate(("soil", "glass", "crust", "vein", "edge")):
        gray = np.repeat(masks[name][..., None], 3, axis=2)
        rgba = np.concatenate((gray, np.ones((*gray.shape[:2], 1), dtype=np.float32)), axis=2)
        path = temp / f"mask_{index:02d}_{name}.png"
        save_rgba(Image, np, path, rgba)
        layer_entries.append((f"{name.title()} Coverage Mask", path, "hidden"))

    size = base_rgb.shape[0]
    ora_path = source_root / "Crystal_Golden_Master.ora"
    stack_layers = []
    for layer_index, (name, path, visibility) in enumerate(reversed(layer_entries)):
        stack_layers.append(
            f'<layer name="{name}" src="data/layer_{layer_index:02d}.png" visibility="{visibility}" '
            'composite-op="svg:src-over" opacity="1.0"/>'
        )
    stack_xml = (
        '<?xml version="1.0" encoding="UTF-8"?>\n'
        f'<image version="0.0.1" w="{size}" h="{size}" name="Crystal Golden Master">\n'
        '  <stack name="Crystal Golden Source">\n    '
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
        raise FileNotFoundError("--concept must point to the user-approved Crystal concept image")

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
    cool = 0.55 * flattened[..., 1] + 0.45 * flattened[..., 2] - 0.78 * flattened[..., 0]
    darkness = 1.0 - luminance
    independent_macro = fractal_noise(np, size, [(9, 0.45), (17, 0.33), (29, 0.22)], SEED + 1)
    independent_macro_b = fractal_noise(np, size, [(12, 0.46), (23, 0.32), (41, 0.22)], SEED + 101)
    independent_fine = fractal_noise(np, size, [(38, 0.43), (73, 0.34), (131, 0.23)], SEED + 201)

    total = size * size
    available = np.ones((size, size), dtype=bool)
    categories = {}
    vein_boundary, vein_value = periodic_voronoi(np, size, 14, SEED + 401)
    vein_gate = smoothstep(np.clip((independent_macro_b - 0.42) / 0.32, 0.0, 1.0))
    raw_scores = {
        "glass": darkness * 1.16 - warmth * 0.34 + independent_macro * 0.20 + independent_macro_b * 0.10,
        "crust": cool * 1.30 + blur_medium * 0.24 - warmth * 0.18 + independent_macro_b * 0.16,
        "vein": cool * 0.22 + vein_boundary * vein_gate * (0.58 + vein_value * 0.25) + independent_macro_b * 0.18,
        "edge": darkness * 0.52 + texture_energy * 0.40 + np.abs(medium_band) * 0.34 + independent_macro * 0.16,
    }
    scores = {
        "glass": gaussian(np, raw_scores["glass"], max(6.0, size / 2048.0 * 24.0)),
        "crust": gaussian(np, raw_scores["crust"], max(5.0, size / 2048.0 * 22.0)),
        "vein": gaussian(np, raw_scores["vein"], max(0.8, size / 2048.0 * 1.2)),
        "edge": gaussian(np, raw_scores["edge"], max(1.5, size / 2048.0 * 4.5)),
    }
    for name in ("glass", "crust", "vein", "edge"):
        categories[name] = pick_top(np, scores[name], available, round(total * RATIOS[name]))
        available &= ~categories[name]
    categories["soil"] = available

    radius_scale = size / 2048.0
    blur_radius = {"soil": 7.0, "glass": 8.0, "crust": 8.0, "vein": 1.8, "edge": 3.5}
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
        "soil": hex_rgb("61503A"),
        "glass": hex_rgb("263234"),
        "crust": hex_rgb("526563"),
        "vein": hex_rgb("4D8F92"),
        "edge": hex_rgb("493B2F"),
    }
    mixes = {"soil": 0.20, "glass": 0.28, "crust": 0.25, "vein": 0.15, "edge": 0.25}
    smooth_factors = {"soil": 0.05, "glass": 0.32, "crust": 0.18, "vein": 0.12, "edge": 0.08}
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

    basin_boundary, basin_value = periodic_voronoi(np, size, 9, SEED + 501)
    material_relief = np.tanh((medium_band * 3.0 + high_band * 0.48) * 2.2)
    height_layers = {
        "soil": 0.495 + 0.023 * independent_macro + 0.010 * np.maximum(material_relief, 0.0),
        "glass": 0.454 + 0.009 * independent_macro - 0.006 * basin_boundary,
        "crust": 0.526 + 0.018 * independent_macro + 0.008 * np.maximum(material_relief, 0.0),
        "vein": 0.515 + 0.010 * independent_fine + 0.006 * vein_boundary,
        "edge": 0.443 + 0.012 * basin_value + 0.008 * independent_fine,
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

    metallic = np.zeros_like(height)
    concavity = np.maximum(gaussian(np, height, max(1.0, radius_scale * 6.0)) - height, 0.0)
    ao = np.clip(1.0 - concavity * 3.4 - masks["edge"] * 0.060 - vein_boundary * masks["vein"] * 0.022, 0.74, 1.0)
    detail = (
        masks["soil"] * (0.42 + 0.25 * independent_fine)
        + masks["glass"] * (0.18 + 0.19 * independent_fine)
        + masks["crust"] * (0.48 + 0.27 * independent_fine)
        + masks["vein"] * (0.68 + 0.28 * independent_fine)
        + masks["edge"] * (0.55 + 0.27 * independent_fine)
    )
    smoothness = (
        masks["soil"] * (0.13 + 0.10 * independent_fine)
        + masks["glass"] * (0.55 + 0.23 * independent_fine)
        + masks["crust"] * (0.28 + 0.18 * independent_fine)
        + masks["vein"] * (0.36 + 0.19 * independent_fine)
        + masks["edge"] * (0.18 + 0.15 * independent_fine)
    )
    mask_rgba = np.stack((metallic, ao, np.clip(detail, 0.0, 1.0), np.clip(smoothness, 0.0, 1.0)), axis=2)

    base_path = args.asset_root / "T_Terrain_Crystal_BaseColor.png"
    normal_path = args.asset_root / "T_Terrain_Crystal_Normal.png"
    mask_path = args.asset_root / "T_Terrain_Crystal_Mask.png"
    height_path = args.asset_root / "T_Terrain_Crystal_Height.png"
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
    tiling.save(args.qa_root / "QA_Terrain_Crystal_Tiling4x4.png", format="PNG", optimize=True)

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
    scene["wastecity_asset"] = "Crystal terrain first-pass sample"
    scene["generator_seed"] = SEED
    scene["texture_resolution"] = args.size
    scene["coverage_ratios"] = "65/18/10/4/3"
    scene["gameplay_truth"] = "none"
    scene["authoring_blender"] = bpy.app.version_string
    scene["preview_renderer"] = "EEVEE"
    scene["approved_concept"] = "Crystal_Approved_AI_Concept_v002.png"

    world = scene.world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.018, 0.023, 0.026, 1.0)
    background.inputs["Strength"].default_value = 0.25

    material = bpy.data.materials.new("MAT_Terrain_Crystal_Golden_Preview")
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

    base = image_node("BaseColor_sRGB", "T_Terrain_Crystal_BaseColor.png", 330, "sRGB")
    normal = image_node("Normal_Linear", "T_Terrain_Crystal_Normal.png", 70, "Non-Color")
    packed = image_node("URP_Mask_Linear", "T_Terrain_Crystal_Mask.png", -190, "Non-Color")
    height = image_node("Height16_Linear", "T_Terrain_Crystal_Height.png", -450, "Non-Color")
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
    plane.name = "Crystal_PBR_Plane_4x4"
    plane.data.materials.append(material)
    scale_uv(plane, 4.0)

    bpy.ops.mesh.primitive_uv_sphere_add(segments=96, ring_count=64, radius=1.15, location=(-1.9, 0.15, 1.18))
    sphere = bpy.context.object
    sphere.name = "Crystal_PBR_Sphere"
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
    scene.render.filepath = str(args.qa_root / "QA_Terrain_Crystal_DefaultOrtho.png")
    bpy.ops.render.render(write_still=True)

    sphere.hide_render = False
    camera.data.type = "PERSP"
    camera.data.lens = 55
    point_camera((7.8, -10.8, 6.5), (-0.2, 0.0, 0.8))
    scene.render.filepath = str(args.qa_root / "QA_Terrain_Crystal_PBRCheck.png")
    bpy.ops.render.render(write_still=True)

    generator_text = bpy.data.texts.new("generate_crystal.py")
    generator_text.write(Path(__file__).read_text(encoding="utf-8"))
    notes = bpy.data.texts.new("README_Crystal_Golden.txt")
    notes.write(
        "Waste City Crystal terrain first-pass material sample.\n"
        "User-approved AI concept informed the BaseColor rebuild; seed 824219.\n"
        "Height and Mask were independently reconstructed; no grayscale channel forgery.\n"
        f"Authored and rendered with Blender {bpy.app.version_string} (EEVEE).\n"
        "No gameplay truth and no third-party stock texture input.\n"
        "Textures are packed for source portability.\n"
    )
    for image in bpy.data.images:
        if image.source == "FILE":
            image.pack()
    blend_path = args.source_root / "Crystal_Golden_Generator.blend"
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
