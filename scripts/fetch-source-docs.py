#!/usr/bin/env python3
import argparse
import subprocess
from pathlib import Path


parser = argparse.ArgumentParser(description="Print pinned MrSquare source documents without changing the consumer.")
parser.add_argument("--source", required=True, type=Path)
parser.add_argument("--commit", default="5c5233edefc8433d61b6c757fd2022bac98f76ba")
parser.add_argument("--path", required=True)
args = parser.parse_args()
result = subprocess.run(["git", "show", f"{args.commit}:{args.path}"], cwd=args.source, check=False)
raise SystemExit(result.returncode)
