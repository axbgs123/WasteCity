from __future__ import annotations

import argparse
import sys
from pathlib import Path


MODULES = (
    "SM_Cliff_Straight_A",
    "SM_Cliff_Straight_B",
    "SM_Cliff_InnerCorner",
    "SM_Cliff_OuterCorner",
    "SM_Cliff_EndCap",
    "SM_Cliff_TopCap",
)


def parse_args():
    raw = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--mode", choices=("golden", "kit", "fbx"), required=True)
    parser.add_argument("--path", type=Path)
    return parser.parse_args(raw)


def mesh_metrics(obj):
    obj.data.calc_loop_triangles()
    minimum_z = min(vertex.co.z for vertex in obj.data.vertices)
    return len(obj.data.loop_triangles), len(obj.data.uv_layers), len(obj.data.materials), minimum_z


def validate_golden(bpy):
    scene = bpy.context.scene
    assert scene.get("wastecity_asset") == "Cliff terrain first-pass material sample"
    assert scene.get("gameplay_truth") == "none"
    assert scene.get("coverage_ratios") == "55/20/15/7/3"
    assert scene.get("texture_resolution") == 2048
    packed = [image for image in bpy.data.images if image.packed_file is not None]
    assert len(packed) >= 4
    print(
        "GOLDEN_OK"
        f"|asset={scene.get('wastecity_asset')}"
        f"|gameplay_truth={scene.get('gameplay_truth')}"
        f"|ratios={scene.get('coverage_ratios')}"
        f"|packed_images={len(packed)}"
    )


def validate_kit(bpy):
    scene = bpy.context.scene
    assert scene.get("module_count") == 6
    assert scene.get("gameplay_truth") == "none"
    assert scene.get("colliders") == "none"
    assets = [bpy.data.objects.get(name) for name in MODULES]
    assert all(asset is not None and asset.type == "MESH" for asset in assets)
    for obj in assets:
        triangles, uv_sets, materials, minimum_z = mesh_metrics(obj)
        assert 200 <= triangles <= 2000
        assert uv_sets == 1
        assert materials == 5
        assert abs(minimum_z) <= 1e-5
        assert tuple(round(value, 6) for value in obj.location) == (0.0, 0.0, 0.0)
        print(
            f"KIT_MODULE_OK|{obj.name}|tris={triangles}|uv={uv_sets}|materials={materials}"
            f"|minz={minimum_z:.6f}|size={obj.dimensions.x:.4f},{obj.dimensions.y:.4f},{obj.dimensions.z:.4f}"
        )
    print(
        "KIT_OK"
        f"|gameplay_truth={scene.get('gameplay_truth')}"
        f"|colliders={scene.get('colliders')}"
        f"|modules={len(assets)}"
    )


def validate_fbx(bpy, directory: Path):
    assert directory is not None and directory.is_dir()
    for name in MODULES:
        path = directory / f"{name}.fbx"
        assert path.is_file()
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=str(path))
        meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
        assert len(meshes) == 1
        obj = meshes[0]
        triangles, uv_sets, materials, minimum_z = mesh_metrics(obj)
        assert obj.name == name
        assert 200 <= triangles <= 2000
        assert uv_sets == 1
        assert materials == 5
        assert abs(minimum_z) <= 1e-5
        print(
            f"FBX_OK|{path.name}|tris={triangles}|uv={uv_sets}|materials={materials}"
            f"|minz={minimum_z:.6f}|size={obj.dimensions.x:.4f},{obj.dimensions.y:.4f},{obj.dimensions.z:.4f}"
        )


def main():
    import bpy

    args = parse_args()
    if args.mode == "golden":
        validate_golden(bpy)
    elif args.mode == "kit":
        validate_kit(bpy)
    else:
        validate_fbx(bpy, args.path.resolve() if args.path else None)


if __name__ == "__main__":
    main()
