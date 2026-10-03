# Game Platform .NET SDK

**P3 complete at the owner-approved local nonproduction scope; broader P4 and production/public release remain held.** Unity consumes the canonical Git UPM package at `e7c03c5`; current source edits require a new package before Unity can consume them.

This game-neutral C# SDK provides portable account-scope, durable local state, ordered command/outbox, synchronization and bounded progression/receipt machinery, with replaceable HTTP, SQLite and MessagePack adapters. Runtime libraries target `netstandard2.1` with C# 9. Keep core/contracts portable and game rules, Unity scenes and MrSquare types outside the SDK. Some broad feature façades remain unavailable; scoped implemented services do not imply every planned feature is complete.

The [current status](docs/IMPLEMENTATION_STATUS.md) separates SDK source, canonical package and Unity consumption. The backend [codebase reading guide](https://github.com/gindemit/game-platform-backend/blob/main/docs/engineering/CODEBASE-READING-GUIDE.md) follows a campaign completion across all three repositories.

## Current coordinated work

Start at [AGENT_ORCHESTRATOR.md](AGENT_ORCHESTRATOR.md) and the backend [current campaign checkpoint](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/checkpoint.md). The [SDK handoff](docs/implementation/CAMPAIGN_UPM_HANDOFF.md) records package generation and Unity consumption. The current bounded maintenance work follows the [selected packet](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/CODEX-MAINTAINABILITY-2026-10-03.md).

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
