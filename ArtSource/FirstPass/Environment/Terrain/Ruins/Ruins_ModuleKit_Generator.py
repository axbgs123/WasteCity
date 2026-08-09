from __future__ import annotations

import argparse
import math
import random
import sys
from pathlib import Path


SEED = 824502
REFERENCE_NAME = "Ruins_Modules_Approved_AI_Reference_v001.png"
MODULES = (
    ("SM_Ruins_CrackedFloorSlab", "Cracked industrial floor slab"),
    ("SM_Ruins_RubblePile_A", "Low compact concrete rubble pile"),
    ("SM_Ruins_RubblePile_B", "Elongated mixed rubble pile"),
    ("SM_Ruins_RebarConcreteBlock", "Reinforced-concrete block with three short rebars"),
    ("SM_Ruins_BrokenPipe", "Short broken industrial pipe"),
    ("SM_Ruins_DrainageChannel", "Straight shallow drainage channel"),
    ("SM_Ruins_BoundaryEdge", "Damaged ruins boundary edge plate"),
    ("SM_Ruins_WornMarkingPlate", "Thin worn industrial marking plate"),
)


def parse_args() -> argparse.Namespace:
    raw = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else sys.argv[1:]
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-root", type=Path, required=True)
    parser.add_argument("--asset-root", type=Path, required=True)
    parser.add_argument("--qa-root", type=Path, required=True)
    parser.add_argument("--reference", type=Path, required=True)
    args = parser.parse_args(raw)
    for key in ("source_root", "asset_root", "qa_root", "reference"):
        setattr(args, key, getattr(args, key).resolve())
    return args


def clear_scene(bpy) -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in list(bpy.data.collections):
        if collection.name != "Collection":
            bpy.data.collections.remove(collection)


