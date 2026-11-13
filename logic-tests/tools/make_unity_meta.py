#!/usr/bin/env python3
"""Writes Unity .meta files for the logic code and its tests.

Unity gives every file an id (a GUID) kept in a .meta file beside it. If those ids changed each time the
files were copied into the Unity project, anything pointing at them would break. This script works each id
out from the file's path, so the same file always gets the same id, on any computer.

Run it from anywhere after adding, moving or deleting a file:

    python3 logic-tests/tools/make_unity_meta.py

It only touches the two folders this code owns and never changes an id that already exists.
"""
import hashlib
import pathlib

ASSETS = pathlib.Path(__file__).resolve().parents[2] / "unity" / "Assets"
OWNED = ["DesktopRider/Scripts/Logic", "DesktopRider/Tests/EditMode"]

TAIL = "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
IMPORTERS = {
    ".cs": "MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n"
           "  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n",
    ".asmdef": "AssemblyDefinitionImporter:\n" + TAIL,
    ".json": "TextScriptImporter:\n" + TAIL,
}


def guid_for(path: pathlib.Path) -> str:
    relative = path.relative_to(ASSETS).as_posix()
    return hashlib.md5(("desktop-rider:" + relative).encode("utf-8")).hexdigest()


def meta_for(path: pathlib.Path) -> str:
    head = f"fileFormatVersion: 2\nguid: {guid_for(path)}\n"
    if path.is_dir():
        return head + "folderAsset: yes\nDefaultImporter:\n" + TAIL
    return head + IMPORTERS[path.suffix]


def main() -> None:
    written = removed = 0
    for owned in OWNED:
        root = ASSETS / owned
        wanted = set()
        for path in [root, *sorted(root.rglob("*"))]:
            if path.suffix == ".meta":
                continue
            if path.is_file() and path.suffix not in IMPORTERS:
                raise SystemExit(f"No importer is known for {path}. Add its kind to IMPORTERS.")
            meta = path.with_name(path.name + ".meta")
            wanted.add(meta)
            if not meta.exists():
                meta.write_text(meta_for(path), encoding="utf-8", newline="\n")
                written += 1
        for meta in root.rglob("*.meta"):
            if meta not in wanted:
                meta.unlink()
                removed += 1
    print(f"{written} .meta file(s) written, {removed} left-over one(s) removed.")


if __name__ == "__main__":
    main()
