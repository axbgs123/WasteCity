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


def create_materials(bpy):
    specs = {
        "MAT_Ruins_Concrete": ((0.105, 0.095, 0.082, 1.0), 0.84, 0.0),
        "MAT_Ruins_DarkFloor": ((0.055, 0.052, 0.047, 1.0), 0.78, 0.0),
        "MAT_Ruins_Dust": ((0.185, 0.115, 0.055, 1.0), 0.92, 0.0),
        "MAT_Ruins_Rust": ((0.235, 0.070, 0.025, 1.0), 0.68, 0.42),
        "MAT_Ruins_Marking": ((0.330, 0.185, 0.035, 1.0), 0.80, 0.0),
        "MAT_Ruins_DrainDark": ((0.030, 0.028, 0.025, 1.0), 0.70, 0.0),
    }
    materials = {}
    for name, (color, roughness, metallic) in specs.items():
        material = bpy.data.materials.new(name)
        material.diffuse_color = color
        material.use_nodes = True
        bsdf = material.node_tree.nodes.get("Principled BSDF")
        bsdf.inputs["Base Color"].default_value = color
        bsdf.inputs["Roughness"].default_value = roughness
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Specular IOR Level"].default_value = 0.24
        noise = material.node_tree.nodes.new("ShaderNodeTexNoise")
        noise.inputs["Scale"].default_value = 7.0 if "Concrete" in name else 11.0
        noise.inputs["Detail"].default_value = 2.0
        noise.inputs["Roughness"].default_value = 0.72
        bump = material.node_tree.nodes.new("ShaderNodeBump")
        bump.inputs["Strength"].default_value = 0.16 if "Concrete" in name else 0.09
        bump.inputs["Distance"].default_value = 0.035
        material.node_tree.links.new(noise.outputs["Fac"], bump.inputs["Height"])
        material.node_tree.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
        materials[name] = material
    return materials


def assign_material(obj, material) -> None:
    if len(obj.data.materials) == 0:
        obj.data.materials.append(material)
    else:
        obj.data.materials[0] = material


def create_irregular_prism(bpy, name, points, height, material, z=0.0):
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


def create_beveled_cube(bpy, name, size, location, rotation, material, bevel=0.015):
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
    obj.rotation_euler[2] = rng.uniform(-math.pi, math.pi)
    return obj


def create_tube(bpy, name, length, radius, thickness, material, location=(0.0, 0.0, 0.0), seed=0):
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
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
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


def make_cracked_slab(bpy, materials, rng):
    parts = []
    points = [(-0.47, -0.42), (-0.15, -0.49), (0.35, -0.45), (0.49, -0.18), (0.44, 0.35), (0.18, 0.48), (-0.30, 0.44), (-0.50, 0.12)]
    parts.append(create_irregular_prism(bpy, "Slab_Base", points, 0.075, materials["MAT_Ruins_DarkFloor"]))
    for index, (x, y, sx, sy, angle) in enumerate((
        (-0.22, 0.17, 0.38, 0.24, -0.12),
        (0.17, 0.18, 0.34, 0.26, 0.16),
        (-0.12, -0.18, 0.30, 0.23, 0.10),
        (0.25, -0.18, 0.28, 0.21, -0.20),
    )):
        parts.append(create_beveled_cube(bpy, f"Slab_Plate_{index}", (sx, sy, 0.035), (x, y, 0.086), (0.0, 0.0, angle), materials["MAT_Ruins_Concrete"], 0.018))
    add_scattered_chunks(bpy, rng, parts, "Slab_Chip", 7, (0.50, 0.45), (0.072, 0.09), (0.025, 0.055), [materials["MAT_Ruins_Concrete"], materials["MAT_Ruins_Dust"]])
    return join_parts(bpy, parts, MODULES[0][0])


def make_rubble_a(bpy, materials, rng):
    parts = []
    add_scattered_chunks(bpy, rng, parts, "RubbleA", 22, (0.38, 0.31), (0.0, 0.12), (0.055, 0.14), [materials["MAT_Ruins_Concrete"], materials["MAT_Ruins_Dust"]])
    return join_parts(bpy, parts, MODULES[1][0])