def create_materials(bpy, texture_paths, wasteland_paths):
    specs = {
        # tint, value, saturation, tint mix, texture scale, metallic, bump, roughness, normal
        "MAT_Ruins_Concrete": ((0.82, 0.74, 0.62, 1.0), 0.92, 0.48, 0.58, 2.50, 0.0, 0.34, 0.90, 0.56),
        "MAT_Ruins_Aggregate": ((0.68, 0.57, 0.44, 1.0), 0.84, 0.46, 0.90, 5.00, 0.0, 0.58, 0.98, 0.88),
        "MAT_Ruins_DarkFloor": ((0.38, 0.39, 0.38, 1.0), 0.55, 0.20, 0.80, 2.50, 0.0, 0.34, 0.84, 0.62),
        "MAT_Ruins_Dust": ((0.78, 0.58, 0.34, 1.0), 0.92, 0.70, 0.88, 4.00, 0.0, 0.48, 0.99, 0.78),
        "MAT_Ruins_DustFilm": ((0.60, 0.50, 0.39, 1.0), 0.72, 0.46, 0.82, 4.50, 0.0, 0.16, 0.99, 0.34),
        "MAT_Ruins_Rust": ((0.46, 0.12, 0.025, 1.0), 0.78, 1.28, 0.94, 6.00, 0.76, 0.62, 0.72, 0.92),
        "MAT_Ruins_Marking": ((0.78, 0.34, 0.055, 1.0), 0.88, 1.12, 0.90, 3.50, 0.0, 0.20, 0.88, 0.48),
        "MAT_Ruins_DrainDark": ((0.20, 0.22, 0.22, 1.0), 0.42, 0.10, 0.90, 3.00, 0.0, 0.30, 0.72, 0.60),
    }
    detail_specs = {
        "MAT_Ruins_Concrete": ((0.52, 0.45, 0.36, 1.0), (0.92, 0.79, 0.62, 1.0), 13.0, 0.34),
        "MAT_Ruins_Aggregate": ((0.40, 0.33, 0.25, 1.0), (0.82, 0.67, 0.48, 1.0), 22.0, 0.52),
        "MAT_Ruins_DarkFloor": ((0.34, 0.35, 0.34, 1.0), (0.72, 0.70, 0.64, 1.0), 8.0, 0.42),
        "MAT_Ruins_Dust": ((0.64, 0.52, 0.39, 1.0), (1.00, 0.84, 0.62, 1.0), 18.0, 0.42),
        "MAT_Ruins_DustFilm": ((0.52, 0.45, 0.36, 1.0), (0.82, 0.68, 0.51, 1.0), 15.0, 0.26),
        "MAT_Ruins_Rust": ((0.24, 0.055, 0.012, 1.0), (0.92, 0.34, 0.055, 1.0), 7.0, 0.70),
        "MAT_Ruins_Marking": ((0.40, 0.15, 0.018, 1.0), (0.92, 0.48, 0.09, 1.0), 10.0, 0.46),
        "MAT_Ruins_DrainDark": ((0.24, 0.26, 0.25, 1.0), (0.58, 0.60, 0.56, 1.0), 9.0, 0.40),
    }
    dust_accumulation = {
        # minimum on vertical faces, maximum on upward faces
        "MAT_Ruins_Concrete": (0.04, 0.22),
        "MAT_Ruins_Aggregate": (0.03, 0.12),
        "MAT_Ruins_DarkFloor": (0.04, 0.13),
        "MAT_Ruins_Dust": (0.48, 0.82),
        "MAT_Ruins_DustFilm": (0.10, 0.22),
        "MAT_Ruins_Rust": (0.00, 0.025),
        "MAT_Ruins_Marking": (0.01, 0.06),
        "MAT_Ruins_DrainDark": (0.02, 0.06),
    }

    base_color = bpy.data.images.load(str(texture_paths["base_color"]), check_existing=True)
    normal = bpy.data.images.load(str(texture_paths["normal"]), check_existing=True)
    mask = bpy.data.images.load(str(texture_paths["mask"]), check_existing=True)
    height = bpy.data.images.load(str(texture_paths["height"]), check_existing=True)
    wasteland_base = bpy.data.images.load(str(wasteland_paths["base_color"]), check_existing=True)
    base_color.colorspace_settings.name = "sRGB"
    for image in (normal, mask, height):
        image.colorspace_settings.name = "Non-Color"
    for image in (base_color, normal, mask, height, wasteland_base):
        image.pack()
        image.use_fake_user = True
    wasteland_base.colorspace_settings.name = "sRGB"

    materials = {}
    for name, (tint, value, saturation, tint_factor, texture_scale, metallic_scale, bump_strength, roughness_bias, normal_strength) in specs.items():
        material = bpy.data.materials.new(name)
        material.diffuse_color = tint
        material.use_nodes = True
        nodes = material.node_tree.nodes
        links = material.node_tree.links
        nodes.clear()
        output = nodes.new("ShaderNodeOutputMaterial")
        bsdf = nodes.new("ShaderNodeBsdfPrincipled")
        bsdf.inputs["Specular IOR Level"].default_value = 0.24
        texcoord = nodes.new("ShaderNodeTexCoord")
        mapping = nodes.new("ShaderNodeMapping")
        mapping.inputs["Scale"].default_value = (texture_scale, texture_scale, texture_scale)
        base_node = nodes.new("ShaderNodeTexImage")
        base_node.image = base_color
        base_node.extension = "REPEAT"
        hue = nodes.new("ShaderNodeHueSaturation")
        hue.inputs["Value"].default_value = value
        hue.inputs["Saturation"].default_value = saturation
        tint_mix = nodes.new("ShaderNodeMixRGB")
        tint_mix.blend_type = "MULTIPLY"
        tint_mix.inputs[0].default_value = tint_factor
        tint_mix.inputs[2].default_value = tint
        mask_node = nodes.new("ShaderNodeTexImage")
        mask_node.image = mask
        mask_node.extension = "REPEAT"
        mask_split = nodes.new("ShaderNodeSeparateColor")
        ao_mix = nodes.new("ShaderNodeMixRGB")
        ao_mix.blend_type = "MULTIPLY"
        ao_mix.inputs[0].default_value = 0.30
        detail_noise = nodes.new("ShaderNodeTexNoise")
        detail_noise.inputs["Scale"].default_value = detail_specs[name][2]
        detail_noise.inputs["Detail"].default_value = 4.0
        detail_noise.inputs["Roughness"].default_value = 0.72
        detail_ramp = nodes.new("ShaderNodeValToRGB")
        detail_ramp.color_ramp.elements[0].position = 0.28
        detail_ramp.color_ramp.elements[0].color = detail_specs[name][0]
        detail_ramp.color_ramp.elements[1].position = 0.72
        detail_ramp.color_ramp.elements[1].color = detail_specs[name][1]
        detail_mix = nodes.new("ShaderNodeMixRGB")
        detail_mix.blend_type = "MULTIPLY"
        detail_mix.inputs[0].default_value = detail_specs[name][3]
        wasteland_mapping = nodes.new("ShaderNodeMapping")
        wasteland_mapping.inputs["Scale"].default_value = (3.20, 3.20, 3.20)
        wasteland_node = nodes.new("ShaderNodeTexImage")
        wasteland_node.image = wasteland_base
        wasteland_node.extension = "REPEAT"
        geometry = nodes.new("ShaderNodeNewGeometry")
        normal_split = nodes.new("ShaderNodeSeparateXYZ")
        upward_dust = nodes.new("ShaderNodeMapRange")
        upward_dust.inputs["From Min"].default_value = 0.0
        upward_dust.inputs["From Max"].default_value = 0.75
        upward_dust.inputs["To Min"].default_value = dust_accumulation[name][0]
        upward_dust.inputs["To Max"].default_value = dust_accumulation[name][1]
        upward_dust.clamp = True
        broken_dust = nodes.new("ShaderNodeMapRange")
        broken_dust.inputs["From Min"].default_value = 0.0
        broken_dust.inputs["From Max"].default_value = 1.0
        broken_dust.inputs["To Min"].default_value = 0.55
        broken_dust.inputs["To Max"].default_value = 1.0
        dust_factor = nodes.new("ShaderNodeMath")
        dust_factor.operation = "MULTIPLY"
        dust_mix = nodes.new("ShaderNodeMixRGB")
        dust_mix.blend_type = "MIX"
        normal_node = nodes.new("ShaderNodeTexImage")
        normal_node.image = normal
        normal_node.extension = "REPEAT"
        normal_map = nodes.new("ShaderNodeNormalMap")
        normal_map.inputs["Strength"].default_value = normal_strength
        height_node = nodes.new("ShaderNodeTexImage")
        height_node.image = height
        height_node.extension = "REPEAT"
        bump = nodes.new("ShaderNodeBump")
        bump.inputs["Strength"].default_value = bump_strength
        bump.inputs["Distance"].default_value = 0.060
        roughness = nodes.new("ShaderNodeMapRange")
        roughness.inputs["From Min"].default_value = 0.08
        roughness.inputs["From Max"].default_value = 0.48
        roughness.inputs["To Min"].default_value = min(1.0, roughness_bias + 0.12)
        roughness.inputs["To Max"].default_value = max(0.42, roughness_bias - 0.18)
        links.new(texcoord.outputs["UV"], mapping.inputs["Vector"])
        for image_node in (base_node, mask_node, normal_node, height_node):
            links.new(mapping.outputs["Vector"], image_node.inputs["Vector"])
        links.new(base_node.outputs["Color"], hue.inputs["Color"])
        links.new(hue.outputs["Color"], tint_mix.inputs[1])
        links.new(tint_mix.outputs["Color"], ao_mix.inputs[1])
        links.new(mask_node.outputs["Color"], mask_split.inputs["Color"])
        links.new(mask_split.outputs["Green"], ao_mix.inputs[2])
        links.new(texcoord.outputs["Generated"], detail_noise.inputs["Vector"])
        links.new(detail_noise.outputs["Fac"], detail_ramp.inputs["Fac"])
        links.new(ao_mix.outputs["Color"], detail_mix.inputs[1])
        links.new(detail_ramp.outputs["Color"], detail_mix.inputs[2])
        links.new(texcoord.outputs["UV"], wasteland_mapping.inputs["Vector"])
        links.new(wasteland_mapping.outputs["Vector"], wasteland_node.inputs["Vector"])
        links.new(geometry.outputs["Normal"], normal_split.inputs["Vector"])
        links.new(normal_split.outputs["Z"], upward_dust.inputs["Value"])
        links.new(detail_noise.outputs["Fac"], broken_dust.inputs["Value"])
        links.new(upward_dust.outputs["Result"], dust_factor.inputs[0])
        links.new(broken_dust.outputs["Result"], dust_factor.inputs[1])
        links.new(dust_factor.outputs[0], dust_mix.inputs[0])
        links.new(detail_mix.outputs["Color"], dust_mix.inputs[1])
        links.new(wasteland_node.outputs["Color"], dust_mix.inputs[2])
        links.new(dust_mix.outputs["Color"], bsdf.inputs["Base Color"])
        links.new(normal_node.outputs["Color"], normal_map.inputs["Color"])
        links.new(normal_map.outputs["Normal"], bump.inputs["Normal"])
        links.new(height_node.outputs["Color"], bump.inputs["Height"])
        links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
        links.new(mask_node.outputs["Alpha"], roughness.inputs["Value"])
        links.new(roughness.outputs["Result"], bsdf.inputs["Roughness"])
        if metallic_scale > 0.0:
            metallic = nodes.new("ShaderNodeMath")
            metallic.operation = "MULTIPLY"
            metallic.inputs[1].default_value = metallic_scale
            links.new(mask_split.outputs["Red"], metallic.inputs[0])
            links.new(metallic.outputs[0], bsdf.inputs["Metallic"])
        else:
            bsdf.inputs["Metallic"].default_value = 0.0
        links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
        material["surface_role"] = name.removeprefix("MAT_Ruins_")
        material["uses_approved_ruins_pbr_maps"] = True
        material["uses_approved_wasteland_dust_color"] = True
        materials[name] = material
    return materials


