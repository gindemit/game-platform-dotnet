# P3 SQLite extension-migration increment — 2026-09-20

Source branch: `impl/cl007-extension-migrations-20260920`, based on integrated
SDK `8adf434` before this increment.

The fixed `SqlitePlatformMigrationRegistry` now registers the previously
reviewed `SqliteAccountsDirectoryMigration` as platform v4 and adds platform v5
for `gp_extension_migrations`. Platform v1--v3 SQL text, migration IDs and
checksums were not modified.

`SqliteExtensionDescriptor` and `SqliteExtensionMigration` are immutable
consumer-supplied values. A descriptor has a lower-case namespaced identity, a
minimum platform version and contiguous versions from one. The application
requires descriptors in ordinal namespace order. The marker checksum binds the
namespace, minimum platform version, version, migration ID and exact statement
bytes. There is no extension discovery or mutable registry.

`SqliteDatabase.OpenAsync` has explicit-extension overloads. It applies all
platform migrations first, then validates owner scope, retained namespaces,
version/ID/checksum identity and platform minimum before applying each extension
effects-plus-marker transaction. The existing overload supplies no descriptors
and rejects a v5 database retaining an extension. It rejects omission, newer
extension schema, gaps, duplicate/unsorted namespaces, checksum drift and unmet
minimum platform version. A conservative statement review rejects apparent
`gp_` platform-table references in consumer SQL; this is an API review guard,
not a SQL sandbox.

Focused native evidence (Windows x64, .NET SDK 9.0.203):

```text
rtk dotnet build GamePlatform.sln -c Release --no-restore
exit 0
ok dotnet build: 18 projects, 0 errors, 0 warnings

rtk dotnet test tests/GamePlatform.Tests/GamePlatform.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~Cl017ExtensionMigrationTests|FullyQualifiedName~Cl010MigrationRegistryTests|FullyQualifiedName~Cl101AccountsLifecycleTests"
exit 0
ok dotnet test: 19 tests passed, 0 warnings in 1 projects

rtk dotnet test GamePlatform.sln -c Release --no-build --no-restore
exit 0
ok dotnet test: 389 tests passed, 0 warnings in 1 projects

rtk python scripts/validate.py
exit 0
Validation passed: 15 projects, 16 evidence-aware feature states, direct/transitive references, runtime packages, module READMEs, and reviewed contract pins.

rtk python -m unittest discover -s scripts -p "test_*.py"
exit 0
Ran 51 tests
OK
```

The selected tests use the real pinned Windows x64 SQLite library and cover
fresh v4/v5 installation, retained v1-v3 platform rows during upgrade, Accounts
v4, platform upgrade with an extension, independent extension upgrade,
omission/newer/drift/gap rejection, effects/marker checkpoint rollback, and
concurrent open. Full-suite, physical-fault, non-Windows native, Unity/AOT and
device validation are recorded separately and are not claimed by this evidence.