def make_rubble_b(bpy, materials, rng):
    parts = []
    add_scattered_chunks(bpy, rng, parts, "RubbleB", 17, (0.57, 0.24), (0.0, 0.10), (0.05, 0.13), [materials["MAT_Ruins_Concrete"], materials["MAT_Ruins_Dust"]])
    for index, (x, y, sx, sy, angle) in enumerate(((-0.25, 0.02, 0.42, 0.18, -0.18), (0.22, -0.03, 0.36, 0.20, 0.14), (0.03, 0.10, 0.30, 0.16, -0.05))):
        parts.append(create_beveled_cube(bpy, f"RubbleB_Slab_{index}", (sx, sy, 0.045), (x, y, 0.13 + index * 0.015), (0.02, -0.02, angle), materials["MAT_Ruins_Concrete"], 0.012))
    for index, y in enumerate((-0.15, 0.16)):
        parts.append(create_beveled_cube(bpy, f"RubbleB_Metal_{index}", (0.30, 0.025, 0.018), (0.05, y, 0.13), (0.0, 0.12, rng.uniform(-0.5, 0.5)), materials["MAT_Ruins_Rust"], 0.006))
    return join_parts(bpy, parts, MODULES[2][0])


def make_rebar_block(bpy, materials, rng):
    parts = [create_beveled_cube(bpy, "Block_Main", (0.55, 0.50, 0.34), (-0.05, 0.0, 0.17), (0.0, 0.0, 0.0), materials["MAT_Ruins_Concrete"], 0.035)]
    add_scattered_chunks(bpy, rng, parts, "Block_Chip", 8, (0.34, 0.30), (0.0, 0.04), (0.035, 0.075), [materials["MAT_Ruins_Concrete"], materials["MAT_Ruins_Dust"]])
    for index, y in enumerate((-0.14, 0.0, 0.14)):
        parts.append(create_bent_rebar(bpy, f"Block_Rebar_{index}", y, materials["MAT_Ruins_Rust"]))
    return join_parts(bpy, parts, MODULES[3][0])


def make_broken_pipe(bpy, materials, rng):
    parts = [create_tube(bpy, "Pipe_Main", 0.72, 0.27, 0.055, materials["MAT_Ruins_Rust"], seed=SEED + 50)]
    add_scattered_chunks(bpy, rng, parts, "Pipe_Debris", 7, (0.45, 0.32), (0.0, 0.025), (0.025, 0.060), [materials["MAT_Ruins_Concrete"], materials["MAT_Ruins_Dust"]])
    dust = create_irregular_prism(bpy, "Pipe_Dust", [(-0.18, -0.13), (0.16, -0.15), (0.21, 0.10), (-0.15, 0.12)], 0.012, materials["MAT_Ruins_Dust"], z=0.012)
    dust.rotation_euler[1] = math.radians(82.0)
    dust.location = (-0.34, 0.0, 0.27)
    parts.append(dust)
    return join_parts(bpy, parts, MODULES[4][0])


def make_drain(bpy, materials, rng):
    parts = [
        create_beveled_cube(bpy, "Drain_Floor", (1.00, 0.40, 0.055), (0.0, 0.0, 0.0275), (0.0, 0.0, 0.0), materials["MAT_Ruins_DrainDark"], 0.012),
        create_beveled_cube(bpy, "Drain_Lip_L", (1.00, 0.105, 0.15), (0.0, -0.165, 0.075), (0.0, 0.0, 0.0), materials["MAT_Ruins_Concrete"], 0.018),
        create_beveled_cube(bpy, "Drain_Lip_R", (1.00, 0.105, 0.15), (0.0, 0.165, 0.075), (0.0, 0.0, 0.0), materials["MAT_Ruins_Concrete"], 0.018),
    ]
    add_scattered_chunks(bpy, rng, parts, "Drain_Chip", 9, (0.52, 0.25), (0.02, 0.07), (0.022, 0.050), [materials["MAT_Ruins_Concrete"], materials["MAT_Ruins_Dust"]])
    return join_parts(bpy, parts, MODULES[5][0])


def make_boundary_edge(bpy, materials, rng):
    parts = []
    for index, (x, y, sx, angle) in enumerate(((-0.34, 0.04, 0.34, 0.08), (0.00, 0.00, 0.33, -0.05), (0.34, -0.04, 0.34, 0.10))):
        parts.append(create_beveled_cube(bpy, f"Edge_Curb_{index}", (sx, 0.22, 0.14), (x, y, 0.07), (0.0, 0.0, angle), materials["MAT_Ruins_Concrete"], 0.018))
    plate = create_irregular_prism(bpy, "Edge_Ground", [(-0.52, -0.25), (0.48, -0.22), (0.52, 0.16), (0.13, 0.25), (-0.42, 0.20)], 0.035, materials["MAT_Ruins_Dust"])
    parts.append(plate)
    add_scattered_chunks(bpy, rng, parts, "Edge_Chip", 10, (0.52, 0.30), (0.02, 0.06), (0.022, 0.055), [materials["MAT_Ruins_Concrete"], materials["MAT_Ruins_Dust"]])
    return join_parts(bpy, parts, MODULES[6][0])