def create_wasteland_qa_material(bpy, texture_paths):
    images = {}
    for key, path in texture_paths.items():
        image = bpy.data.images.load(str(path), check_existing=True)
        image.colorspace_settings.name = "sRGB" if key == "base_color" else "Non-Color"
        image.pack()
        image.use_fake_user = True
        images[key] = image
    material = bpy.data.materials.new("MAT_QA_Approved_Wasteland")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Specular IOR Level"].default_value = 0.20
    texcoord = nodes.new("ShaderNodeTexCoord")
    mapping = nodes.new("ShaderNodeMapping")
    mapping.inputs["Scale"].default_value = (6.0, 6.0, 6.0)
    base = nodes.new("ShaderNodeTexImage")
    base.image = images["base_color"]
    base.extension = "REPEAT"
    normal = nodes.new("ShaderNodeTexImage")
    normal.image = images["normal"]
    normal.extension = "REPEAT"
    normal_map = nodes.new("ShaderNodeNormalMap")
    normal_map.inputs["Strength"].default_value = 0.70
    height = nodes.new("ShaderNodeTexImage")
    height.image = images["height"]
    height.extension = "REPEAT"
    bump = nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.36
    bump.inputs["Distance"].default_value = 0.045
    mask = nodes.new("ShaderNodeTexImage")
    mask.image = images["mask"]
    mask.extension = "REPEAT"
    mask_split = nodes.new("ShaderNodeSeparateColor")
    roughness = nodes.new("ShaderNodeMapRange")
    roughness.inputs["From Min"].default_value = 0.08
    roughness.inputs["From Max"].default_value = 0.45
    roughness.inputs["To Min"].default_value = 0.94
    roughness.inputs["To Max"].default_value = 0.72
    links.new(texcoord.outputs["UV"], mapping.inputs["Vector"])
    for node in (base, normal, height, mask):
        links.new(mapping.outputs["Vector"], node.inputs["Vector"])
    links.new(base.outputs["Color"], bsdf.inputs["Base Color"])
    links.new(normal.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], bump.inputs["Normal"])
    links.new(height.outputs["Color"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    links.new(mask.outputs["Color"], mask_split.inputs["Color"])
    links.new(mask.outputs["Alpha"], roughness.inputs["Value"])
    links.new(roughness.outputs["Result"], bsdf.inputs["Roughness"])
    links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    material["qa_only"] = True
    material["source"] = "Approved Wasteland golden master PBR set"
    return material


def assign_material(obj, material) -> None:
    if len(obj.data.materials) == 0:
        obj.data.materials.append(material)
    else:
        obj.data.materials[0] = material


def create_irregular_prism(bpy, name, points, height, material, z=0.0, side_material=None):
    count = len(points)
    vertices = [(x, y, z) for x, y in points] + [(x, y, z + height) for x, y in points]
    faces = [tuple(range(count - 1, -1, -1)), tuple(range(count, count * 2))]
    for index in range(count):
        nxt = (index + 1) % count
        faces.append((index, nxt, count + nxt, count + index))
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    assign_material(obj, material)
    if side_material is None and material.name in {"MAT_Ruins_Concrete", "MAT_Ruins_DarkFloor"}:
        side_material = bpy.data.materials.get("MAT_Ruins_Aggregate")
    if side_material is not None:
        obj.data.materials.append(side_material)
        for polygon in obj.data.polygons:
            if polygon.index != 1:
                polygon.material_index = 1
    return obj


def create_beveled_cube(bpy, name, size, location, rotation, material, bevel=0.015, side_material=None):
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = (size[0] * 0.5, size[1] * 0.5, size[2] * 0.5)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0.0:
        modifier = obj.modifiers.new("Restrained_Bevel", "BEVEL")
        modifier.width = min(bevel, min(size) * 0.22)
        modifier.segments = 1
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    assign_material(obj, material)
    if side_material is None and material.name in {"MAT_Ruins_Concrete", "MAT_Ruins_DarkFloor"}:
        side_material = bpy.data.materials.get("MAT_Ruins_Aggregate")
    if side_material is not None:
        obj.data.materials.append(side_material)
        for polygon in obj.data.polygons:
            if polygon.normal.z < 0.64:
                polygon.material_index = 1
    return obj


def create_broken_curb(bpy, rng, name, size, location, angle, material):
    sx, sy, sz = size
    cuts = [rng.uniform(0.74, 0.94) for _ in range(4)]
    points = [
        (-sx * 0.50 * cuts[0], -sy * 0.50),
        (sx * 0.34, -sy * 0.50),
        (sx * 0.50, -sy * 0.28 * cuts[1]),
        (sx * 0.48 * cuts[2], sy * 0.50),
        (-sx * 0.28, sy * 0.50),
        (-sx * 0.50, sy * 0.22 * cuts[3]),
    ]
    obj = create_irregular_prism(bpy, name, points, sz, material)
    obj.location = location
    obj.rotation_euler[2] = angle
    bevel_mesh(bpy, obj, min(0.012, sz * 0.10))
    return obj


def create_angular_chunk(bpy, rng, name, center, scale, material):
    sides = rng.choice((5, 6, 7))
    angles = sorted(rng.uniform(0.0, math.tau) for _ in range(sides))
    points = []
    for angle in angles:
        radius = rng.uniform(0.70, 1.05)
        points.append((math.cos(angle) * scale[0] * radius, math.sin(angle) * scale[1] * radius))
    obj = create_irregular_prism(bpy, name, points, scale[2], material)
    obj.location = (center[0], center[1], center[2])
    obj.rotation_euler = (rng.uniform(-0.22, 0.22), rng.uniform(-0.22, 0.22), rng.uniform(-math.pi, math.pi))
    return obj


def bevel_mesh(bpy, obj, width=0.012):
    modifier = obj.modifiers.new("Broken_Edge_Bevel", "BEVEL")
    modifier.width = width
    modifier.segments = 1
    modifier.affect = "EDGES"
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    return obj


def roughen_mesh(bpy, obj, name, strength=0.010, noise_scale=0.090):
    subdivision = obj.modifiers.new(name + "_SimpleSubdivision", "SUBSURF")
    subdivision.subdivision_type = "SIMPLE"
    subdivision.levels = 1
    subdivision.render_levels = 1
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=subdivision.name)
    texture = bpy.data.textures.new(name + "_SurfaceNoise", type="CLOUDS")
    texture.noise_scale = noise_scale
    texture.noise_depth = 1
    displacement = obj.modifiers.new(name + "_Weathering", "DISPLACE")
    displacement.texture = texture
    displacement.texture_coords = "GLOBAL"
    displacement.strength = strength
    displacement.mid_level = 0.50
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=displacement.name)
    return obj


