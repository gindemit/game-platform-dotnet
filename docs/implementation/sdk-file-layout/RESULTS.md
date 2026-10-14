# SDK source file organization — 2026-10-14

## Result

The owner selected this maintenance refactor to make SDK types easier for agents
to locate and edit. Two disjoint workers (Sol and Luna) reviewed 61 handwritten
source groups containing 327 declarations and reorganized 59 of those files.
The result adds 298 named C# files, removes 35 superseded bundled files and retains
26 principal files (24 reduced, two unchanged because their helpers stay local).
There are 298 moved declarations; the remaining selected declarations stay in
their principal files. Namespaces, public APIs and behavior are unchanged.

For example, `StorageTransactions.cs` now has `StorageScope.cs`,
`ILocalStorageTransaction.cs`, `ISerializedStorageExecutor.cs`, `StorageFailure.cs`
and `StorageException.cs`. Feature services now have separately named public ports
and exceptions; substantive model bundles have individually named models.

[type-moves.json](type-moves.json) maps every moved type from its original path to
its current file, with a declaration hash. Project and namespace are included in
the identity so domain and wire models with the same name remain distinct.
Historical work orders and receipts retain their original source paths and scope;
use the map to navigate removed filenames. This refactor does not reopen them.

The default for future feature work is one independently useful handwritten
public top-level type per `TypeName.cs`. Root [AGENTS](../../../AGENTS.md) surfaces
the detailed [source rules](../../../src/AGENTS.md#file-organization-for-new-features-and-refactors),
which explain compact companions, internal helpers and generated/vendor exceptions.

## Preservation and deliberate exceptions

Configuration projects/files, generators, generated `.g.cs` sources, vendor code,
tests, samples, project/build settings and generated UPM delivery are unchanged.
The existing unrelated edit in `Accounts/AccountsLifecycle.cs` was excluded and
its SHA256 remains `8a18dcf94dad11aa8b54ea9c1551cff50764b503a018b86f8f7fe08ec1c23f4b`.
Validation copies use its committed baseline bytes so the unrelated edit is not
part of the tested or delivered task source.

Compact existing groups remain in `IAppLog.cs`, `FeatureContracts.cs`, compatibility
`StateRecord.cs`, `WireOptional.cs`, SQLite `SqliteMigration.cs` and
`SqliteAccountsDirectoryMigration.cs`. Legacy `FeatureStubs.cs` remains intact,
including its remote-config placeholder and validator fixtures. SQLite internal
row/checkpoint types remain beside their sole implementation in
`SqliteDurableFeatureStateStore.cs`, `SqliteAtomicCommandStore.cs` and
`SqlitePrivateSyncStore.cs`. Shared internal feature validation helpers have named
files. These exceptions do not establish a pattern for new large contract bundles.

## Validation

SDK base: `186af3c57db3f7bed1566a18019e29f6c5124947`, default branch `main`.
Checks ran in coordinator-owned disposable source copies outside the repositories.

- Roslyn C# 9 syntax comparison: 618 top-level types before and after, zero changed,
  missing or added declarations. Comparison includes documentation, attributes and
  declaration bodies. Each destination retains its original imports and namespace
  framing. Selected inputs contain no preprocessor directives requiring adaptation.
- Scope/byte audit: all changed baseline source paths belong to the two approved
  leases; excluded source, generated/vendor/configuration/delivery bytes and the
  unrelated Accounts edit are unchanged.
- `dotnet test GamePlatform.sln -c Release --logger trx`: passed, 995 passed,
  9 skipped, 0 failed (883 SDK, 48 configuration and 64 configuration-client tests).
- `dotnet format GamePlatform.sln whitespace --verify-no-changes --no-restore --exclude src/GamePlatform.Storage.Sqlite/Vendor/SQLite.cs`:
  passed with no formatting violations; the formatter emitted a workspace-loading
  warning. Normal and deterministic package builds completed with zero warnings
  and errors.
- `python3 scripts/validate.py`: passed (17 projects and 16 evidence-aware states).
- `python3 -m unittest discover -s scripts -p test_*.py -v`: all 75 passed on the
  refactored source; all 75 also passed on the baseline.
- MessagePack `generate-schema.py --check` and `generate-dto-mapper.py --check`:
  passed on both baseline and refactored source.
- `python3 scripts/package-sdk.py --allow-dirty` followed by
  `python3 scripts/package-sdk.py --verify-development`: passed with pinned native
  inputs, closure, notices and debug identity. This disposable local artifact is
  explicitly development-only; it is not an importable release or consumer pin.
- Workspace `python3 scripts/agent-guide.py check --all`: passed.

Historical failures reproduced with the same inputs on baseline and refactored
source: core `generate_dtos.py --check` fails on boolean `additionalProperties`
(`TypeError`); `generate_g3_dtos.py --check` rejects the inactive historical G3
checkpoint; `scripts/validate-g3-schema.py` likewise reports an unexpected G3
checkpoint version. Generators and pins were not changed by this layout order.

## Review and limits

The work order and source leases received a fresh nonauthor review before worker
activation. Final independent review is recorded in [REVIEW.md](REVIEW.md).
No source test assertions or runtime behavior were changed. No Unity imports,
IL2CPP builds, browser/device tests, retained databases or deployments were run.
The nine existing joined recovery skips and all prior product/physical gates
remain as recorded. Source-layout verification does not grant runtime acceptance.

The full coordination order is
[WORK-ORDER.md](https://github.com/gindemit/game-platform-workspace/blob/main/docs/work/sdk-file-layout/WORK-ORDER.md).
