# Game Platform .NET SDK

**P3 complete at the owner-approved local nonproduction scope; broader P4 and production/public release remain held.** This is not an M0-only scaffold. Unity already consumes a verified SDK runtime bundle; the new Git Unity Package Manager delivery is approved but not yet implemented by this documentation update.

This game-neutral C# SDK provides portable account-scope, durable local state, ordered command/outbox, synchronization and bounded progression/receipt machinery, with replaceable HTTP, SQLite and MessagePack adapters. Runtime libraries target `netstandard2.1` with C# 9. Keep core/contracts portable and game rules, Unity scenes and MrSquare types outside the SDK. Some broad feature façades remain unavailable; scoped implemented services do not imply every planned feature is complete.

Unity's current Assets import records runtime source `4b1ba063877d44ab12ad77c8d8470cf64a55b1e6` and bundle manifest SHA-256 `2f9061f3cceaadf7e72b07b34d3723c1130bc5c614d4fa4713a2028ce9ac59ba`. Later repository documentation/test-tool commits are not automatically a different imported runtime. [Current status](docs/IMPLEMENTATION_STATUS.md) links the exact acceptance and limits.

## Current coordinated work

Start at [AGENT_ORCHESTRATOR.md](AGENT_ORCHESTRATOR.md) and the backend [local campaign packet](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/README.md), goal `CAMPAIGN_LOCAL`. SDK responsibilities are deterministic Git UPM packaging first, then only necessary generic progression/evidence contract changes. See [SDK handoff](docs/implementation/CAMPAIGN_UPM_HANDOFF.md). No Unity-only SDK source fork, copied duplicate runtime, game-specific route logic or broad feature rewrite.

The existing [implementation plan](docs/implementation/README.md), [runbook](docs/implementation/RUNBOOK.md) and [execution manifest](docs/implementation/execution-manifest.json) retain original task/evidence authority. Do not restart their initial codec/import milestones or mark full original tasks complete from a narrower campaign result.

## Prerequisites and commands

.NET SDK 9.0.203 is pinned by global.json; Python 3.10+ is required. Inspect repository pins before updating tooling.

```sh
python3 scripts/validate.py
python3 -m unittest discover -s scripts -p 'test_*.py'
dotnet restore GamePlatform.sln --locked-mode
dotnet format GamePlatform.sln whitespace --verify-no-changes --no-restore --exclude src/GamePlatform.Storage.Sqlite/Vendor/SQLite.cs
dotnet build GamePlatform.sln -c Release --no-restore
dotnet test GamePlatform.sln -c Release --no-build --no-restore
python3 scripts/package-sdk.py
python3 docs/implementation/validate_manifest.py --ready
python3 -m unittest discover -s docs/implementation -p 'test_*.py'
```

Read [AGENTS.md](AGENTS.md), [documentation index](docs/README.md) and scoped instructions only for the changed boundary. Preserve exact source/artifact identity, account isolation, new SQLite/outbox and accepted wire contracts. Repository pushes for the requested Git package do not authorize registry publishing, deployment, distribution or production data changes.