def create_poly_rod(bpy, name, coords, radius, material):
    curve = bpy.data.curves.new(name + "_Curve", "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 1
    curve.bevel_depth = radius
    curve.bevel_resolution = 0
    curve.resolution_v = 0
    curve.use_fill_caps = True
    spline = curve.splines.new("POLY")
    spline.points.add(len(coords) - 1)
    for point, coordinate in zip(spline.points, coords):
        point.co = (*coordinate, 1.0)
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(material)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.convert(target="MESH")
    obj.select_set(False)
    return obj


def create_rust_ring(bpy, name, x, radius, material):
    bpy.ops.mesh.primitive_torus_add(
        major_radius=radius,
        minor_radius=0.010,
        major_segments=20,
        minor_segments=4,
        location=(x, 0.0, radius),
        rotation=(0.0, math.pi * 0.5, 0.0),
    )
    obj = bpy.context.object
    obj.name = name
    assign_material(obj, material)
    return obj


def create_tube(bpy, name, length, radius, thickness, material, location=(0.0, 0.0, 0.0), seed=0, inner_material=None):
    rng = random.Random(seed)
    segments = 20
    vertices = []
    for side, x in enumerate((-length * 0.5, length * 0.5)):
        for ring_radius in (radius, radius - thickness):
            for index in range(segments):
                angle = math.tau * index / segments
                damage = 1.0 + (rng.uniform(-0.05, 0.05) if side == 0 else rng.uniform(-0.025, 0.025))
                vertices.append((x, math.cos(angle) * ring_radius * damage, math.sin(angle) * ring_radius * damage + radius))
    outer_a = 0
    inner_a = segments
    outer_b = segments * 2
    inner_b = segments * 3
    faces = []
    for index in range(segments):
        nxt = (index + 1) % segments
        faces.append((outer_a + index, outer_a + nxt, outer_b + nxt, outer_b + index))
        faces.append((inner_a + nxt, inner_a + index, inner_b + index, inner_b + nxt))
        faces.append((outer_a + nxt, outer_a + index, inner_a + index, inner_a + nxt))
        faces.append((outer_b + index, outer_b + nxt, inner_b + nxt, inner_b + index))
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    assign_material(obj, material)
    if inner_material is None and material.name == "MAT_Ruins_Concrete":
        inner_material = bpy.data.materials.get("MAT_Ruins_Aggregate")
    if inner_material is not None:
        obj.data.materials.append(inner_material)
        for polygon in obj.data.polygons:
            if polygon.index % 4 in (1, 2, 3):
                polygon.material_index = 1
    return obj


def create_bent_rebar(bpy, name, offset_y, material):
    curve = bpy.data.curves.new(name + "_Curve", "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 1
    curve.bevel_depth = 0.018
    curve.bevel_resolution = 0
    curve.resolution_u = 1
    spline = curve.splines.new("POLY")
    spline.points.add(3)
    coords = (
        (0.20, offset_y, 0.20, 1.0),
        (0.35, offset_y, 0.20, 1.0),
        (0.48, offset_y, 0.25, 1.0),
        (0.55, offset_y, 0.20, 1.0),
    )
    for point, coordinate in zip(spline.points, coords):
        point.co = coordinate
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(material)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.convert(target="MESH")
    obj.select_set(False)
    return obj


def join_parts(bpy, parts, final_name):
    bpy.ops.object.select_all(action="DESELECT")
    for part in parts:
        part.hide_set(False)
        part.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = final_name
    triangulate = obj.modifiers.new("Export_Triangulate", "TRIANGULATE")
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=triangulate.name)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    obj.data.update()
    return obj


def smart_uv(bpy, obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66.0), island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")
    obj.select_set(False)


def add_scattered_chunks(bpy, rng, parts, prefix, count, spread, z_range, scale_range, materials):
    for index in range(count):
        angle = rng.uniform(0.0, math.tau)
        radius = math.sqrt(rng.random())
        center = (
            math.cos(angle) * spread[0] * radius,
            math.sin(angle) * spread[1] * radius,
            rng.uniform(*z_range),
        )
        sx = rng.uniform(*scale_range)
        sy = sx * rng.uniform(0.65, 1.25)
        sz = sx * rng.uniform(0.35, 0.85)
        parts.append(create_angular_chunk(bpy, rng, f"{prefix}_{index:02d}", center, (sx, sy, sz), rng.choice(materials)))


def add_rubble_mound(bpy, rng, parts, prefix, count, spread, scale_range, materials, mound_height):
    for index in range(count):
        angle = rng.uniform(0.0, math.tau)
        radius = math.sqrt(rng.random())
        x = math.cos(angle) * spread[0] * radius
        y = math.sin(angle) * spread[1] * radius
        sx = rng.uniform(*scale_range)
        sy = sx * rng.uniform(0.60, 1.30)
        sz = sx * rng.uniform(0.42, 0.95)
        dome = mound_height * max(0.0, 1.0 - radius * radius)
        z = dome + rng.uniform(-0.015, 0.020)
        parts.append(create_angular_chunk(bpy, rng, f"{prefix}_{index:02d}", (x, y, z), (sx, sy, sz), rng.choice(materials)))


def add_surface_cracks(bpy, parts, prefix, networks, z, material, radius=0.006):
    index = 0
    for network in networks:
        for start, end in zip(network, network[1:]):
            parts.append(create_poly_rod(bpy, f"{prefix}_{index:02d}", ((*start, z), (*end, z + 0.001)), radius, material))
            index += 1


def add_flat_weather_patch(bpy, parts, name, location, size, z, material, angle=0.0):
    sx, sy = size
    patch = create_irregular_prism(
        bpy,
        name,
        [
            (-sx * 0.50, -sy * 0.20),
            (-sx * 0.28, -sy * 0.50),
            (sx * 0.24, -sy * 0.44),
            (sx * 0.50, -sy * 0.05),
            (sx * 0.32, sy * 0.42),
            (-sx * 0.20, sy * 0.50),
            (-sx * 0.48, sy * 0.18),
        ],
        0.0015,
        material,
        z=z,
    )
    patch.location = (location[0], location[1], 0.0)
    patch.rotation_euler[2] = angle
    parts.append(patch)
    return patch


def make_cracked_slab(bpy, materials, rng):
    parts = []
    points = [(-0.56, -0.35), (-0.47, -0.47), (-0.08, -0.50), (0.31, -0.47), (0.54, -0.34), (0.56, 0.10), (0.45, 0.40), (0.12, 0.48), (-0.38, 0.44), (-0.56, 0.25)]
    slab = create_irregular_prism(bpy, "Slab_Main", points, 0.125, materials["MAT_Ruins_Concrete"])
    bevel_mesh(bpy, slab, 0.018)
    roughen_mesh(bpy, slab, "Slab", 0.012, 0.075)
    parts.append(slab)
    add_surface_cracks(
        bpy,
        parts,
        "Slab_Crack",
        (
            ((-0.44, 0.08), (-0.24, 0.04), (-0.08, -0.07), (0.10, -0.05), (0.28, -0.20), (0.47, -0.22)),
            ((-0.08, -0.07), (-0.12, -0.27), (-0.25, -0.44)),
            ((0.10, -0.05), (0.17, 0.14), (0.10, 0.34), (0.02, 0.47)),
            ((0.17, 0.14), (0.35, 0.25), (0.46, 0.28)),
            ((-0.24, 0.04), (-0.30, 0.25), (-0.43, 0.38)),
            ((-0.30, 0.25), (-0.16, 0.34), (-0.08, 0.45)),
            ((0.28, -0.20), (0.34, -0.34), (0.31, -0.46)),
            ((-0.12, -0.27), (0.01, -0.36), (0.08, -0.48)),
            ((0.35, 0.25), (0.34, 0.38), (0.28, 0.47)),
        ),
        0.127,
        materials["MAT_Ruins_DrainDark"],
        0.0025,
    )
    for index, (x, y, sx, sy) in enumerate(((-0.34, -0.42, 0.16, 0.08), (0.44, -0.29, 0.13, 0.07), (0.49, 0.13, 0.11, 0.06), (-0.45, 0.30, 0.12, 0.06))):
        patch = create_irregular_prism(
            bpy,
            f"Slab_Spall_{index}",
            [(-sx * 0.5, -sy * 0.35), (sx * 0.36, -sy * 0.5), (sx * 0.5, sy * 0.20), (0.0, sy * 0.5), (-sx * 0.45, sy * 0.18)],
            0.003,
            materials["MAT_Ruins_Aggregate"],
            z=0.124,
        )
        patch.location = (x, y, 0.0)
        parts.append(patch)
    add_scattered_chunks(bpy, rng, parts, "Slab_Chip", 36, (0.62, 0.57), (0.0, 0.022), (0.012, 0.038), [materials["MAT_Ruins_Concrete"], materials["MAT_Ruins_Concrete"], materials["MAT_Ruins_Concrete"], materials["MAT_Ruins_Dust"]])
    # The approved reference has a dense collapsed edge, not evenly distributed confetti.
    for index in range(18):
        x = rng.uniform(0.38, 0.64)
        y = rng.uniform(-0.43, 0.30)
        size = rng.uniform(0.018, 0.050)
        parts.append(create_angular_chunk(
            bpy,
            rng,
            f"Slab_BrokenEdge_{index:02d}",
            (x, y, rng.uniform(0.0, 0.025)),
            (size, size * rng.uniform(0.60, 1.20), size * rng.uniform(0.35, 0.78)),
            materials["MAT_Ruins_Aggregate"] if index % 3 else materials["MAT_Ruins_Dust"],
        ))
    add_flat_weather_patch(bpy, parts, "Slab_Dust_A", (-0.30, 0.19), (0.20, 0.10), 0.128, materials["MAT_Ruins_DustFilm"], -0.20)
    add_flat_weather_patch(bpy, parts, "Slab_Dust_B", (0.22, -0.28), (0.17, 0.085), 0.128, materials["MAT_Ruins_DustFilm"], 0.16)
    add_flat_weather_patch(bpy, parts, "Slab_Dust_C", (0.34, 0.24), (0.14, 0.07), 0.128, materials["MAT_Ruins_DustFilm"], -0.35)
    return join_parts(bpy, parts, MODULES[0][0])


def make_rubble_a(bpy, materials, rng):
    parts = []
    add_rubble_mound(bpy, rng, parts, "RubbleA", 78, (0.47, 0.36), (0.025, 0.090), [materials["MAT_Ruins_Concrete"], materials["MAT_Ruins_Concrete"], materials["MAT_Ruins_Concrete"], materials["MAT_Ruins_Dust"]], 0.145)
    for index, (x, y, sx, sy, z, angle) in enumerate((
        (-0.21, 0.03, 0.28, 0.17, 0.135, -0.28),
        (0.06, 0.06, 0.31, 0.19, 0.175, 0.18),
        (0.25, -0.04, 0.23, 0.14, 0.125, -0.14),
        (-0.02, -0.14, 0.21, 0.13, 0.115, 0.32),
    )):
        slab = create_irregular_prism(bpy, f"RubbleA_Large_{index}", [(-sx/2, -sy/2), (sx*0.42, -sy/2), (sx/2, sy*0.28), (sx*0.18, sy/2), (-sx/2, sy*0.36)], 0.045, materials["MAT_Ruins_Concrete"])
        slab.location = (x, y, z)
        slab.rotation_euler = (rng.uniform(-0.18, 0.18), rng.uniform(-0.18, 0.18), angle)
        parts.append(slab)
    return join_parts(bpy, parts, MODULES[1][0])


def make_rubble_b(bpy, materials, rng):
    parts = []
    add_rubble_mound(bpy, rng, parts, "RubbleB", 70, (0.64, 0.28), (0.026, 0.082), [materials["MAT_Ruins_Concrete"], materials["MAT_Ruins_Concrete"], materials["MAT_Ruins_Concrete"], materials["MAT_Ruins_Dust"]], 0.075)
    for index, (x, y, sx, sy, z, angle) in enumerate((
        (-0.36, -0.03, 0.38, 0.23, 0.078, -0.22),
        (-0.09, 0.05, 0.32, 0.18, 0.098, 0.20),
        (0.21, -0.02, 0.36, 0.21, 0.088, -0.08),
        (0.43, 0.04, 0.25, 0.16, 0.070, 0.18),
    )):
        slab = create_irregular_prism(bpy, f"RubbleB_Slab_{index}", [(-sx/2, -sy/2), (sx*0.42, -sy/2), (sx/2, 0.0), (sx*0.30, sy/2), (-sx*0.38, sy*0.44)], 0.050, materials["MAT_Ruins_Concrete"])
        slab.location = (x, y, z)
        slab.rotation_euler = (rng.uniform(-0.16, 0.16), rng.uniform(-0.16, 0.16), angle)
        parts.append(slab)
    for index, (coords, radius) in enumerate((
        (((-0.52, -0.18, 0.10), (-0.62, -0.25, 0.15), (-0.69, -0.20, 0.12)), 0.012),
        (((0.08, 0.12, 0.16), (0.16, 0.20, 0.22), (0.23, 0.16, 0.17)), 0.012),
        (((0.45, -0.12, 0.12), (0.53, -0.20, 0.18), (0.61, -0.17, 0.13)), 0.011),
    )):
        parts.append(create_poly_rod(bpy, f"RubbleB_Rebar_{index}", coords, radius, materials["MAT_Ruins_Rust"]))
    for index in range(18):
        x = rng.uniform(-0.67, 0.68)
        y_sign = -1.0 if index % 2 else 1.0
        y = y_sign * rng.uniform(0.18, 0.29)
        size = rng.uniform(0.018, 0.046)
        parts.append(create_angular_chunk(
            bpy,
            rng,
            f"RubbleB_Edge_{index:02d}",
            (x, y, rng.uniform(0.0, 0.028)),
            (size, size * rng.uniform(0.65, 1.15), size * rng.uniform(0.38, 0.78)),
            materials["MAT_Ruins_Aggregate"] if index % 4 else materials["MAT_Ruins_Dust"],
        ))
    return join_parts(bpy, parts, MODULES[2][0])


def make_rebar_block(bpy, materials, rng):
    parts = []
    main = create_irregular_prism(
        bpy,
        "Block_Main",
        [(-0.34, -0.27), (0.24, -0.29), (0.33, -0.13), (0.30, 0.24), (0.08, 0.30), (-0.30, 0.25), (-0.38, 0.04)],
        0.45,
        materials["MAT_Ruins_Concrete"],
    )
    bevel_mesh(bpy, main, 0.026)
    roughen_mesh(bpy, main, "Block", 0.030, 0.070)
    parts.append(main)
    for index, (x, y, sx, sy, z) in enumerate(((-0.27, -0.20, 0.13, 0.09, 0.448), (0.19, 0.18, 0.15, 0.10, 0.452), (-0.08, 0.25, 0.12, 0.08, 0.451))):
        patch = create_irregular_prism(
            bpy,
            f"Block_TopChip_{index}",
            [(-sx * 0.5, -sy * 0.3), (sx * 0.22, -sy * 0.5), (sx * 0.5, sy * 0.1), (sx * 0.05, sy * 0.5), (-sx * 0.45, sy * 0.22)],
            0.003,
            materials["MAT_Ruins_Aggregate"],
            z=z,
        )
        patch.location = (x, y, 0.0)
        parts.append(patch)
    top_break = create_irregular_prism(
        bpy,
        "Block_ExposedTopBreak",
        [(-0.22, -0.08), (0.02, -0.13), (0.24, -0.08), (0.18, 0.04), (-0.04, 0.08), (-0.24, 0.03)],
        0.006,
        materials["MAT_Ruins_Aggregate"],
        z=0.451,
    )
    top_break.location = (-0.03, -0.18, 0.0)
    parts.append(top_break)
    add_flat_weather_patch(bpy, parts, "Block_Dust_A", (-0.17, 0.08), (0.14, 0.075), 0.457, materials["MAT_Ruins_DustFilm"], 0.24)
    add_flat_weather_patch(bpy, parts, "Block_Dust_B", (0.14, -0.02), (0.11, 0.06), 0.457, materials["MAT_Ruins_DustFilm"], -0.18)
    add_scattered_chunks(bpy, rng, parts, "Block_BaseDebris", 30, (0.46, 0.38), (0.0, 0.025), (0.020, 0.058), [materials["MAT_Ruins_Concrete"], materials["MAT_Ruins_Concrete"], materials["MAT_Ruins_Concrete"], materials["MAT_Ruins_Dust"]])
    rebar_paths = (
        ((0.27, -0.16, 0.31), (0.46, -0.16, 0.31), (0.57, -0.16, 0.25), (0.59, -0.16, 0.17)),
        ((0.29, 0.00, 0.27), (0.48, 0.00, 0.27), (0.58, 0.00, 0.20), (0.57, 0.00, 0.12)),
        ((0.27, 0.16, 0.23), (0.45, 0.16, 0.23), (0.55, 0.16, 0.17), (0.53, 0.16, 0.10)),
    )
    for index, coords in enumerate(rebar_paths):
        parts.append(create_poly_rod(bpy, f"Block_Rebar_{index}", coords, 0.017, materials["MAT_Ruins_Rust"]))
    return join_parts(bpy, parts, MODULES[3][0])


def make_broken_pipe(bpy, materials, rng):
    pipe = create_tube(bpy, "Pipe_Main", 0.86, 0.32, 0.068, materials["MAT_Ruins_Concrete"], seed=SEED + 50)
    roughen_mesh(bpy, pipe, "Pipe", 0.010, 0.055)
    parts = [pipe]
    parts.append(create_rust_ring(bpy, "Pipe_RustRing_A", -0.31, 0.314, materials["MAT_Ruins_Rust"]))
    parts.append(create_rust_ring(bpy, "Pipe_RustRing_B", 0.31, 0.314, materials["MAT_Ruins_Rust"]))
    hole = create_irregular_prism(bpy, "Pipe_SurfaceBreak", [(-0.12, -0.08), (0.08, -0.11), (0.15, 0.01), (0.06, 0.10), (-0.11, 0.08)], 0.008, materials["MAT_Ruins_DrainDark"], z=0.618)
    parts.append(hole)
    for index, angle in enumerate((0.0, 0.8, 1.7, 2.6, 3.5, 4.4, 5.3)):
        size = rng.uniform(0.020, 0.038)
        chip = create_angular_chunk(
            bpy,
            rng,
            f"Pipe_HoleEdge_{index}",
            (math.cos(angle) * 0.13, math.sin(angle) * 0.085, 0.625),
            (size, size * 0.75, size * 0.35),
            materials["MAT_Ruins_Aggregate"],
        )
        parts.append(chip)
    add_flat_weather_patch(bpy, parts, "Pipe_Dust_A", (-0.20, -0.01), (0.13, 0.06), 0.635, materials["MAT_Ruins_DustFilm"], 0.20)
    add_flat_weather_patch(bpy, parts, "Pipe_Dust_B", (0.24, 0.04), (0.10, 0.05), 0.635, materials["MAT_Ruins_DustFilm"], -0.15)
    add_scattered_chunks(bpy, rng, parts, "Pipe_Debris", 24, (0.48, 0.37), (0.0, 0.018), (0.018, 0.052), [materials["MAT_Ruins_Concrete"], materials["MAT_Ruins_Concrete"], materials["MAT_Ruins_Dust"]])
    add_scattered_chunks(bpy, rng, parts, "Pipe_InteriorDebris", 13, (0.20, 0.18), (0.0, 0.030), (0.018, 0.045), [materials["MAT_Ruins_Concrete"], materials["MAT_Ruins_Concrete"], materials["MAT_Ruins_Dust"]])
    return join_parts(bpy, parts, MODULES[4][0])


def make_drain(bpy, materials, rng):
    parts = [create_beveled_cube(bpy, "Drain_Floor", (1.10, 0.42, 0.060), (0.0, 0.0, 0.030), (0.0, 0.0, 0.0), materials["MAT_Ruins_DrainDark"], 0.010)]
    segments = ((-0.265, 0.51), (0.265, 0.51))
    for side, y in (("L", -0.19), ("R", 0.19)):
        for index, (x, length) in enumerate(segments):
            height = rng.uniform(0.13, 0.16)
            lip = create_broken_curb(
                bpy,
                rng,
                f"Drain_{side}_{index}",
                (length, rng.uniform(0.105, 0.120), height),
                (x, y + rng.uniform(-0.012, 0.012), 0.055),
                rng.uniform(-0.035, 0.035),
                materials["MAT_Ruins_Concrete"],
            )
            roughen_mesh(bpy, lip, f"Drain_{side}_{index}", 0.007, 0.048)
            parts.append(lip)
    add_scattered_chunks(bpy, rng, parts, "Drain_Chip", 38, (0.61, 0.32), (0.0, 0.045), (0.014, 0.046), [materials["MAT_Ruins_Concrete"], materials["MAT_Ruins_Concrete"], materials["MAT_Ruins_Aggregate"], materials["MAT_Ruins_Dust"]])
    add_flat_weather_patch(bpy, parts, "Drain_Dust_A", (-0.27, -0.01), (0.17, 0.075), 0.061, materials["MAT_Ruins_DustFilm"], -0.10)
    add_flat_weather_patch(bpy, parts, "Drain_Dust_B", (0.34, 0.02), (0.13, 0.06), 0.061, materials["MAT_Ruins_DustFilm"], 0.16)
    return join_parts(bpy, parts, MODULES[5][0])


def make_boundary_edge(bpy, materials, rng):
    parts = []
    plate = create_irregular_prism(bpy, "Edge_Ground", [(-0.58, -0.24), (0.52, -0.22), (0.59, -0.03), (0.43, 0.22), (0.09, 0.25), (-0.30, 0.23), (-0.58, 0.10)], 0.045, materials["MAT_Ruins_DarkFloor"])
    parts.append(plate)
    for index, (x, y, sx, angle, height) in enumerate(((-0.38, 0.12, 0.37, -0.025, 0.14), (0.00, 0.13, 0.37, 0.018, 0.15), (0.38, 0.11, 0.36, -0.035, 0.14))):
        curb = create_broken_curb(bpy, rng, f"Edge_Curb_{index}", (sx, 0.17, height), (x, y, 0.045), angle, materials["MAT_Ruins_Concrete"])
        roughen_mesh(bpy, curb, f"Edge_Curb_{index}", 0.007, 0.048)
        parts.append(curb)
    add_surface_cracks(bpy, parts, "Edge_Crack", (((-0.52, -0.18), (-0.26, -0.12), (-0.06, -0.19), (0.18, -0.12), (0.43, -0.20)),), 0.052, materials["MAT_Ruins_DrainDark"], 0.005)
    add_scattered_chunks(bpy, rng, parts, "Edge_Chip", 34, (0.62, 0.36), (0.0, 0.040), (0.016, 0.047), [materials["MAT_Ruins_Concrete"], materials["MAT_Ruins_Concrete"], materials["MAT_Ruins_Concrete"], materials["MAT_Ruins_Dust"]])
    add_flat_weather_patch(bpy, parts, "Edge_Dust_A", (-0.30, -0.12), (0.16, 0.065), 0.046, materials["MAT_Ruins_DustFilm"], 0.12)
    add_flat_weather_patch(bpy, parts, "Edge_Dust_B", (0.29, -0.09), (0.14, 0.06), 0.046, materials["MAT_Ruins_DustFilm"], -0.20)
    return join_parts(bpy, parts, MODULES[6][0])


def make_marking_plate(bpy, materials, rng):
    parts = []
    points = [(-0.53, -0.36), (-0.18, -0.42), (0.16, -0.40), (0.51, -0.27), (0.55, 0.02), (0.43, 0.35), (0.05, 0.39), (-0.36, 0.34), (-0.55, 0.10)]
    base = create_irregular_prism(bpy, "Marking_Base", points, 0.055, materials["MAT_Ruins_DarkFloor"])
    bevel_mesh(bpy, base, 0.012)
    roughen_mesh(bpy, base, "Marking", 0.006, 0.070)
    parts.append(base)
    stripe_shapes = (
        ((-0.44, -0.16), (-0.15, -0.18), (0.12, -0.15), (0.43, -0.17), (0.45, -0.06), (0.10, -0.04), (-0.18, -0.07), (-0.45, -0.05)),
        ((-0.43, 0.08), (-0.10, 0.07), (0.16, 0.10), (0.44, 0.08), (0.43, 0.20), (0.12, 0.18), (-0.14, 0.20), (-0.44, 0.18)),
    )
    for index, stripe_points in enumerate(stripe_shapes):
        stripe = create_irregular_prism(bpy, f"Marking_Stripe_{index}", stripe_points, 0.007, materials["MAT_Ruins_Marking"], z=0.056)
        parts.append(stripe)
    for index, (x, y, sx, sy) in enumerate(((-0.29, -0.11, 0.12, 0.045), (0.04, -0.10, 0.16, 0.040), (0.31, -0.12, 0.10, 0.035), (-0.17, 0.13, 0.14, 0.040), (0.22, 0.14, 0.12, 0.045))):
        worn_patch = create_irregular_prism(
            bpy,
            f"Marking_Wear_{index}",
            [(-sx * 0.5, -sy * 0.5), (sx * 0.35, -sy * 0.5), (sx * 0.5, sy * 0.18), (sx * 0.08, sy * 0.5), (-sx * 0.46, sy * 0.26)],
            0.002,
            materials["MAT_Ruins_DarkFloor"],
            z=0.063,
        )
        worn_patch.location = (x, y, 0.0)
        parts.append(worn_patch)
    add_surface_cracks(
        bpy,
        parts,
        "Marking_Crack",
        (((-0.46, -0.20), (-0.24, -0.13), (-0.08, -0.24)), ((0.08, 0.32), (0.17, 0.16), (0.37, 0.08), (0.49, -0.05))),
        0.057,
        materials["MAT_Ruins_DrainDark"],
        0.0025,
    )
    add_scattered_chunks(bpy, rng, parts, "Marking_Chip", 24, (0.57, 0.43), (0.0, 0.025), (0.014, 0.040), [materials["MAT_Ruins_Dust"], materials["MAT_Ruins_Concrete"]])
    add_flat_weather_patch(bpy, parts, "Marking_Dust_A", (-0.34, 0.29), (0.14, 0.06), 0.057, materials["MAT_Ruins_DustFilm"], -0.12)
    add_flat_weather_patch(bpy, parts, "Marking_Dust_B", (0.33, -0.29), (0.13, 0.06), 0.057, materials["MAT_Ruins_DustFilm"], 0.18)
    return join_parts(bpy, parts, MODULES[7][0])


def triangle_count(obj) -> int:
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def set_origin_at_base(bpy, obj):
    minimum = min(vertex.co.z for vertex in obj.data.vertices)
    for vertex in obj.data.vertices:
        vertex.co.z -= minimum
    bpy.context.scene.cursor.location = (0.0, 0.0, 0.0)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR", center="MEDIAN")
    obj.select_set(False)


def export_fbx(bpy, obj, output_path: Path):
    bpy.ops.object.select_all(action="DESELECT")
    obj.hide_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    output_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=str(output_path),
        use_selection=True,
        object_types={"MESH"},
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=False,
        mesh_smooth_type="FACE",
        path_mode="AUTO",
    )
    obj.select_set(False)
    obj.hide_set(True)


