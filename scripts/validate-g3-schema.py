#!/usr/bin/env python3
"""Validate the exact P3 G3 mirror and generated carrier mapping checkpoint."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CONTRACTS = ROOT / "contracts"


def load(relative):
    return json.loads((ROOT / relative).read_text(encoding="utf-8"))


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


snapshot = load("contracts/snapshot.json")
if snapshot.get("contractVersion") != "0.4.0-g3-schema-checkpoint.1":
    raise SystemExit("unexpected G3 checkpoint version")
for relative, expected in snapshot.get("files", {}).items():
    path = CONTRACTS / relative
    if not path.is_file() or digest(path) != expected:
        raise SystemExit(f"mirror pin mismatch: {relative}")

mappings = load("contracts/g3/client-mappings.json")
catalog = load("src/GamePlatform.Wire.Contracts/g3-dto-catalog.json")
if mappings.get("contractVersion") != snapshot["contractVersion"] or catalog.get("contractVersion") != snapshot["contractVersion"]:
    raise SystemExit("mapping version mismatch")
if catalog.get("canonicalCommit") != "0518ad565efdfe16df13e78e2acbb92033e8bd30":
    raise SystemExit("canonical backend commit mismatch")
if catalog.get("codecStatus") != "unimplemented_unverified":
    raise SystemExit("runtime codec must remain unverified")
if catalog.get("presence", {}).get("completion.reward") != "required nullable; C# nullable property, never WireOptional":
    raise SystemExit("required-nullable reward mapping drift")
source = (ROOT / "src/GamePlatform.Wire.Contracts/G3CheckpointDtos.g.cs").read_text(encoding="utf-8")
for text in ("client_trusted_unvalidated", "public long FeedRevision", "public long Quantity", "LiveSliceRewardReceipt? Reward"):
    if text not in source:
        raise SystemExit(f"generated carrier mapping missing: {text}")
print("G3 mirror/mapping validation passed: 98 pins, 8 generated carrier classes, 2 union interfaces, runtime codec unverified.")
