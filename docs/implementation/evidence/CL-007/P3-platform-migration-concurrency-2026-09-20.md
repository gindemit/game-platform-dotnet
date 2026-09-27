# P3 platform-migration concurrent-open correction — 2026-09-20

Source branch: `impl/cl007-platform-race-20260920`, based on integrated SDK
`139c1c4`.

Two separate concurrent full-suite runs exposed an intermittent initialization
failure before extension migrations: one attempted to create `gp_stream_state`,
and another attempted to create `gp_sync_state`, after a peer had committed the
same platform migration. The immediate full-suite rerun passed, confirming this
was scheduling-dependent rather than a journal/checksum mismatch.

`SqliteDatabase.ApplyMigrations` now takes SQLite's `BEGIN IMMEDIATE` write
reservation before reading either the platform maximum or an individual marker.
It uses matching SQL `COMMIT`/best-effort `ROLLBACK` for that reservation. A
blocked opener is classified as Busy, retries on a fresh connection, and then
re-reads the committed journal; it cannot replay platform DDL from an obsolete
preflight read. Platform and extension checksums, ordering, owner scope, journal
contents and bounded retry policy are unchanged.

The existing native concurrent-open test now releases eight independent opens
through a `Barrier` against each of sixteen fresh database files. It asserts all
five platform markers and exactly one extension marker after each iteration:
128 synchronized opens per invocation, with no timer/sleep race shaping.

Windows x64 / .NET SDK 9.0.203 evidence:

```text
rtk dotnet test tests/GamePlatform.Tests/GamePlatform.Tests.csproj -c Release --filter "FullyQualifiedName~Cl007P3ExtensionMigrationTests" --logger "trx;LogFileName=cl007-platform-race.trx"
exit 0
9 passed, 0 failed, 0 warnings

rtk powershell -NoProfile -Command '<8 selected test invocations>'
exit 0
8 passed invocations; each exercised 16 × 8 synchronized fresh opens (1,024 opens total)

rtk dotnet test GamePlatform.sln -c Release --no-restore --logger "trx;LogFileName=full-cl007-platform-race.trx"
exit 0
408 passed, 0 failed, 0 warnings

rtk python scripts/validate.py
exit 0
Validation passed

rtk python -m unittest discover -s scripts -p "test_*.py"
exit 0
51 passed

rtk python docs/implementation/validate_manifest.py
exit 0
VALID: 52 tasks; 16 feature ledgers; 21 conservative waves

rtk python -m unittest discover -s docs/implementation -p "test_*.py"
exit 0
14 passed
```

This is real Windows x64 SQLite evidence for the initialization race only. It
does not establish physical-fault, non-Windows native, Unity/IL2CPP, device, or
G3 acceptance.
