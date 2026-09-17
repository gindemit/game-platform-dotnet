#!/usr/bin/env python3
import argparse
import shutil
from pathlib import Path


parser = argparse.ArgumentParser(description="Explicitly copy the reviewed backend canonical protocol snapshot.")
parser.add_argument("--source", required=True, type=Path, help="game-platform-backend repository root")
args = parser.parse_args()
target = Path(__file__).resolve().parents[1] / "contracts"
source = args.source.resolve() / "contracts"
for relative in ("platform-policy.json", "v1/primitive-vectors.json", "snapshot.json"):
    origin = source / relative
    if not origin.is_file(): raise SystemExit(f"Missing canonical file: {origin}")
    destination = target / relative
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(origin, destination)
print("Copied canonical protocol bytes; run validate.py and review the diff.")
