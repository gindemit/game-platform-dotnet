# Implementation status

Date: 2026-09-17. Milestone: M0 compatibility/tooling.

## Implemented in M0

- Fifteen `netstandard2.1` project seams and one .NET 9 xUnit project.
- Central build/package configuration, deterministic builds and genuine NuGet lockfile workflow.
- Explicit typed unavailable results/exceptions for all sixteen product features and the HTTP, SQLite, MessagePack and JSON adapters.
- Machine-readable architecture/feature catalogs, validator and validator self-tests.
- Backend-owned `0.1.0-draft` protocol mirror with SHA-256 pins.
- Documentation, scoped agent instructions, CI, PR checklist, security/private-license guidance and local non-publishing package tooling.

## Not implemented or accepted

All product features, identity/provisioning, local durability, synchronization, HTTP, codecs, native SQLite, navigation behavior and presenters remain stubs/planned. A01–A13 production gates remain pending. No Unity import, IL2CPP/AOT/stripping, device, PostgreSQL, Supabase/Deno, Workers, Node/container or cross-language MessagePack runtime test was run. No package was published and the Unity game was not modified.

## M0 validation record

Environment: Windows 10.0.26200, .NET SDK 9.0.203/MSBuild 17.13.20, Python 3.10.4 and Git 2.47.0.windows.2.

- `python scripts/validate.py`: passed; 15 runtime projects, 16 stubbed features, architecture references/module READMEs and 2 pinned contract files.
- `python -m unittest discover -s scripts -p 'test_*.py'`: 7 passed in 0.024 seconds.
- `dotnet restore GamePlatform.sln --use-lock-file`: passed; generated 16 genuine `packages.lock.json` files (15 runtime + test).
- `dotnet restore GamePlatform.sln --locked-mode`: passed with 0 errors and 0 warnings.
- `dotnet build GamePlatform.sln -c Release --no-restore`: passed with 0 errors and 0 warnings. An earlier authoring run failed because the test project inherited C# 9 while its generated xUnit global using required C# 10; the test project now explicitly uses the installed latest language version while runtime projects remain C# 9.
- `dotnet test GamePlatform.sln -c Release --no-build --no-restore`: 19 test cases passed, 0 failed/skipped/warnings, one test project, 989 ms.
- `dotnet list GamePlatform.sln package --include-transitive`: all 15 runtime projects have no NuGet packages; the test project resolved the centrally pinned test packages and lockfile-recorded transitives.
- `python scripts/package-sdk.py`: passed after correcting directory enumeration; produced 15 local `.nupkg` files, 15 managed DLLs and a SHA-256 dependency manifest. It included no native libraries and published nothing.
- Contract SHA-256: policy `3529f3d92ee3e368e91c6d1ca9176022ebe5ae5124b5072cdb76d2fd8474047c`; primitive vectors `d76ccd7aa7e26a4a8b2603061f2709e6391dcc9abfe85bd5739fb58138da30a5`.

Publication/permission status: local Git repository only. No remote was created or pushed by this M0 task; repository publication is owned by the parent integration task. There were no local filesystem or package-feed permission blockers.
