#!/usr/bin/env python3
"""Validate the repository boundary of the IDEA-0004 first art delivery."""

from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import PurePosixPath


REPOSITORY_ROOTS = ("ArtSource/FirstPass/", "Assets/_Game/Art/FirstPass/")
LFS_EXTENSIONS = {".blend", ".fbx", ".kra", ".ora", ".png", ".psd", ".wav"}
TERRAIN_TYPES = {
    "Wasteland",
    "Rocky",
    "Wetland",
    "Crystal",
    "Ruins",
    "DeepWater",
    "Cliff",
}
TERRAIN_MAPS = {"BaseColor", "Normal", "Mask", "Height"}
TERRAIN_ROOT = "Assets/_Game/Art/FirstPass/Environment/Terrain/"


def git(*args: str, binary: bool = False) -> bytes | str:
    result = subprocess.run(
        ["git", *args],
        check=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    return result.stdout if binary else result.stdout.decode("utf-8")


def tracked_paths(treeish: str | None) -> list[str]:
    if treeish is None:
        output = git("ls-files", "--cached")
    else:
        output = git("ls-tree", "-r", "--name-only", treeish)
    return [line for line in str(output).splitlines() if line]


def blob(path: str, treeish: str | None) -> bytes:
    object_name = f":{path}" if treeish is None else f"{treeish}:{path}"
    return bytes(git("show", object_name, binary=True))


def lfs_attribute(path: str) -> str:
    output = str(git("check-attr", "--cached", "filter", "--", path)).strip()
    return output.rsplit(": ", 1)[-1]


def is_lfs_pointer(content: bytes) -> bool:
    if len(content) > 1024:
        return False
    lines = content.decode("ascii", errors="replace").splitlines()
    return (
        len(lines) == 3
        and lines[0] == "version https://git-lfs.github.com/spec/v1"
        and lines[1].startswith("oid sha256:")
        and len(lines[1]) == len("oid sha256:") + 64
        and lines[2].startswith("size ")
        and lines[2][5:].isdigit()
    )


def validate_lfs(paths: list[str], treeish: str | None) -> list[str]:
    issues: list[str] = []
    for path in paths:
        pure = PurePosixPath(path)
        if not path.startswith(REPOSITORY_ROOTS) or pure.suffix.lower() not in LFS_EXTENSIONS:
            continue
        if lfs_attribute(path) != "lfs":
            issues.append(f"LFS_ATTRIBUTE {path}: filter is not lfs")
            continue
        if not is_lfs_pointer(blob(path, treeish)):
            issues.append(f"LFS_POINTER {path}: indexed blob is not an LFS v1 pointer")
    return issues


def validate_runtime_inventory(paths: list[str]) -> list[str]:
    issues: list[str] = []
    path_set = set(paths)
    expected_textures = {
        f"{TERRAIN_ROOT}{terrain}/T_Terrain_{terrain}_{map_name}.png"
        for terrain in TERRAIN_TYPES
        for map_name in TERRAIN_MAPS
    }
    actual_textures = {
        path
        for path in paths
        if path.startswith(TERRAIN_ROOT) and path.lower().endswith(".png")
    }
    missing = sorted(expected_textures - actual_textures)
    unexpected = sorted(actual_textures - expected_textures)
    for path in missing:
        issues.append(f"TERRAIN_MISSING {path}")
    for path in unexpected:
        issues.append(f"TERRAIN_UNEXPECTED {path}")

    model_expectations = {
        "Ruins": 8,
        "Cliff": 6,
    }
    for terrain, expected_count in model_expectations.items():
        prefix = f"{TERRAIN_ROOT}{terrain}/Models/"
        models = sorted(path for path in path_set if path.startswith(prefix) and path.lower().endswith(".fbx"))
        if len(models) != expected_count:
            issues.append(
                f"MODEL_COUNT {terrain}: expected {expected_count} FBX files, found {len(models)}"
            )
    return issues


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--treeish",
        help="Validate blobs from a committed tree instead of the current Git index.",
    )
    args = parser.parse_args()

    try:
        paths = tracked_paths(args.treeish)
        issues = validate_lfs(paths, args.treeish)
        issues.extend(validate_runtime_inventory(paths))
    except subprocess.CalledProcessError as error:
        sys.stderr.write(error.stderr.decode("utf-8", errors="replace"))
        return 2

    if issues:
        for issue in issues:
            print(issue)
        print(f"FAILED: {len(issues)} first-art delivery issue(s)")
        return 1

    print("PASS: first-art delivery LFS and runtime inventory contracts are satisfied")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
