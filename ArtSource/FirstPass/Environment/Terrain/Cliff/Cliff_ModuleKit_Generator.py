from __future__ import annotations

import argparse
import math
import random
import sys
from pathlib import Path


SEED = 813418
REFERENCE_NAME = "Cliff_MaterialAndModules_Approved_AI_Reference_v001.png"
REFERENCE_SHA256 = "e76d8d0e86b78c30475181aad0d99a58637b1d7b6dc756af76ffdf09c74be15d"
MODULES = (
    "SM_Cliff_Straight_A",
    "SM_Cliff_Straight_B",
    "SM_Cliff_InnerCorner",
    "SM_Cliff_OuterCorner",
    "SM_Cliff_EndCap",
    "SM_Cliff_TopCap",
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


def create_materials(bpy, texture_paths):
    images = {}
    for key, path in texture_paths.items():
        image = bpy.data.images.load(str(path), check_existing=True)
        image.colorspace_settings.name = "sRGB" if key == "base_color" else "Non-Color"
        image.pack()
        image.use_fake_user = True
        images[key] = image

    specs = {
        # tint, tint mix, mapping scale, roughness floor, normal, bump
        "MAT_Cliff_Strata": ((0.34, 0.25, 0.18, 1.0), 0.34, 5.2, 0.80, 0.82, 0.34),
        "MAT_Cliff_Fracture": ((0.105, 0.083, 0.064, 1.0), 0.54, 6.0, 0.88, 1.05, 0.48),
        "MAT_Cliff_Dust": ((0.24, 0.145, 0.075, 1.0), 0.52, 7.2, 0.94, 0.54, 0.22),
        "MAT_Cliff_Rubble": ((0.25, 0.18, 0.125, 1.0), 0.46, 7.0, 0.92, 0.96, 0.42),
        "MAT_Cliff_Mineral": ((0.30, 0.26, 0.22, 1.0), 0.44, 5.5, 0.72, 0.72, 0.28),
    }
    materials = {}
    for name, (tint, tint_mix, scale, rough_floor, normal_strength, bump_strength) in specs.items():
        material = bpy.data.materials.new(name)
        material.diffuse_color = tint
        material.use_nodes = True
        nodes = material.node_tree.nodes
        links = material.node_tree.links
        nodes.clear()
        output = nodes.new("ShaderNodeOutputMaterial")
        bsdf = nodes.new("ShaderNodeBsdfPrincipled")
        bsdf.inputs["Specular IOR Level"].default_value = 0.22
        texcoord = nodes.new("ShaderNodeTexCoord")
        mapping = nodes.new("ShaderNodeMapping")
        mapping.inputs["Scale"].default_value = (scale, scale, scale)
        base = nodes.new("ShaderNodeTexImage")
        base.image = images["base_color"]
        base.extension = "REPEAT"
        tint_node = nodes.new("ShaderNodeMixRGB")
        tint_node.blend_type = "MIX"
        tint_node.inputs[0].default_value = tint_mix
        tint_node.inputs[2].default_value = tint
        normal = nodes.new("ShaderNodeTexImage")
        normal.image = images["normal"]
        normal.extension = "REPEAT"
        normal_map = nodes.new("ShaderNodeNormalMap")
        normal_map.inputs["Strength"].default_value = normal_strength
        height = nodes.new("ShaderNodeTexImage")
        height.image = images["height"]
        height.extension = "REPEAT"
        bump = nodes.new("ShaderNodeBump")
        bump.inputs["Strength"].default_value = bump_strength
        bump.inputs["Distance"].default_value = 0.055
        mask = nodes.new("ShaderNodeTexImage")
        mask.image = images["mask"]
        mask.extension = "REPEAT"
        split = nodes.new("ShaderNodeSeparateColor")
        roughness = nodes.new("ShaderNodeMapRange")
        roughness.inputs["From Min"].default_value = 0.08
        roughness.inputs["From Max"].default_value = 0.44
        roughness.inputs["To Min"].default_value = min(1.0, rough_floor + 0.12)
        roughness.inputs["To Max"].default_value = rough_floor
        links.new(texcoord.outputs["UV"], mapping.inputs["Vector"])
        for node in (base, normal, height, mask):
            links.new(mapping.outputs["Vector"], node.inputs["Vector"])
        links.new(base.outputs["Color"], tint_node.inputs[1])
        links.new(tint_node.outputs["Color"], bsdf.inputs["Base Color"])
        links.new(normal.outputs["Color"], normal_map.inputs["Color"])
        links.new(normal_map.outputs["Normal"], bump.inputs["Normal"])
        links.new(height.outputs["Color"], bump.inputs["Height"])
        links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
        links.new(mask.outputs["Alpha"], roughness.inputs["Value"])
        links.new(roughness.outputs["Result"], bsdf.inputs["Roughness"])
        links.new(mask.outputs["Color"], split.inputs["Color"])
        if name == "MAT_Cliff_Mineral":
            metallic = nodes.new("ShaderNodeMath")
            metallic.operation = "MULTIPLY"
            metallic.inputs[1].default_value = 0.18
            links.new(split.outputs["Red"], metallic.inputs[0])
            links.new(metallic.outputs[0], bsdf.inputs["Metallic"])
        links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
        material["surface_role"] = name.removeprefix("MAT_Cliff_")
        material["uses_approved_cliff_pbr_maps"] = True
        materials[name] = material
    return materials


def create_wasteland_material(bpy, paths):
    images = {}
    for key, path in paths.items():
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
    mapping.inputs["Scale"].default_value = (7.0, 7.0, 7.0)
    base = nodes.new("ShaderNodeTexImage")
    base.image = images["base_color"]
    normal = nodes.new("ShaderNodeTexImage")
    normal.image = images["normal"]
    normal_map = nodes.new("ShaderNodeNormalMap")
    normal_map.inputs["Strength"].default_value = 0.72
    height = nodes.new("ShaderNodeTexImage")
    height.image = images["height"]
    bump = nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.34
    bump.inputs["Distance"].default_value = 0.045
    mask = nodes.new("ShaderNodeTexImage")
    mask.image = images["mask"]
    roughness = nodes.new("ShaderNodeMapRange")
    roughness.inputs["From Min"].default_value = 0.08
    roughness.inputs["From Max"].default_value = 0.45
    roughness.inputs["To Min"].default_value = 0.94
    roughness.inputs["To Max"].default_value = 0.72
    for node in (base, normal, height, mask):
        node.extension = "REPEAT"
        links.new(texcoord.outputs["UV"], mapping.inputs["Vector"])
        links.new(mapping.outputs["Vector"], node.inputs["Vector"])
    links.new(base.outputs["Color"], bsdf.inputs["Base Color"])
    links.new(normal.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], bump.inputs["Normal"])
    links.new(height.outputs["Color"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    links.new(mask.outputs["Alpha"], roughness.inputs["Value"])
    links.new(roughness.outputs["Result"], bsdf.inputs["Roughness"])
    links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    material["qa_only"] = True
    return material


def assign_material(obj, material) -> None:
    obj.data.materials.append(material)


def irregular_prism(bpy, name, points, height, z, material):
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
    return obj


def bevel(bpy, obj, width=0.022):
    modifier = obj.modifiers.new("WeatheredEdge", "BEVEL")
    modifier.width = width
    modifier.segments = 1
    modifier.affect = "EDGES"
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def roughen(bpy, obj, name, strength=0.018, scale=0.14):
    # Keep the authored silhouette efficient: bevel vertices already provide enough
    # breakup for displacement, while an extra subdivision exceeds the module budget.
    texture = bpy.data.textures.new(name + "_Noise", type="CLOUDS")
    texture.noise_scale = scale
    texture.noise_depth = 1
    displacement = obj.modifiers.new(name + "_Erosion", "DISPLACE")
    displacement.texture = texture
    displacement.texture_coords = "GLOBAL"
    displacement.strength = strength
    displacement.mid_level = 0.5
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=displacement.name)


def angular_chunk(bpy, rng, name, center, scale, material):
    sides = rng.choice((5, 6, 7))
    angles = sorted(rng.uniform(0.0, math.tau) for _ in range(sides))
    points = []
    for angle in angles:
        radius = rng.uniform(0.72, 1.05)
        points.append((math.cos(angle) * scale[0] * radius, math.sin(angle) * scale[1] * radius))
    obj = irregular_prism(bpy, name, points, scale[2], 0.0, material)
    obj.location = center
    obj.rotation_euler = (rng.uniform(-0.18, 0.18), rng.uniform(-0.18, 0.18), rng.uniform(-math.pi, math.pi))
    return obj


def poly_rod(bpy, name, coords, radius, material):
    curve = bpy.data.curves.new(name + "_Curve", "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 1
    curve.bevel_depth = radius
    curve.bevel_resolution = 0
    curve.resolution_v = 0
    curve.use_fill_caps = True
    spline = curve.splines.new("POLY")
    spline.points.add(len(coords) - 1)
    for point, coord in zip(spline.points, coords):
        point.co = (*coord, 1.0)
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    assign_material(obj, material)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.convert(target="MESH")
    obj.select_set(False)
    return obj


def join_parts(bpy, parts, name):
    bpy.ops.object.select_all(action="DESELECT")
    for part in parts:
        part.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = name
    modifier = obj.modifiers.new("ExportTriangulate", "TRIANGULATE")
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return obj


def smart_uv(bpy, obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(64.0), island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")
    obj.select_set(False)


def add_strata_stack(bpy, rng, parts, prefix, outlines, materials, variant=0):
    z = 0.06
    heights = (0.20, 0.22, 0.19, 0.23, 0.18, 0.22, 0.17)
    for index, height in enumerate(heights):
        base_points = outlines[index % len(outlines)]
        jitter = 0.020 + 0.006 * ((index + variant) % 3)
        points = [(x + rng.uniform(-jitter, jitter), y + rng.uniform(-jitter, jitter)) for x, y in base_points]
        material = materials["MAT_Cliff_Fracture"] if index in ({2, 5} if variant % 2 == 0 else {1, 4}) else materials["MAT_Cliff_Strata"]
        layer = irregular_prism(bpy, f"{prefix}_Stratum_{index:02d}", points, height, z, material)
        bevel(bpy, layer, 0.018 + 0.004 * (index % 2))
        roughen(bpy, layer, f"{prefix}_{index:02d}", 0.014 + 0.003 * (index % 3), 0.12)
        parts.append(layer)
        z += height - 0.006
    return z


def add_top_dust(bpy, parts, name, outline, z, material):
    points = [(x * 0.965, y * 0.965) for x, y in outline]
    patch = irregular_prism(bpy, name, points, 0.018, z + 0.002, material)
    bevel(bpy, patch, 0.010)
    parts.append(patch)


def add_top_chips(bpy, rng, parts, prefix, bounds, z, materials, count=14):
    min_x, max_x, min_y, max_y = bounds
    for index in range(count):
        sx = rng.uniform(0.035, 0.095)
        sy = sx * rng.uniform(0.65, 1.30)
        sz = rng.uniform(0.008, 0.024)
        material = materials["MAT_Cliff_Dust"] if index % 3 else materials["MAT_Cliff_Rubble"]
        parts.append(angular_chunk(
            bpy,
            rng,
            f"{prefix}_TopChip_{index:02d}",
            (rng.uniform(min_x, max_x), rng.uniform(min_y, max_y), z + rng.uniform(0.020, 0.034)),
            (sx, sy, sz),
            material,
        ))


def add_foot_rubble(bpy, rng, parts, prefix, bounds, material, count=24):
    min_x, max_x, front_y = bounds
    for index in range(count):
        x = rng.uniform(min_x, max_x)
        y = front_y + rng.uniform(-0.24, 0.10)
        sx = rng.uniform(0.055, 0.14)
        sy = sx * rng.uniform(0.55, 1.15)
        sz = sx * rng.uniform(0.35, 0.72)
        parts.append(angular_chunk(bpy, rng, f"{prefix}_Rubble_{index:02d}", (x, y, rng.uniform(0.0, 0.025)), (sx, sy, sz), material))


def add_vertical_streaks(bpy, parts, prefix, xs, front_y, material, height=1.0):
    for index, x in enumerate(xs):
        z_top = 0.86 + 0.16 * (index % 2)
        z_bottom = max(0.16, z_top - height * (0.58 + 0.08 * (index % 2)))
        width = 0.038 + 0.012 * (index % 3)
        vertices = (
            (x - width, front_y - 0.032, z_top),
            (x + width * 0.72, front_y - 0.034, z_top - 0.055),
            (x + width * 0.46, front_y - 0.036, z_bottom),
            (x - width * 0.58, front_y - 0.034, z_bottom + 0.075),
        )
        mesh = bpy.data.meshes.new(f"{prefix}_Mineral_{index:02d}_Mesh")
        mesh.from_pydata(vertices, [], [(0, 3, 2, 1)])
        mesh.update()
        patch = bpy.data.objects.new(f"{prefix}_Mineral_{index:02d}", mesh)
        bpy.context.collection.objects.link(patch)
        assign_material(patch, material)
        parts.append(patch)


def straight_outline(length=2.6, depth=0.94, skew=0.0):
    x = length * 0.5
    y = depth * 0.5
    return [(-x, -y), (-x * 0.45, -y - 0.035), (0.0, -y + skew), (x * 0.48, -y - 0.025), (x, -y), (x, y), (0.0, y + 0.025), (-x, y)]


def make_straight(bpy, materials, rng, name, variant):
    parts = []
    outlines = [straight_outline(2.6, 0.94, 0.015 * ((index + variant) % 3 - 1)) for index in range(3)]
    top = add_strata_stack(bpy, rng, parts, name, outlines, materials, variant)
    add_top_dust(bpy, parts, name + "_TopDust", outlines[-1], top, materials["MAT_Cliff_Dust"])
    add_top_chips(bpy, rng, parts, name, (-1.18, 1.18, -0.38, 0.38), top, materials, 16)
    add_foot_rubble(bpy, rng, parts, name, (-1.35, 1.35, -0.49), materials["MAT_Cliff_Rubble"], 26)
    streaks = (-0.82, 0.12, 0.74) if variant == 0 else (-1.02, -0.30, 0.52, 1.06)
    add_vertical_streaks(bpy, parts, name, streaks, -0.50, materials["MAT_Cliff_Mineral"])
    return join_parts(bpy, parts, name)


def make_inner_corner(bpy, materials, rng):
    parts = []
    outline = [(-1.30, -0.52), (0.52, -0.52), (0.52, 0.54), (1.30, 0.54), (1.30, 1.30), (-0.48, 1.30), (-0.52, 0.52), (-1.30, 0.52)]
    outlines = [outline, [(x * 0.985, y * 0.985) for x, y in outline]]
    top = add_strata_stack(bpy, rng, parts, MODULES[2], outlines, materials, 2)
    add_top_dust(bpy, parts, MODULES[2] + "_TopDust", outlines[-1], top, materials["MAT_Cliff_Dust"])
    add_top_chips(bpy, rng, parts, MODULES[2], (-1.15, 0.38, -0.38, 0.38), top, materials, 10)
    add_foot_rubble(bpy, rng, parts, MODULES[2] + "_A", (-1.34, 0.54, -0.54), materials["MAT_Cliff_Rubble"], 18)
    for index in range(18):
        x = 0.55 + rng.uniform(-0.10, 0.20)
        y = rng.uniform(0.48, 1.34)
        size = rng.uniform(0.055, 0.13)
        parts.append(angular_chunk(bpy, rng, f"{MODULES[2]}_B_Rubble_{index:02d}", (x, y, 0.02), (size, size * 0.8, size * 0.55), materials["MAT_Cliff_Rubble"]))
    add_vertical_streaks(bpy, parts, MODULES[2], (-0.78, 0.04), -0.53, materials["MAT_Cliff_Mineral"])
    return join_parts(bpy, parts, MODULES[2])


def make_outer_corner(bpy, materials, rng):
    parts = []
    outline = [(-1.30, -0.52), (0.22, -0.55), (0.58, -0.28), (0.58, 1.30), (-0.28, 1.30), (-0.34, 0.36), (-1.30, 0.34)]
    outlines = [outline, [(x * 0.99, y * 0.99) for x, y in outline]]
    top = add_strata_stack(bpy, rng, parts, MODULES[3], outlines, materials, 3)
    add_top_dust(bpy, parts, MODULES[3] + "_TopDust", outlines[-1], top, materials["MAT_Cliff_Dust"])
    add_top_chips(bpy, rng, parts, MODULES[3], (-1.12, 0.36, -0.38, 0.25), top, materials, 10)
    add_foot_rubble(bpy, rng, parts, MODULES[3] + "_A", (-1.34, 0.55, -0.56), materials["MAT_Cliff_Rubble"], 20)
    for index in range(18):
        x = 0.58 + rng.uniform(-0.10, 0.18)
        y = rng.uniform(-0.25, 1.34)
        size = rng.uniform(0.050, 0.13)
        parts.append(angular_chunk(bpy, rng, f"{MODULES[3]}_B_Rubble_{index:02d}", (x, y, 0.02), (size, size * 0.78, size * 0.55), materials["MAT_Cliff_Rubble"]))
    add_vertical_streaks(bpy, parts, MODULES[3], (-0.84, -0.12, 0.36), -0.55, materials["MAT_Cliff_Mineral"])
    return join_parts(bpy, parts, MODULES[3])


def make_endcap(bpy, materials, rng):
    parts = []
    outline = [(-1.20, -0.50), (0.62, -0.50), (1.08, -0.26), (1.18, 0.0), (1.04, 0.30), (0.58, 0.50), (-1.20, 0.50)]
    outlines = [outline, [(x * 0.985, y * 0.98) for x, y in outline]]
    top = add_strata_stack(bpy, rng, parts, MODULES[4], outlines, materials, 4)
    add_top_dust(bpy, parts, MODULES[4] + "_TopDust", outlines[-1], top, materials["MAT_Cliff_Dust"])
    add_top_chips(bpy, rng, parts, MODULES[4], (-1.05, 0.90, -0.36, 0.36), top, materials, 14)
    add_foot_rubble(bpy, rng, parts, MODULES[4], (-1.24, 1.13, -0.52), materials["MAT_Cliff_Rubble"], 28)
    add_vertical_streaks(bpy, parts, MODULES[4], (-0.72, 0.18, 0.68), -0.51, materials["MAT_Cliff_Mineral"])
    return join_parts(bpy, parts, MODULES[4])


def make_topcap(bpy, materials, rng):
    parts = []
    outline = [(-1.28, -1.18), (1.28, -1.18), (1.28, 1.18), (-1.28, 1.18)]
    # A fill piece deliberately remains a vertical-sided plateau, never a traversable ramp.
    outlines = [outline, [(-1.26, -1.20), (1.27, -1.16), (1.25, 1.19), (-1.27, 1.16)]]
    top = add_strata_stack(bpy, rng, parts, MODULES[5], outlines, materials, 5)
    add_top_dust(bpy, parts, MODULES[5] + "_TopDust", outlines[-1], top, materials["MAT_Cliff_Dust"])
    add_top_chips(bpy, rng, parts, MODULES[5], (-1.12, 1.12, -1.02, 1.02), top, materials, 22)
    add_foot_rubble(bpy, rng, parts, MODULES[5], (-1.30, 1.30, -1.20), materials["MAT_Cliff_Rubble"], 24)
    add_vertical_streaks(bpy, parts, MODULES[5], (-0.86, -0.12, 0.72), -1.20, materials["MAT_Cliff_Mineral"])
    return join_parts(bpy, parts, MODULES[5])


def triangle_count(obj):
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


def export_fbx(bpy, obj, output_path):
    bpy.ops.object.select_all(action="DESELECT")
    obj.hide_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    output_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=str(output_path), use_selection=True, object_types={"MESH"}, use_mesh_modifiers=True,
        add_leaf_bones=False, apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z", axis_up="Y", bake_space_transform=False, mesh_smooth_type="FACE", path_mode="AUTO",
    )
    obj.select_set(False)
    obj.hide_set(True)


def point_camera(camera, target, Vector):
    direction = Vector(target) - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def create_wire_material(bpy):
    material = bpy.data.materials.new("MAT_QA_Cliff_Wireframe")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    diffuse = nodes.new("ShaderNodeBsdfDiffuse")
    diffuse.inputs["Color"].default_value = (0.22, 0.19, 0.16, 1.0)
    wire = nodes.new("ShaderNodeWireframe")
    wire.inputs["Size"].default_value = 0.007
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = (0.008, 0.008, 0.008, 1.0)
    emission.inputs["Strength"].default_value = 0.4
    mix = nodes.new("ShaderNodeMixShader")
    links.new(wire.outputs["Fac"], mix.inputs[0])
    links.new(diffuse.outputs["BSDF"], mix.inputs[1])
    links.new(emission.outputs["Emission"], mix.inputs[2])
    links.new(mix.outputs["Shader"], output.inputs["Surface"])
    return material


def build(args):
    import bpy
    from mathutils import Vector

    if not args.reference.is_file():
        raise FileNotFoundError(f"Approved reference missing: {args.reference}")
    for path in (args.source_root, args.asset_root, args.qa_root):
        path.mkdir(parents=True, exist_ok=True)
    texture_root = args.asset_root.parent
    texture_paths = {key: texture_root / f"T_Terrain_Cliff_{suffix}.png" for key, suffix in (
        ("base_color", "BaseColor"), ("normal", "Normal"), ("mask", "Mask"), ("height", "Height"))}
    wasteland_root = texture_root.parent / "Wasteland"
    wasteland_paths = {key: wasteland_root / f"T_Terrain_Wasteland_{suffix}.png" for key, suffix in (
        ("base_color", "BaseColor"), ("normal", "Normal"), ("mask", "Mask"), ("height", "Height"))}
    for label, paths in (("Cliff", texture_paths), ("Wasteland", wasteland_paths)):
        for key, path in paths.items():
            if not path.is_file():
                raise FileNotFoundError(f"{label} {key} map missing: {path}")

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
    scene["wastecity_asset"] = "Cliff six-module first-pass visual kit"
    scene["approved_reference"] = REFERENCE_NAME
    scene["approved_reference_sha256"] = REFERENCE_SHA256
    scene["generator_seed"] = SEED
    scene["module_count"] = 6
    scene["material_roles"] = "Strata/Fracture/Dust/Rubble/Mineral"
    scene["gameplay_truth"] = "none"
    scene["colliders"] = "none"
    scene["unity_axis"] = "Y-up, -Z forward via FBX export"
    materials = create_materials(bpy, texture_paths)
    rng = random.Random(SEED)
    assets = (
        make_straight(bpy, materials, rng, MODULES[0], 0),
        make_straight(bpy, materials, rng, MODULES[1], 1),
        make_inner_corner(bpy, materials, rng),
        make_outer_corner(bpy, materials, rng),
        make_endcap(bpy, materials, rng),
        make_topcap(bpy, materials, rng),
    )
    for obj in assets:
        set_origin_at_base(bpy, obj)
        smart_uv(bpy, obj)
        obj.location = (0.0, 0.0, 0.0)
        obj.rotation_euler = (0.0, 0.0, 0.0)
        obj["gameplay_truth"] = "none"
        obj["collider"] = "none"
        obj["module_height_m"] = round(obj.dimensions.z, 4)
        obj["triangle_count"] = triangle_count(obj)
        count = triangle_count(obj)
        if count < 200 or count > 2000:
            raise RuntimeError(f"{obj.name} triangle count {count} outside 200..2000")
        export_fbx(bpy, obj, args.asset_root / f"{obj.name}.fbx")

    preview_objects = []
    positions = ((-3.1, 1.45, 0.0), (0.0, 1.45, 0.0), (3.1, 1.45, 0.0), (-3.1, -1.45, 0.0), (0.0, -1.45, 0.0), (3.1, -1.45, 0.0))
    for source, position in zip(assets, positions):
        clone = source.copy()
        clone.data = source.data.copy()
        clone.name = "PREVIEW_" + source.name
        bpy.context.collection.objects.link(clone)
        clone.hide_set(False)
        clone.hide_render = False
        clone.location = position
        preview_objects.append(clone)
        source.hide_set(True)
        source.hide_render = True

    world = scene.world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.105, 0.092, 0.080, 1.0)
    background.inputs["Strength"].default_value = 0.44
    backdrop_material = bpy.data.materials.new("MAT_QA_Cliff_Backdrop")
    backdrop_material.diffuse_color = (0.16, 0.14, 0.12, 1.0)
    backdrop_material.use_nodes = True
    backdrop_material.node_tree.nodes.get("Principled BSDF").inputs["Base Color"].default_value = backdrop_material.diffuse_color
    backdrop_material.node_tree.nodes.get("Principled BSDF").inputs["Roughness"].default_value = 0.94
    wasteland_material = create_wasteland_material(bpy, wasteland_paths)
    bpy.ops.mesh.primitive_plane_add(size=18.0, location=(0.0, 0.0, -0.02))
    backdrop = bpy.context.object
    backdrop.name = "QA_Backdrop"
    backdrop.data.materials.append(backdrop_material)

    def add_light(name, energy, location, color, size):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.shape = "DISK"
        data.size = size
        data.color = color
        light = bpy.data.objects.new(name, data)
        scene.collection.objects.link(light)
        light.location = location

    add_light("Key_Area", 1350.0, (4.0, -6.0, 9.5), (1.0, 0.82, 0.63), 7.0)
    add_light("Fill_Area", 760.0, (-5.0, -1.0, 7.0), (0.57, 0.66, 0.76), 6.0)
    add_light("Rim_Area", 720.0, (5.5, 5.0, 7.5), (0.78, 0.72, 0.62), 5.0)
    camera_data = bpy.data.cameras.new("Camera_Cliff_ModuleKit")
    camera = bpy.data.objects.new("Camera_Cliff_ModuleKit", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 8.6
    camera.location = (9.0, -12.0, 9.2)
    point_camera(camera, (0.0, 0.0, 0.55), Vector)
    scene.render.filepath = str(args.qa_root / "QA_Cliff_ModuleKit_DefaultOrtho.png")
    bpy.ops.render.render(write_still=True)

    backdrop.data.materials[0] = wasteland_material
    scene.render.filepath = str(args.qa_root / "QA_Cliff_ModuleKit_WastelandContext.png")
    bpy.ops.render.render(write_still=True)

    camera.location = (0.0, 0.0, 14.0)
    camera.data.ortho_scale = 8.2
    point_camera(camera, (0.0, 0.0, 0.0), Vector)
    scene.render.filepath = str(args.qa_root / "QA_Cliff_ModuleKit_Top.png")
    bpy.ops.render.render(write_still=True)

    wire_material = create_wire_material(bpy)
    originals = []
    for clone in preview_objects:
        originals.append(list(clone.data.materials))
        clone.data.materials.clear()
        clone.data.materials.append(wire_material)
        for polygon in clone.data.polygons:
            polygon.material_index = 0
    backdrop.data.materials[0] = backdrop_material
    camera.location = (9.0, -12.0, 9.2)
    camera.data.ortho_scale = 8.6
    point_camera(camera, (0.0, 0.0, 0.55), Vector)
    scene.render.filepath = str(args.qa_root / "QA_Cliff_ModuleKit_Wireframe.png")
    bpy.ops.render.render(write_still=True)
    for clone, mats, source in zip(preview_objects, originals, assets):
        clone.data.materials.clear()
        for material in mats:
            clone.data.materials.append(material)
        for polygon, source_polygon in zip(clone.data.polygons, source.data.polygons):
            polygon.material_index = source_polygon.material_index

    # Additional assembled visual validates modular joins and a closed corner without scene integration.
    for clone in preview_objects:
        clone.hide_render = True
    assembled = []
    placements = (
        (assets[0], (-2.6, 0.0, 0.0), 0.0),
        (assets[1], (0.0, 0.0, 0.0), 0.0),
        (assets[2], (2.6, 0.0, 0.0), 0.0),
        (assets[0], (3.38, 2.25, 0.0), math.pi * 0.5),
    )
    for index, (source, location, rotation) in enumerate(placements):
        clone = source.copy()
        clone.data = source.data.copy()
        clone.name = f"ASSEMBLED_{index:02d}_{source.name}"
        bpy.context.collection.objects.link(clone)
        clone.hide_set(False)
        clone.hide_render = False
        clone.location = location
        clone.rotation_euler[2] = rotation
        assembled.append(clone)
    backdrop.data.materials[0] = wasteland_material
    camera.location = (8.4, -11.6, 8.6)
    camera.data.ortho_scale = 8.5
    point_camera(camera, (0.5, 0.8, 0.55), Vector)
    scene.render.filepath = str(args.qa_root / "QA_Cliff_ModuleKit_Assembled.png")
    bpy.ops.render.render(write_still=True)

    reference = bpy.data.images.load(str(args.reference), check_existing=True)
    reference.pack()
    reference.use_fake_user = True
    script = bpy.data.texts.new("generate_cliff_module_kit.py")
    script.write(Path(__file__).read_text(encoding="utf-8"))
    notes = bpy.data.texts.new("README_Cliff_ModuleKit.txt")
    notes.write(
        "Waste City Cliff six-module first-pass visual kit.\n"
        "Built after user approval of the combined material/module reference.\n"
        "Six independent meshes; FBX is the primary Unity delivery format.\n"
        "Five visible material roles: strata, fresh fracture, dust, rubble, mineral.\n"
        "Foot rubble hides visual seams without creating a traversable ramp.\n"
        "No Collider, Rigidbody, gameplay component, stable ID, or gameplay truth.\n"
        "Blender is Z-up; FBX exports -Z Forward / Y Up at scale 1.0.\n"
    )
    blend_path = args.source_root / "Cliff_ModuleKit.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), compress=True)
    print(f"BLEND={blend_path}")
    for obj in assets:
        print(f"MODULE={obj.name}|TRIS={triangle_count(obj)}|SIZE={obj.dimensions.x:.4f},{obj.dimensions.y:.4f},{obj.dimensions.z:.4f}|MATERIALS={len(obj.data.materials)}")


def main():
    build(parse_args())


if __name__ == "__main__":
    main()
