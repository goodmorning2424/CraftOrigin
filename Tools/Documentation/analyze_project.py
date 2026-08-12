from __future__ import annotations

import json
import re
import sys
import argparse
from collections import Counter
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / "Assets"


BUILTIN_COMPONENTS = {
    4: "Transform",
    20: "Camera",
    23: "MeshRenderer",
    25: "Renderer",
    33: "MeshFilter",
    54: "Rigidbody",
    64: "MeshCollider",
    65: "BoxCollider",
    81: "AudioListener",
    82: "AudioSource",
    108: "Light",
    114: "MonoBehaviour",
    135: "SphereCollider",
    136: "CapsuleCollider",
    137: "SkinnedMeshRenderer",
    212: "SpriteRenderer",
    222: "CanvasRenderer",
    223: "Canvas",
    224: "RectTransform",
    225: "CanvasGroup",
}


def build_guid_map() -> dict[str, str]:
    result: dict[str, str] = {}
    for meta in ASSETS.rglob("*.meta"):
        try:
            text = meta.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        match = re.search(r"(?m)^guid:\s*([0-9a-f]+)\s*$", text)
        if match:
            asset_path = meta.with_suffix("")
            result[match.group(1)] = asset_path.relative_to(ROOT).as_posix()
    return result


def scalar(text: str, key: str, default: str = "") -> str:
    match = re.search(rf"(?m)^\s{{2}}{re.escape(key)}:\s*(.*?)\s*$", text)
    return match.group(1) if match else default


def file_id(text: str, key: str) -> int:
    match = re.search(
        rf"(?m)^\s{{2}}{re.escape(key)}:\s*\{{fileID:\s*(-?\d+)", text
    )
    return int(match.group(1)) if match else 0


def parse_unity_documents(path: Path) -> list[dict]:
    raw = path.read_text(encoding="utf-8", errors="replace")
    matches = list(re.finditer(r"(?m)^--- !u!(\d+) &(-?\d+)(?: stripped)?\s*$", raw))
    docs: list[dict] = []
    for index, match in enumerate(matches):
        end = matches[index + 1].start() if index + 1 < len(matches) else len(raw)
        docs.append(
            {
                "class_id": int(match.group(1)),
                "file_id": int(match.group(2)),
                "text": raw[match.end() : end],
            }
        )
    return docs


