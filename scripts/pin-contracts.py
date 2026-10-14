#!/usr/bin/env python3
import argparse
import hashlib
import json
from pathlib import Path


def digest(path: Path) -> str: return hashlib.sha256(path.read_bytes()).hexdigest()


parser = argparse.ArgumentParser(description="Verify or explicitly rewrite protocol SHA-256 pins.")
parser.add_argument("--write", action="store_true", help="rewrite snapshot pins after reviewed canonical sync")
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
snapshot_path = root / "contracts" / "snapshot.json"
snapshot = json.loads(snapshot_path.read_text(encoding="utf-8"))
actual = {name: digest(root / "contracts" / name) for name in snapshot["files"]}
if args.write:
    snapshot["files"] = actual
    snapshot_path.write_text(json.dumps(snapshot, indent=2) + "\n", encoding="utf-8", newline="\n")
    print("Updated reviewed contract pins.")
else:
    mismatches = [name for name, value in actual.items() if snapshot["files"][name] != value]
    if mismatches: raise SystemExit(f"Contract drift: {', '.join(mismatches)}")
    print("Contract pins match.")
