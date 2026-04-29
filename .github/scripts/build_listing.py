#!/usr/bin/env python3
"""Build a VPM listing index.json from GitHub releases.

For each release whose tag matches `<prefix>/v<version>` and whose prefix
is registered in packages.json, fetch the bundled `package.json` asset
from the release and merge it into the listing.

Environment:
  GITHUB_REPOSITORY   owner/repo (provided by GitHub Actions)
  GITHUB_TOKEN        token for API access (set in workflow)

Usage:
  build_listing.py <packages_config> <output_path> [listing_url]
"""
from __future__ import annotations

import hashlib
import json
import os
import sys
import urllib.error
import urllib.request
from pathlib import Path
from typing import Any

API = "https://api.github.com"


def http_get(url: str, token: str | None, accept: str = "application/vnd.github+json") -> bytes:
    req = urllib.request.Request(url, headers={
        "Accept": accept,
        "User-Agent": "vpm-listing-builder",
    })
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    with urllib.request.urlopen(req) as resp:
        return resp.read()


def get_json(url: str, token: str | None) -> Any:
    return json.loads(http_get(url, token))


def fetch_releases(repo: str, token: str | None) -> list[dict[str, Any]]:
    releases: list[dict[str, Any]] = []
    page = 1
    while True:
        data = get_json(f"{API}/repos/{repo}/releases?per_page=100&page={page}", token)
        if not data:
            break
        releases.extend(data)
        if len(data) < 100:
            break
        page += 1
    return releases


def parse_tag(tag: str, prefixes: dict[str, str]) -> tuple[str, str] | None:
    if "/" not in tag:
        return None
    prefix, _, version = tag.partition("/")
    if prefix not in prefixes:
        return None
    if version.startswith("v"):
        version = version[1:]
    if not version:
        return None
    return prefixes[prefix], version


def find_asset(release: dict[str, Any], name: str) -> dict[str, Any] | None:
    for asset in release.get("assets", []):
        if asset.get("name") == name:
            return asset
    return None


def build(config_path: Path, output: Path, listing_url: str) -> int:
    config = json.loads(config_path.read_text(encoding="utf-8"))
    prefixes = {pkg["prefix"]: pkg["name"] for pkg in config["packages"]}

    repo = os.environ.get("GITHUB_REPOSITORY")
    if not repo:
        print("error: GITHUB_REPOSITORY not set", file=sys.stderr)
        return 1
    token = os.environ.get("GITHUB_TOKEN")

    listing = {
        "name": "colloid VPM Listing",
        "id": "jp.colloid.vpm",
        "url": listing_url,
        "author": {"name": "colloid"},
        "packages": {name: {"versions": {}} for name in prefixes.values()},
    }

    seen = 0
    for release in fetch_releases(repo, token):
        if release.get("draft") or release.get("prerelease"):
            continue
        tag = release.get("tag_name", "")
        parsed = parse_tag(tag, prefixes)
        if not parsed:
            continue
        package_name, version = parsed

        manifest_asset = find_asset(release, "package.json")
        zip_name = f"{package_name}-{version}.zip"
        zip_asset = find_asset(release, zip_name)
        if not manifest_asset or not zip_asset:
            print(f"skip {tag}: missing assets", file=sys.stderr)
            continue

        manifest_url = manifest_asset["browser_download_url"]
        manifest_bytes = http_get(manifest_url, token, accept="application/octet-stream")
        manifest = json.loads(manifest_bytes)
        manifest["url"] = zip_asset["browser_download_url"]

        zip_bytes = http_get(zip_asset["browser_download_url"], token, accept="application/octet-stream")
        manifest["zipSHA256"] = hashlib.sha256(zip_bytes).hexdigest()

        listing["packages"][package_name]["versions"][version] = manifest
        seen += 1
        print(f"indexed {tag}")

    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(listing, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"wrote {output} ({seen} versions)")
    return 0


def main(argv: list[str]) -> int:
    if len(argv) < 3:
        print(__doc__, file=sys.stderr)
        return 64
    listing_url = argv[3] if len(argv) >= 4 else ""
    return build(Path(argv[1]), Path(argv[2]), listing_url)


if __name__ == "__main__":
    sys.exit(main(sys.argv))
