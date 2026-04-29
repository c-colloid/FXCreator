#!/usr/bin/env python3
"""Build a .unitypackage from a Unity package directory.

A .unitypackage is a gzipped tar archive where each asset lives in a
directory named after its GUID. Each directory contains:
  - asset       (binary content of the asset; omitted for folders)
  - asset.meta  (the .meta file content)
  - pathname    (the asset path Unity will use on import)

Usage:
  build_unitypackage.py <package_dir> <output_path> <unity_path_prefix>

Example:
  build_unitypackage.py Packages/jp.colloid.clipgen \
      dist/jp.colloid.clipgen-1.0.0.unitypackage \
      Packages/jp.colloid.clipgen
"""
from __future__ import annotations

import io
import os
import re
import sys
import tarfile
import time
from pathlib import Path

GUID_RE = re.compile(r"^guid:\s*([0-9a-f]{32})\s*$", re.MULTILINE)


def read_guid(meta_path: Path) -> str | None:
    text = meta_path.read_text(encoding="utf-8", errors="replace")
    match = GUID_RE.search(text)
    return match.group(1) if match else None


def add_entry(tar: tarfile.TarFile, guid: str, name: str, data: bytes, mtime: float) -> None:
    info = tarfile.TarInfo(f"{guid}/{name}")
    info.size = len(data)
    info.mtime = int(mtime)
    info.mode = 0o644
    info.type = tarfile.REGTYPE
    tar.addfile(info, io.BytesIO(data))


def build(package_dir: Path, output: Path, unity_path_prefix: str) -> int:
    if not package_dir.is_dir():
        print(f"error: {package_dir} is not a directory", file=sys.stderr)
        return 1

    output.parent.mkdir(parents=True, exist_ok=True)
    seen_guids: set[str] = set()
    count = 0
    mtime = time.time()

    with tarfile.open(output, "w:gz") as tar:
        for meta_path in sorted(package_dir.rglob("*.meta")):
            asset_path = meta_path.with_suffix("")
            if asset_path.name == "":
                continue
            if not asset_path.exists():
                print(f"warn: orphan meta {meta_path}", file=sys.stderr)
                continue

            guid = read_guid(meta_path)
            if not guid:
                print(f"warn: no guid in {meta_path}", file=sys.stderr)
                continue
            if guid in seen_guids:
                print(f"error: duplicate guid {guid} ({asset_path})", file=sys.stderr)
                return 2
            seen_guids.add(guid)

            relative = asset_path.relative_to(package_dir).as_posix()
            unity_path = f"{unity_path_prefix}/{relative}".rstrip("/")

            add_entry(tar, guid, "pathname", unity_path.encode("utf-8"), mtime)
            add_entry(tar, guid, "asset.meta", meta_path.read_bytes(), mtime)
            if asset_path.is_file():
                add_entry(tar, guid, "asset", asset_path.read_bytes(), mtime)
            count += 1

    print(f"wrote {output} ({count} assets)")
    return 0


def main(argv: list[str]) -> int:
    if len(argv) != 4:
        print(__doc__, file=sys.stderr)
        return 64
    return build(Path(argv[1]), Path(argv[2]), argv[3])


if __name__ == "__main__":
    sys.exit(main(sys.argv))
