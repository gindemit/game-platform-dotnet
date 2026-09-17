#!/usr/bin/env python3
"""Validate the M0 repository topology, catalogs, references and protocol pins."""
from __future__ import annotations

import hashlib
import json
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

PROJECTS = {
    "GamePlatform.Core", "GamePlatform.Diagnostics.Abstractions", "GamePlatform.Features.Contracts",
    "GamePlatform.Backend.Contracts", "GamePlatform.Storage.Abstractions", "GamePlatform.Features",
    "GamePlatform.Sync", "GamePlatform.Navigation", "GamePlatform.Features.Presentation",
    "GamePlatform.Wire.Contracts", "GamePlatform.Transport.Abstractions", "GamePlatform.Transport.Http",
    "GamePlatform.Storage.Sqlite", "GamePlatform.Serialization.MessagePack", "GamePlatform.Serialization.Json",
}
FEATURES = {
    "accounts", "profiles", "catalog", "inventory", "wallet", "entitlements", "reward-fulfillment",
    "progression", "quests", "achievements", "store", "purchases", "leaderboards", "teams",
    "remote-config", "inbox",
}
FEATURE_DIRS = {"reward-fulfillment": "RewardFulfillment", "remote-config": "RemoteConfig"}


def read_json(path: Path):
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def project_references(project: Path) -> set[str]:
    root = ET.parse(project).getroot()
    return {Path(node.attrib["Include"].replace("\\", "/")).stem for node in root.findall(".//ProjectReference")}


def validate(root: Path) -> list[str]:
    errors: list[str] = []
    required = ["AGENTS.md", "README.md", "SECURITY.md", "LICENSE-NOTICE.md", "GamePlatform.sln",
                "Directory.Build.props", "Directory.Packages.props", "architecture.json", "features.json",
                "contracts/snapshot.json", "docs/IMPLEMENTATION_STATUS.md"]
    for item in required:
        if not (root / item).is_file(): errors.append(f"missing required file: {item}")

    try:
        architecture = read_json(root / "architecture.json")
        declared = set(architecture["projects"])
        if declared != PROJECTS: errors.append(f"architecture projects differ: {sorted(declared ^ PROJECTS)}")
        actual_projects = {p.parent.name for p in (root / "src").glob("*/*.csproj")}
        if actual_projects != PROJECTS: errors.append(f"runtime projects differ: {sorted(actual_projects ^ PROJECTS)}")
        for name in sorted(PROJECTS):
            project = root / "src" / name / f"{name}.csproj"
            tree = ET.parse(project).getroot()
            target = tree.findtext(".//TargetFramework")
            if target != "netstandard2.1": errors.append(f"{name}: target is {target!r}")
            refs = project_references(project)
            allowed = set(architecture["projects"][name])
            if not refs <= allowed: errors.append(f"{name}: forbidden references {sorted(refs - allowed)}")
            if not (project.parent / "README.md").is_file(): errors.append(f"{name}: missing README.md")
    except (OSError, KeyError, json.JSONDecodeError, ET.ParseError) as exc:
        errors.append(f"architecture validation failed: {exc}")

    try:
        catalog = read_json(root / "features.json")
        entries = catalog["features"]
        ids = [entry["id"] for entry in entries]
        if set(ids) != FEATURES or len(ids) != len(FEATURES): errors.append("feature identifiers are missing or duplicated")
        for entry in entries:
            if entry.get("status") != "stubbed": errors.append(f"{entry.get('id')}: M0 status must be stubbed")
            dirname = FEATURE_DIRS.get(entry["id"], entry["name"])
            if not (root / "src" / "GamePlatform.Features" / dirname / "README.md").is_file():
                errors.append(f"{entry['id']}: missing module README")
    except (OSError, KeyError, json.JSONDecodeError) as exc:
        errors.append(f"feature validation failed: {exc}")

    try:
        snapshot = read_json(root / "contracts" / "snapshot.json")
        if snapshot.get("contractVersion") != "0.1.0-draft": errors.append("unexpected contract version")
        for relative, expected in snapshot["files"].items():
            contract = root / "contracts" / relative
            if not contract.is_file(): errors.append(f"missing contract: {relative}")
            elif sha256(contract) != expected: errors.append(f"contract hash mismatch: {relative}")
    except (OSError, KeyError, json.JSONDecodeError) as exc:
        errors.append(f"contract validation failed: {exc}")
    return errors


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    errors = validate(root)
    if errors:
        for error in errors: print(f"ERROR: {error}")
        print(f"Validation failed with {len(errors)} error(s).")
        return 1
    print("Validation passed: 15 projects, 16 stubbed features, architecture references, module READMEs, and 2 pinned contract files.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
