#!/usr/bin/env python3
from __future__ import annotations
import hashlib
import json
import shutil
import subprocess
from pathlib import Path


root = Path(__file__).resolve().parents[1]
artifacts = root / "artifacts"
nuget = artifacts / "nuget"
dlls = artifacts / "managed"
if artifacts.exists(): shutil.rmtree(artifacts)
nuget.mkdir(parents=True)
dlls.mkdir(parents=True)
subprocess.run(["dotnet", "pack", "GamePlatform.sln", "-c", "Release", "--no-build", "--no-restore", "-o", str(nuget)], cwd=root, check=True)
for project in sorted(path for path in (root / "src").iterdir() if path.is_dir()):
    dll = project / "bin" / "Release" / "netstandard2.1" / f"{project.name}.dll"
    if not dll.is_file(): raise SystemExit(f"Missing built assembly: {dll}")
    shutil.copy2(dll, dlls / dll.name)
files = []
for path in sorted(p for p in artifacts.rglob("*") if p.is_file()):
    files.append({"path": path.relative_to(artifacts).as_posix(), "sha256": hashlib.sha256(path.read_bytes()).hexdigest(), "bytes": path.stat().st_size})
manifest = {"version": "0.1.0-draft", "managedRuntime": "netstandard2.1", "nativeLibrariesIncluded": False, "published": False, "files": files}
(artifacts / "dependency-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8", newline="\n")
print(f"Packaged {len(list(nuget.glob('*.nupkg')))} NuGet files and {len(list(dlls.glob('*.dll')))} managed DLLs; no native libraries; nothing published.")