def parse_scene(path: Path, guid_map: dict[str, str]) -> dict:
    docs = parse_unity_documents(path)
    game_objects: dict[int, dict] = {}
    transforms: dict[int, dict] = {}
    component_docs: dict[int, dict] = {}

    for doc in docs:
        class_id = doc["class_id"]
        body = doc["text"]
        if class_id == 1:
            component_ids = [
                int(value)
                for value in re.findall(
                    r"(?m)^\s{2}- component:\s*\{fileID:\s*(-?\d+)\}", body
                )
            ]
            game_objects[doc["file_id"]] = {
                "id": doc["file_id"],
                "name": scalar(body, "m_Name", "(unnamed)"),
                "active": scalar(body, "m_IsActive", "1") == "1",
                "layer": int(scalar(body, "m_Layer", "0") or 0),
                "tag": scalar(body, "m_TagString", "Untagged"),
                "component_ids": component_ids,
            }
        elif class_id in (4, 224):
            parent_match = re.search(
                r"(?m)^\s{2}m_Father:\s*\{fileID:\s*(-?\d+)\}", body
            )
            go_match = re.search(
                r"(?m)^\s{2}m_GameObject:\s*\{fileID:\s*(-?\d+)\}", body
            )
            transforms[doc["file_id"]] = {
                "id": doc["file_id"],
                "game_object": int(go_match.group(1)) if go_match else 0,
                "parent": int(parent_match.group(1)) if parent_match else 0,
            }
            component_docs[doc["file_id"]] = doc
        else:
            component_docs[doc["file_id"]] = doc

    transform_by_go = {value["game_object"]: value for value in transforms.values()}

    for go in game_objects.values():
        names: list[str] = []
        details: list[dict] = []
        for component_id in go["component_ids"]:
            component = component_docs.get(component_id)
            if not component:
                continue
            class_id = component["class_id"]
            name = BUILTIN_COMPONENTS.get(class_id, f"UnityComponent({class_id})")
            script_path = ""
            if class_id == 114:
                script_match = re.search(
                    r"m_Script:\s*\{fileID:\s*-?\d+,\s*guid:\s*([0-9a-f]+)",
                    component["text"],
                )
                if script_match:
                    script_path = guid_map.get(script_match.group(1), "")
                    name = Path(script_path).stem if script_path else "MissingScript"
            names.append(name)
            details.append(
                {
                    "name": name,
                    "class_id": class_id,
                    "script_path": script_path,
                    "enabled": scalar(component["text"], "m_Enabled", "1") == "1",
                }
            )
        go["components"] = names
        go["component_details"] = details
        transform = transform_by_go.get(go["id"])
        go["transform_id"] = transform["id"] if transform else 0
        go["parent_transform"] = transform["parent"] if transform else 0

    children: dict[int, list[dict]] = {}
    for go in game_objects.values():
        children.setdefault(go["parent_transform"], []).append(go)
    for values in children.values():
        values.sort(key=lambda item: item["name"].lower())

    lines: list[str] = []

    def walk(parent_transform: int, depth: int) -> None:
        for go in children.get(parent_transform, []):
            marker = "" if go["active"] else " [inactive]"
            components = [
                value for value in go["components"] if value not in ("Transform", "RectTransform")
            ]
            suffix = f" — {', '.join(components)}" if components else ""
            lines.append(f"{'  ' * depth}{go['name']}{marker}{suffix}")
            if go["transform_id"]:
                walk(go["transform_id"], depth + 1)

    walk(0, 0)
    component_counter = Counter(
        component
        for go in game_objects.values()
        for component in go.get("components", [])
        if component not in ("Transform", "RectTransform")
    )
    script_paths = sorted(
        {
            detail["script_path"]
            for go in game_objects.values()
            for detail in go.get("component_details", [])
            if detail["script_path"]
        }
    )
    source_prefabs = Counter()
    for doc in docs:
        if doc["class_id"] != 1001:
            continue
        source_match = re.search(
            r"m_SourcePrefab:\s*\{fileID:\s*-?\d+,\s*guid:\s*([0-9a-f]+)",
            doc["text"],
        )
        if source_match:
            source_prefabs[guid_map.get(source_match.group(1), source_match.group(1))] += 1

    go_by_transform = {
        go["transform_id"]: go for go in game_objects.values() if go["transform_id"]
    }

    def object_path(go: dict) -> str:
        names = [go["name"]]
        parent = go["parent_transform"]
        seen: set[int] = set()
        while parent and parent not in seen:
            seen.add(parent)
            parent_go = go_by_transform.get(parent)
            if not parent_go:
                break
            names.append(parent_go["name"])
            parent = parent_go["parent_transform"]
        return "/".join(reversed(names))

    component_settings: list[dict] = []
    for go in game_objects.values():
        for component_id in go["component_ids"]:
            component = component_docs.get(component_id)
            if not component or component["class_id"] != 114:
                continue
            script_match = re.search(
                r"m_Script:\s*\{fileID:\s*-?\d+,\s*guid:\s*([0-9a-f]+)",
                component["text"],
            )
            if not script_match:
                continue
            script_path = guid_map.get(script_match.group(1), "")
            if not script_path.startswith("Assets/Scripts/CraftLive/"):
                continue
            fields: dict[str, str] = {}
            for key, value in re.findall(
                r"(?m)^\s{2}([A-Za-z_][A-Za-z0-9_]*):\s*(.*?)\s*$",
                component["text"],
            ):
                if key.startswith("m_") or key in {"serializedVersion"}:
                    continue
                if not value:
                    fields[key] = "(empty)"
                    continue
                guid_match = re.search(r"guid:\s*([0-9a-f]+)", value)
                if guid_match:
                    value = guid_map.get(guid_match.group(1), value)
                fields[key] = value
            component_settings.append(
                {
                    "object": object_path(go),
                    "component": Path(script_path).stem,
                    "script_path": script_path,
                    "fields": fields,
                }
            )
    return {
        "path": path.relative_to(ROOT).as_posix(),
        "game_object_count": len(game_objects),
        "root_count": len(children.get(0, [])),
        "component_counts": dict(component_counter.most_common()),
        "script_paths": script_paths,
        "source_prefabs": dict(source_prefabs.most_common()),
        "component_settings": component_settings,
        "hierarchy": lines,
    }


def main() -> None:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser()
    parser.add_argument("--scene", default="")
    parser.add_argument("--summary", action="store_true")
    args = parser.parse_args()
    guid_map = build_guid_map()
    scene_paths = sorted((ASSETS / "Scenes").rglob("*.unity"))
    if args.scene:
        scene_paths = [
            path for path in scene_paths if args.scene.lower() in path.name.lower()
        ]
    result = {
        "scenes": [parse_scene(path, guid_map) for path in scene_paths],
        "guid_count": len(guid_map),
    }
    if args.summary:
        for scene in result["scenes"]:
            scene.pop("hierarchy", None)
            scene.pop("component_settings", None)
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
