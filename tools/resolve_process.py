"""Resolve the documented process inheritance; does not change GitHub settings."""
import argparse
import json
from pathlib import Path


def resolve(path, seen=None):
    path = Path(path).resolve()
    seen = set() if seen is None else seen
    if path in seen:
        raise ValueError("Process inheritance cycle")
    seen.add(path)
    child = json.loads(path.read_text(encoding="utf-8"))
    parent_name = child.pop("extends", None)
    if parent_name:
        parent_path = (path.parent / parent_name).resolve()
        if parent_path.parent != path.parent:
            raise ValueError("The parent process must be in the same directory")
        parent = resolve(parent_path, seen)
        result = {**parent, **child, "inheritedFrom": parent["name"]}
        result["fields"] = parent.get("fields", []) + child.get("fields", [])
    else:
        result = child
    names = [field["name"] for field in result.get("fields", [])]
    if len(names) != len(set(names)):
        raise ValueError("Duplicate field names")
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    result = resolve(args.source)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"{result['name']} ← {result.get('inheritedFrom', 'base')}: {len(result.get('fields', []))} custom field(s)")
