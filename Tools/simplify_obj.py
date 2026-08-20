"""Create a lightweight, UV-preserving OBJ using vertex clustering.

This is intentionally dependency-free so the WebGL asset pipeline remains
reproducible on the project's supported Windows/Unity setup. Source OBJ files
are never modified.
"""

from __future__ import annotations

import argparse
import math
from array import array
from pathlib import Path


def read_vertices(path: Path):
    values = array("f")
    minimum = [math.inf, math.inf, math.inf]
    maximum = [-math.inf, -math.inf, -math.inf]
    header = []
    object_name = path.stem
    with path.open("r", encoding="utf-8", errors="replace") as source:
        for line in source:
            if line.startswith("v "):
                parts = line.split()
                point = tuple(float(value) for value in parts[1:4])
                values.extend(point)
                for axis in range(3):
                    minimum[axis] = min(minimum[axis], point[axis])
                    maximum[axis] = max(maximum[axis], point[axis])
            elif line.startswith("mtllib "):
                header.append(line.rstrip())
            elif line.startswith("o ") and object_name == path.stem:
                object_name = line[2:].strip() or object_name
    return values, minimum, maximum, header, object_name


def cluster_vertices(values, minimum, maximum, resolution):
    extent = [max(maximum[i] - minimum[i], 1e-9) for i in range(3)]
    longest = max(extent)
    cells = [max(1, round(resolution * size / longest)) for size in extent]
    representatives = {}
    remap = array("I")
    for index in range(len(values) // 3):
        base = index * 3
        key = tuple(
            min(
                cells[axis] - 1,
                int((values[base + axis] - minimum[axis]) /
                    extent[axis] * cells[axis]),
            )
            for axis in range(3)
        )
        representative = representatives.setdefault(key, index + 1)
        remap.append(representative)
    return remap


def parse_corner(text, vertex_count):
    parts = text.split("/")
    vertex = int(parts[0])
    if vertex < 0:
        vertex = vertex_count + vertex + 1
    texcoord = int(parts[1]) if len(parts) > 1 and parts[1] else 0
    normal = int(parts[2]) if len(parts) > 2 and parts[2] else 0
    return vertex, texcoord, normal


def collect_faces(path, vertex_remap, max_faces):
    faces = []
    seen = set()
    material = ""
    vertex_count = len(vertex_remap)
    with path.open("r", encoding="utf-8", errors="replace") as source:
        for line in source:
            if line.startswith("usemtl "):
                material = line[7:].strip()
                continue
            if not line.startswith("f "):
                continue
            corners = [parse_corner(item, vertex_count)
                       for item in line.split()[1:]]
            for offset in range(1, len(corners) - 1):
                triangle = [corners[0], corners[offset], corners[offset + 1]]
                triangle = [
                    (vertex_remap[item[0] - 1], item[1], item[2])
                    for item in triangle
                ]
                vertex_key = tuple(item[0] for item in triangle)
                if len(set(vertex_key)) < 3:
                    continue
                canonical = tuple(sorted(vertex_key))
                if canonical in seen:
                    continue
                seen.add(canonical)
                faces.append((material, triangle))
                if len(faces) >= max_faces:
                    return faces
    return faces


def format_corner(corner, vertex_map, texcoord_map, normal_map):
    vertex, texcoord, normal = corner
    result = str(vertex_map[vertex])
    if texcoord or normal:
        result += "/" + (str(texcoord_map[texcoord]) if texcoord else "")
    if normal:
        result += "/" + str(normal_map[normal])
    return result


def write_result(source_path, output_path, values, header, object_name, faces):
    used_vertices = sorted({corner[0] for _, face in faces for corner in face})
    used_texcoords = sorted({corner[1] for _, face in faces for corner in face
                             if corner[1]})
    used_normals = sorted({corner[2] for _, face in faces for corner in face
                           if corner[2]})
    vertex_map = {old: new for new, old in enumerate(used_vertices, 1)}
    texcoord_map = {old: new for new, old in enumerate(used_texcoords, 1)}
    normal_map = {old: new for new, old in enumerate(used_normals, 1)}

    selected_texcoords = set(used_texcoords)
    selected_normals = set(used_normals)
    texcoord_lines = {}
    normal_lines = {}
    with source_path.open("r", encoding="utf-8", errors="replace") as source:
        texcoord_index = 0
        normal_index = 0
        for line in source:
            if line.startswith("vt "):
                texcoord_index += 1
                if texcoord_index in selected_texcoords:
                    texcoord_lines[texcoord_index] = line.rstrip()
            elif line.startswith("vn "):
                normal_index += 1
                if normal_index in selected_normals:
                    normal_lines[normal_index] = line.rstrip()

    output_path.parent.mkdir(parents=True, exist_ok=True)
    with output_path.open("w", encoding="utf-8", newline="\n") as output:
        output.write("# CraftOrigin WebGL simplified mesh\n")
        output.write(f"# Source: {source_path.as_posix()}\n")
        for line in header:
            output.write(line + "\n")
        output.write(f"o {object_name}\n")
        for old in used_vertices:
            base = (old - 1) * 3
            output.write(
                f"v {values[base]:.7g} {values[base + 1]:.7g} "
                f"{values[base + 2]:.7g}\n"
            )
        for old in used_texcoords:
            output.write(texcoord_lines[old] + "\n")
        for old in used_normals:
            output.write(normal_lines[old] + "\n")

        active_material = None
        for material, face in faces:
            if material != active_material:
                if material:
                    output.write(f"usemtl {material}\n")
                active_material = material
            output.write(
                "f " + " ".join(
                    format_corner(corner, vertex_map, texcoord_map, normal_map)
                    for corner in face
                ) + "\n"
            )
    return len(used_vertices), len(faces)


def simplify(source_path: Path, output_path: Path, target_faces: int):
    values, minimum, maximum, header, object_name = read_vertices(source_path)
    resolution = max(24, round(math.sqrt(target_faces / 2.0)))
    max_faces = max(target_faces, round(target_faces * 1.15))
    faces = []
    for _ in range(5):
        remap = cluster_vertices(values, minimum, maximum, resolution)
        faces = collect_faces(source_path, remap, max_faces)
        if len(faces) <= target_faces:
            break
        resolution = max(16, round(resolution * 0.78))
    vertex_count, face_count = write_result(
        source_path, output_path, values, header, object_name, faces
    )
    print(f"{source_path}: {len(values) // 3} vertices -> "
          f"{vertex_count} vertices, {face_count} faces ({output_path})")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--target-faces", type=int, default=60000)
    arguments = parser.parse_args()
    simplify(arguments.source, arguments.output, arguments.target_faces)


if __name__ == "__main__":
    main()
