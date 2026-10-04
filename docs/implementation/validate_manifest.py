#!/usr/bin/env python3
"""Compatibility launcher; no orchestration implementation lives here."""
from pathlib import Path
import runpy
import sys

repo = next(p for p in Path(__file__).resolve().parents if (p / ".git").exists())
entry = repo.parent / "game-platform-workspace/scripts/agent-guide.py"
if not entry.is_file():
    raise SystemExit("Clone sibling gindemit/game-platform-workspace for shared context; local builds do not require it.")
sys.path.insert(0, str(entry.parent))
runpy.run_path(str(entry), run_name="__main__")
