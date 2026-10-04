# Game Platform .NET SDK

Game Platform is a game-neutral C# client SDK for account-scoped state, durable offline commands and ordered synchronization with a backend. It is designed for Unity and other `netstandard2.1` hosts while keeping game rules, scenes, grids, levels and presentation outside the portable runtime.

The SDK provides:

- explicit account provisioning, bootstrap and recovery boundaries;
- transactional SQLite projections, sequence allocation and immutable outbox storage;
- ordered push/pull synchronization with retry, receipt and reset handling;
- bounded MessagePack and HTTP adapters with replaceable host transport;
- portable progression, wallet, inventory, entitlement and reward-confirmation building blocks;
- deterministic Git UPM packaging with reviewed managed/native dependencies.

Successful pushes to `main` generate and verify the package under
`upm/com.gindemit.game-platform`, create a package commit descended from the
validated source commit, and tag it as `0.1.0-dev.<workflow-run-number>`.
The generated commit is reachable through the tag without adding build-only
commits to `main`. Unity can install a generated version with:

```text
https://github.com/gindemit/game-platform-dotnet.git?path=/upm/com.gindemit.game-platform#0.1.0-dev.<workflow-run-number>
```

Runtime libraries target `netstandard2.1` with C# 9 and use constructor injection rather than a global service locator. Domain, wire and SQL types remain separate, and unsupported feature breadth fails closed.

## Project status

**P3 is complete at the approved local nonproduction scope; broader P4, production deployment and general package publication/distribution remain held.** This repository is public for source visibility and review, but it does not currently publish a stable NuGet or registry package.

The latest generated Git UPM is `5d3819d`, from SDK source `6915723`. Unity still consumes the independently qualified package `cae1a21`; no new-pin Editor qualification is recorded. The [current status](docs/IMPLEMENTATION_STATUS.md) and backend [campaign checkpoint](https://github.com/gindemit/game-platform-workspace/blob/main/docs/work/campaign-2026-09-28/checkpoint.md) separate SDK source, generated package and actual consumer evidence.

Some feature services are implemented only at a bounded portable scope, while Quests, Achievements, Store, Purchases, Leaderboards, Teams, RemoteConfig and Inbox remain explicit stubs. See the [feature catalog](docs/FEATURES.md), [architecture](docs/ARCHITECTURE.md) and backend [codebase reading guide](https://github.com/gindemit/game-platform-workspace/blob/main/docs/engineering/CODEBASE-READING-GUIDE.md).

## Repository layout

- `src/` — portable runtime assemblies and adapters.
- `tests/` — unit, integration and peer-harness coverage.
- `contracts/` — hash-pinned mirror of reviewed backend protocol artifacts.
- `integration/` — host and Unity integration policy/probes.
- `upm/com.gindemit.game-platform/` — generated Git UPM payload; do not edit it by hand.
- `docs/` — architecture, current status, decisions and retained implementation evidence.

## Current coordinated work

Start at [AGENT_ORCHESTRATOR.md](AGENT_ORCHESTRATOR.md) and the workspace [current campaign checkpoint](https://github.com/gindemit/game-platform-workspace/blob/main/docs/work/campaign-2026-09-28/checkpoint.md). The [SDK handoff](docs/implementation/CAMPAIGN_UPM_HANDOFF.md) records package generation and Unity consumption. The current bounded hosted-dev continuation follows the [selected packet](https://github.com/gindemit/game-platform-workspace/blob/main/docs/work/campaign-2026-09-28/CODEX-OVERNIGHT-HOSTED-DEV-2026-10-04.md); the [retained-dev preflight](integration/HostedDevSmoke/README.md) has no hosted-write command.

The [implementation reference](docs/implementation/README.md) and [SDK-local manifest](docs/implementation/execution-manifest.json) retain local evidence. The [full original task/evidence ledger](https://github.com/gindemit/game-platform-workspace/blob/main/plans/sdk-integration/execution-manifest.json) lives in the workspace. Do not restart its initial codec/import milestones or mark full original tasks complete from a narrower campaign result.

## Development

.NET SDK 9.0.203 is pinned by global.json; Python 3.10+ is required. Inspect repository pins before updating tooling.

```sh
python3 scripts/validate.py
python3 -m unittest discover -s scripts -p 'test_*.py'
dotnet restore GamePlatform.sln --locked-mode
dotnet format GamePlatform.sln whitespace --verify-no-changes --no-restore --exclude src/GamePlatform.Storage.Sqlite/Vendor/SQLite.cs
dotnet build GamePlatform.sln -c Release --no-restore
dotnet test GamePlatform.sln -c Release --no-build --no-restore
python3 scripts/package-sdk.py
```

Read [AGENTS.md](AGENTS.md), [documentation index](docs/README.md) and scoped instructions only for the changed boundary. Preserve exact source/artifact identity, account isolation, new SQLite/outbox and accepted wire contracts. Repository pushes for the requested Git package do not authorize registry publishing, deployment, distribution or production data changes.

## License

Copyright © 2026 gindemit. The source is publicly visible, but no open-source license or permission to copy, distribute, sublicense or publish is granted unless stated in a separate written agreement. See [LICENSE-NOTICE.md](LICENSE-NOTICE.md) and the bundled third-party notices.