def make_marking_plate(bpy, materials, rng):
    parts = []
    points = [(-0.45, -0.35), (0.36, -0.36), (0.45, -0.18), (0.40, 0.34), (0.10, 0.38), (-0.42, 0.31), (-0.48, 0.02)]
    parts.append(create_irregular_prism(bpy, "Marking_Base", points, 0.035, materials["MAT_Ruins_DarkFloor"]))
    for index, x in enumerate((-0.14, 0.08)):
        stripe = create_beveled_cube(bpy, f"Marking_Stripe_{index}", (0.10, 0.66, 0.009), (x, 0.0, 0.040), (0.0, 0.0, -0.05), materials["MAT_Ruins_Marking"], 0.008)
        parts.append(stripe)
    add_scattered_chunks(bpy, rng, parts, "Marking_Chip", 8, (0.45, 0.35), (0.035, 0.055), (0.018, 0.040), [materials["MAT_Ruins_Dust"], materials["MAT_Ruins_Concrete"]])
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
    from mathutils import Vector

    if not args.reference.is_file():
        raise FileNotFoundError(f"Approved model reference missing: {args.reference}")
    args.source_root.mkdir(parents=True, exist_ok=True)
    args.asset_root.mkdir(parents=True, exist_ok=True)
    args.qa_root.mkdir(parents=True, exist_ok=True)
    clear_scene(bpy)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1920
    scene.render.resolution_y = 1080
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
    materials = create_materials(bpy)
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
    positions = ((-2.7, 1.45), (-0.9, 1.45), (0.9, 1.45), (2.7, 1.45), (-2.7, -1.35), (-0.9, -1.35), (0.9, -1.35), (2.7, -1.35))
    for obj, (x, y) in zip(assets, positions):
        clone = obj.copy()
        clone.data = obj.data.copy()
        clone.name = "PREVIEW_" + obj.name
        bpy.context.collection.objects.link(clone)
        clone.hide_render = False
        clone.hide_set(False)
        clone.location = (x, y, 0.0)
        preview_objects.append(clone)
        obj.hide_render = True
        obj.hide_set(True)

    world = scene.world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.030, 0.027, 0.024, 1.0)
    background.inputs["Strength"].default_value = 0.30
    floor_material = bpy.data.materials.new("MAT_QA_Backdrop")
    floor_material.diffuse_color = (0.075, 0.068, 0.060, 1.0)
    floor_material.use_nodes = True
    floor_bsdf = floor_material.node_tree.nodes.get("Principled BSDF")
    floor_bsdf.inputs["Base Color"].default_value = floor_material.diffuse_color
    floor_bsdf.inputs["Roughness"].default_value = 0.94
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

    add_light("Key_Area", 900.0, (3.0, -4.5, 8.5), (1.0, 0.84, 0.68), 7.0)
    add_light("Fill_Area", 450.0, (-5.0, -0.5, 6.0), (0.58, 0.62, 0.66), 6.0)
    add_light("Rim_Area", 500.0, (4.5, 5.0, 7.0), (0.72, 0.68, 0.58), 5.0)
    camera_data = bpy.data.cameras.new("Camera_Ruins_ModuleKit")
    camera = bpy.data.objects.new("Camera_Ruins_ModuleKit", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 8.6
    camera.location = (6.8, -9.8, 8.0)
    point_camera(camera, (0.0, 0.0, 0.15), Vector)
    scene.render.filepath = str(args.qa_root / "QA_Ruins_ModuleKit_DefaultOrtho.png")
    bpy.ops.render.render(write_still=True)

    camera.location = (0.0, 0.0, 12.0)
    camera.data.ortho_scale = 8.2
    point_camera(camera, (0.0, 0.0, 0.0), Vector)
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
    camera.location = (6.8, -9.8, 8.0)
    camera.data.ortho_scale = 8.6
    point_camera(camera, (0.0, 0.0, 0.15), Vector)
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
