"""Carve the three moving-wall passages from Assets/Pad1/Object/Box.obj.

The cut boxes are expressed in the Box mesh's local coordinates.  Their values
come from Pad1_MaterialGallery.unity and the imported Wall/Gakubiti bounds.
Only the inner depth of the original left and right side pillars is opened.
Their original front/rear timber, texture, bevels, and corner hardware remain
connected to the upper timber.
"""

from __future__ import annotations

import argparse
import shutil
from pathlib import Path


WALL_CENTERS_Y = (0.589364594, 0.066364594, -0.426635406)
WALL_HALF_HEIGHT = 0.2476908
HEIGHT_CLEARANCE = 0.015

# The side posts run from approximately |X|=0.66 to |X|=0.80.
LEFT_X = (-0.85, -0.625)
RIGHT_X = (0.625, 0.85)

# Keep the original front and rear parts of both side pillars.  This central
# passage contains the frames while retaining enough of the source timber to
# show its real thickness from the side.
OPENING_Z = (-0.32, 0.32)


def parse_face_vertex_indices(line: str) -> list[int]:
    result: list[int] = []
    for token in line.split()[1:]:
        result.append(int(token.split("/", 1)[0]))
    return result


def overlaps(a_min: float, a_max: float, b_min: float, b_max: float) -> bool:
    return a_max >= b_min and a_min <= b_max


def face_touches_opening(vertices: list[tuple[float, float, float]], indices: list[int]) -> bool:
    points = [vertices[index - 1 if index > 0 else len(vertices) + index] for index in indices]
    xs = [point[0] for point in points]
    ys = [point[1] for point in points]
    zs = [point[2] for point in points]

    touches_side = overlaps(min(xs), max(xs), *LEFT_X) or overlaps(min(xs), max(xs), *RIGHT_X)
    if not touches_side or not overlaps(min(zs), max(zs), *OPENING_Z):
        return False

    half_height = WALL_HALF_HEIGHT + HEIGHT_CLEARANCE
    return any(
        overlaps(min(ys), max(ys), center_y - half_height, center_y + half_height)
        for center_y in WALL_CENTERS_Y
    )


def carve(source: Path, backup: Path) -> tuple[int, int]:
    if not source.is_file():
        raise FileNotFoundError(source)

    backup.parent.mkdir(parents=True, exist_ok=True)
    if not backup.exists():
        shutil.copy2(source, backup)

    vertices: list[tuple[float, float, float]] = []
    with backup.open("r", encoding="utf-8", errors="strict") as input_file:
        for line in input_file:
            if line.startswith("v "):
                fields = line.split()
                vertices.append((float(fields[1]), float(fields[2]), float(fields[3])))

    temporary = source.with_suffix(".obj.codex-tmp")
    kept_faces = 0
    removed_faces = 0
    with backup.open("r", encoding="utf-8", errors="strict") as input_file, temporary.open(
        "w", encoding="utf-8", newline="\n"
    ) as output_file:
        for line in input_file:
            if line.startswith("f ") and face_touches_opening(vertices, parse_face_vertex_indices(line)):
                removed_faces += 1
                continue
            if line.startswith("f "):
                kept_faces += 1
            output_file.write(line)

    temporary.replace(source)
    return kept_faces, removed_faces


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, default=Path("Assets/Pad1/Object/Box.obj"))
    parser.add_argument(
        "--backup",
        type=Path,
        default=Path("LocalBackups/Models/Box-before-wall-openings.obj"),
    )
    args = parser.parse_args()

    kept, removed = carve(args.source, args.backup)
    print(f"Carved {args.source}: removed {removed} faces, kept {kept} original faces")
    print(f"Original backup: {args.backup}")


if __name__ == "__main__":
    main()
