# P3 platform migration v7 — durable invalidation state — 2026-09-20

`SqlitePlatformMigrationRegistry` is the sole platform migration authority. Its
immutable v1--v6 entries are unchanged; it now appends v7,
`platform-confirmed-projection-invalidation-v1`.

v7 replaces only `gp_confirmed_projection` inside the existing per-migration
effects-and-marker transaction. The rebuilt table retains its primary key,
nonnegative signed-64 revision constraint, existing row bytes and the three
prior states. Its state check adds the frozen `invalidation` value as a fourth,
distinct durable state. It does not reinterpret it as `visible`, `removed` or
`tombstone`.

The migration is intentionally storage-only. It does not add a wire/feature
enum, a mapper or a projection policy; the live composition owner must consume
the now-valid durable state separately.

## Windows x64 evidence

All commands ran from the repository root using .NET SDK 9.0.203 and the pinned
native SQLite library.

```text
rtk proxy cmd /c "dotnet restore GamePlatform.sln --locked-mode"
exit 0
18 projects restored

rtk proxy cmd /c "dotnet test tests\\GamePlatform.Tests\\GamePlatform.Tests.csproj -c Release --filter FullyQualifiedName~Cl010MigrationRegistryTests --no-restore"
exit 0
9 passed, 0 failed

rtk proxy cmd /c "dotnet test tests\\GamePlatform.Tests\\GamePlatform.Tests.csproj -c Release --filter FullyQualifiedName~Cl007P3ExtensionMigrationTests --no-restore"
exit 0
9 passed, 0 failed

rtk proxy cmd /c "dotnet build GamePlatform.sln -c Release --no-restore"
exit 0
18 projects, 0 warnings, 0 errors

rtk proxy cmd /c "dotnet test GamePlatform.sln -c Release --no-build --no-restore"
exit 0
469 passed, 0 failed/skipped
```

The v7 registry cases prove fresh installation, v6-to-v7 upgrade with byte and
state preservation, durable insertion/readback of all four distinct states,
reopen idempotence, applied-marker checksum drift rejection, and injected v7
effects-before-marker rollback back to the v6 constraint. The concurrent-open
stress case remains deterministic: 125 fresh databases × 8 barrier-released
openers, asserting seven platform markers and one extension marker per database
without sleeps. It passed once in the focused run and once in the integrated
suite (2,000 synchronized opens total).

This is Windows x64 SQLite migration evidence only. Physical-fault,
non-Windows, Unity/IL2CPP/device, live-provider and G3 acceptance remain
unrun/unpassed.