def point_camera(camera, target, Vector):
    direction = Vector(target) - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def create_wire_material(bpy):
    material = bpy.data.materials.new("MAT_QA_Ruins_Wireframe")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    diffuse = nodes.new("ShaderNodeBsdfDiffuse")
    diffuse.inputs["Color"].default_value = (0.18, 0.17, 0.15, 1.0)
    wire = nodes.new("ShaderNodeWireframe")
    wire.inputs["Size"].default_value = 0.008
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = (0.008, 0.008, 0.008, 1.0)
    emission.inputs["Strength"].default_value = 0.35
    mix = nodes.new("ShaderNodeMixShader")
    links.new(wire.outputs["Fac"], mix.inputs[0])
    links.new(diffuse.outputs["BSDF"], mix.inputs[1])
    links.new(emission.outputs["Emission"], mix.inputs[2])
    links.new(mix.outputs["Shader"], output.inputs["Surface"])
    return material


def build(args: argparse.Namespace):
    import bpy
    from bpy_extras.object_utils import world_to_camera_view
    from mathutils import Vector

    if not args.reference.is_file():
        raise FileNotFoundError(f"Approved model reference missing: {args.reference}")
    args.source_root.mkdir(parents=True, exist_ok=True)
    args.asset_root.mkdir(parents=True, exist_ok=True)
    args.qa_root.mkdir(parents=True, exist_ok=True)
    texture_root = args.asset_root.parent
    texture_paths = {
        "base_color": texture_root / "T_Terrain_Ruins_BaseColor.png",
        "normal": texture_root / "T_Terrain_Ruins_Normal.png",
        "mask": texture_root / "T_Terrain_Ruins_Mask.png",
        "height": texture_root / "T_Terrain_Ruins_Height.png",
    }
    for texture_name, texture_path in texture_paths.items():
        if not texture_path.is_file():
            raise FileNotFoundError(f"Approved Ruins PBR {texture_name} missing: {texture_path}")
    wasteland_root = texture_root.parent / "Wasteland"
    wasteland_paths = {
        "base_color": wasteland_root / "T_Terrain_Wasteland_BaseColor.png",
        "normal": wasteland_root / "T_Terrain_Wasteland_Normal.png",
        "mask": wasteland_root / "T_Terrain_Wasteland_Mask.png",
        "height": wasteland_root / "T_Terrain_Wasteland_Height.png",
    }
    for texture_name, texture_path in wasteland_paths.items():
        if not texture_path.is_file():
            raise FileNotFoundError(f"Approved Wasteland PBR {texture_name} missing: {texture_path}")
    clear_scene(bpy)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1536
    scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene["wastecity_asset"] = "Ruins eight-module low-poly kit"
    scene["approved_reference"] = REFERENCE_NAME
    scene["approved_reference_sha256"] = "f72aa401942a0956f9d027486eb9639acc18825ef06f22776c5b0336f333458c"
    scene["generator_seed"] = SEED
    scene["module_count"] = 8
    scene["gameplay_truth"] = "none"
    scene["colliders"] = "none"
    scene["unity_axis"] = "Y-up, -Z forward via FBX export"
    scene["material_source"] = "Approved Ruins BaseColor/Normal/Mask/Height"
    materials = create_materials(bpy, texture_paths, wasteland_paths)
    rng = random.Random(SEED)
    makers = (make_cracked_slab, make_rubble_a, make_rubble_b, make_rebar_block, make_broken_pipe, make_drain, make_boundary_edge, make_marking_plate)
    assets = []
    for maker in makers:
        obj = maker(bpy, materials, rng)
        set_origin_at_base(bpy, obj)
        smart_uv(bpy, obj)
        obj.location = (0.0, 0.0, 0.0)
        obj.rotation_euler = (0.0, 0.0, 0.0)
        obj["gameplay_truth"] = "none"
        obj["collider"] = "none"
        obj["triangle_count"] = triangle_count(obj)
        assets.append(obj)
    for obj in assets:
        count = triangle_count(obj)
        if count < 200 or count > 2000:
            raise RuntimeError(f"{obj.name} triangle count {count} outside 200..2000")
        export_fbx(bpy, obj, args.asset_root / f"{obj.name}.fbx")

    preview_objects = []
    screen_positions = ((-2.58, 1.18), (-0.86, 1.18), (0.86, 1.18), (2.58, 1.18), (-2.58, -1.18), (-0.86, -1.18), (0.86, -1.18), (2.58, -1.18))
    for obj in assets:
        clone = obj.copy()
        clone.data = obj.data.copy()
        clone.name = "PREVIEW_" + obj.name
        bpy.context.collection.objects.link(clone)
        clone.hide_render = False
        clone.hide_set(False)
        clone.location = (0.0, 0.0, 0.0)
        preview_objects.append(clone)
        obj.hide_render = True
        obj.hide_set(True)

    world = scene.world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.115, 0.105, 0.096, 1.0)
    background.inputs["Strength"].default_value = 0.45
    floor_material = bpy.data.materials.new("MAT_QA_Backdrop")
    floor_material.diffuse_color = (0.165, 0.145, 0.128, 1.0)
    floor_material.use_nodes = True
    floor_bsdf = floor_material.node_tree.nodes.get("Principled BSDF")
    floor_bsdf.inputs["Base Color"].default_value = floor_material.diffuse_color
    floor_bsdf.inputs["Roughness"].default_value = 0.94
    wasteland_qa_material = create_wasteland_qa_material(bpy, wasteland_paths)
    bpy.ops.mesh.primitive_plane_add(size=18.0, location=(0.0, 0.0, -0.012))
    backdrop = bpy.context.object
    backdrop.name = "QA_Backdrop"
    backdrop.data.materials.append(floor_material)

    def add_light(name, energy, location, color, size):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.shape = "DISK"
        data.size = size
        data.color = color
        light = bpy.data.objects.new(name, data)
        scene.collection.objects.link(light)
        light.location = location
        return light

    add_light("Key_Area", 1150.0, (3.0, -4.5, 8.5), (1.0, 0.82, 0.62), 6.5)
    add_light("Fill_Area", 700.0, (-5.0, -0.5, 6.0), (0.62, 0.67, 0.72), 6.0)
    add_light("Rim_Area", 650.0, (4.5, 5.0, 7.0), (0.76, 0.70, 0.60), 5.0)
    camera_data = bpy.data.cameras.new("Camera_Ruins_ModuleKit")
    camera = bpy.data.objects.new("Camera_Ruins_ModuleKit", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 4.80
    camera.location = (6.6, -9.6, 7.4)
    point_camera(camera, (0.0, 0.0, 0.15), Vector)

    def arrange_preview_for_camera():
        bpy.context.view_layer.update()
        projected_origin = world_to_camera_view(scene, camera, Vector((0.0, 0.0, 0.0)))
        projected_x = world_to_camera_view(scene, camera, Vector((1.0, 0.0, 0.0)))
        projected_y = world_to_camera_view(scene, camera, Vector((0.0, 1.0, 0.0)))
        axis_x_u = projected_x.x - projected_origin.x
        axis_y_u = projected_y.x - projected_origin.x
        axis_x_v = projected_x.y - projected_origin.y
        axis_y_v = projected_y.y - projected_origin.y
        determinant = axis_x_u * axis_y_v - axis_y_u * axis_x_v
        aspect = scene.render.resolution_x / scene.render.resolution_y
        for clone, (screen_x, screen_y) in zip(preview_objects, screen_positions):
            target_u = 0.5 + screen_x / (camera.data.ortho_scale * aspect)
            target_v = 0.5 + screen_y / camera.data.ortho_scale
            offset_u = target_u - projected_origin.x
            offset_v = target_v - projected_origin.y
            world_x = (offset_u * axis_y_v - axis_y_u * offset_v) / determinant
            world_y = (axis_x_u * offset_v - offset_u * axis_x_v) / determinant
            clone.location = (world_x, world_y, 0.0)

    arrange_preview_for_camera()
    scene.render.filepath = str(args.qa_root / "QA_Ruins_ModuleKit_DefaultOrtho.png")
    bpy.ops.render.render(write_still=True)

    backdrop.data.materials[0] = wasteland_qa_material
    scene.render.filepath = str(args.qa_root / "QA_Ruins_ModuleKit_WastelandContext.png")
    bpy.ops.render.render(write_still=True)
    backdrop.data.materials[0] = floor_material

    camera.location = (0.0, 0.0, 12.0)
    camera.data.ortho_scale = 4.70
    point_camera(camera, (0.0, 0.0, 0.0), Vector)
    arrange_preview_for_camera()
    scene.render.filepath = str(args.qa_root / "QA_Ruins_ModuleKit_Top.png")
    bpy.ops.render.render(write_still=True)

    wire_material = create_wire_material(bpy)
    original_materials = []
    for clone in preview_objects:
        original_materials.append(list(clone.data.materials))
        clone.data.materials.clear()
        clone.data.materials.append(wire_material)
        for polygon in clone.data.polygons:
            polygon.material_index = 0
    camera.location = (6.6, -9.6, 7.4)
    camera.data.ortho_scale = 4.80
    point_camera(camera, (0.0, 0.0, 0.15), Vector)
    arrange_preview_for_camera()
    scene.render.filepath = str(args.qa_root / "QA_Ruins_ModuleKit_Wireframe.png")
    bpy.ops.render.render(write_still=True)
    for clone, materials_for_clone, source in zip(preview_objects, original_materials, assets):
        clone.data.materials.clear()
        for material in materials_for_clone:
            clone.data.materials.append(material)
        for polygon, source_polygon in zip(clone.data.polygons, source.data.polygons):
            polygon.material_index = source_polygon.material_index

    reference_image = bpy.data.images.load(str(args.reference), check_existing=True)
    reference_image.pack()
    reference_image.use_fake_user = True
    script_text = bpy.data.texts.new("generate_ruins_module_kit.py")
    script_text.write(Path(__file__).read_text(encoding="utf-8"))
    notes = bpy.data.texts.new("README_Ruins_ModuleKit.txt")
    notes.write(
        "Waste City Ruins eight-module low-poly visual kit.\n"
        "Built only after user approval of Ruins_Modules_Approved_AI_Reference_v001.png.\n"
        "Eight independent meshes; FBX is the primary Unity delivery format.\n"
        "No Collider, Rigidbody, gameplay component, stable-ID mutation, or gameplay truth.\n"
        "Blender uses Z-up internally; FBX exports -Z Forward / Y Up at scale 1.0.\n"
    )
    blend_path = args.source_root / "Ruins_ModuleKit.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), compress=True)
    print(f"BLEND={blend_path}")
    for obj in assets:
        dimensions = obj.dimensions
        print(f"MODULE={obj.name}|TRIS={triangle_count(obj)}|SIZE={dimensions.x:.4f},{dimensions.y:.4f},{dimensions.z:.4f}")


def main():
    build(parse_args())


if __name__ == "__main__":
    main()
