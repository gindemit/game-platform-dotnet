# Game Platform .NET SDK

Status: **M0 scaffold; no production capability is implemented**.

This private, game-neutral C# SDK reserves portable seams for identity, state, features, synchronization, navigation, transport and serialization. Runtime libraries target `netstandard2.1` with C# 9. The existing `MrSquare.Platform` contract set remains authoritative until a later coordinated extraction; this repository is not installed in or referenced by the Unity game.

The fifteen libraries follow inward dependencies: core and contracts are BCL-only, portable policy depends on inward ports, and HTTP/SQLite/codecs are replaceable adapters. Every feature and technology adapter currently fails closed with a typed `NotImplemented` result or `PlatformCapabilityUnavailableException`.

## Prerequisites and commands

- .NET SDK 9.0.203 (pinned by `global.json`)
- Python 3.10+

```powershell
python scripts/validate.py
python -m unittest discover -s scripts -p "test_*.py"
dotnet restore GamePlatform.sln --locked-mode
dotnet build GamePlatform.sln -c Release --no-restore
dotnet test GamePlatform.sln -c Release --no-build --no-restore
python scripts/package-sdk.py
```

Read [AGENTS.md](AGENTS.md), [the documentation index](docs/README.md), [implementation status](docs/IMPLEMENTATION_STATUS.md), and the applicable scoped instructions before editing. M0 establishes compatibility/tooling only. M1 must coordinate the backend-owned `0.1.0-draft` protocol and prove bidirectional codec vectors.
