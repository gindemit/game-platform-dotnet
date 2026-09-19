# CL-007 Windows x64 implementation evidence

Date: 2026-09-19

Source commit: `91a67c7805ff59bc3b0ca286fddc2a5124291188`

CL-007 now has an actual `netstandard2.1` SQLite executor backed by the pinned
SQLite3 Multiple Ciphers Windows x64 library. It owns one full-mutex connection,
serializes transactions off the caller thread, scopes migration state by
backend/application/account, checks immutable migration identities and checksums,
and drains admitted work during bounded disposal. Borrowed transaction sessions
cannot commit, roll back, attach another database, vacuum, or outlive their
callback.

Eight real-native xUnit cases cover migration/commit/rollback/reopen, foreign
keys, WAL and busy-timeout configuration, exact signed-64 preservation,
deterministic migration timestamps, session invalidation, injected failure on
both sides of the migration marker, checksum/newer-schema rejection, parallel
writer serialization, queued versus active cancellation, bounded disposal,
scope ownership, SQL guard behavior, and external-writer lock classification.

The CL-010 registry increment at source
`cc329e39d309808150af760d30beb33861ed9fc7` composes the retained outbox v1
and additive private-sync v2 through this same journal. Five additional native
cases prove fresh/upgrade/reopen/drift/rollback behavior and preservation of
Ready, immutable commands, confirmed view, opaque checkpoint and staging rows.
The combined CL-007/008/009/010 native selection passes 46/46; this does not
close the physical-fault or non-Windows acceptance gaps below.

## Commands and raw results

All commands ran from the repository root on Windows x64 with .NET SDK 9.0.203.
The `rtk` prefix diagnostic is omitted below; product output is preserved.

```text
rtk dotnet restore GamePlatform.sln --locked-mode
exit 0
ok dotnet restore: 18 projects, 0 errors, 0 warnings (unknown)

rtk dotnet build GamePlatform.sln -c Release --no-restore
exit 0
ok dotnet build: 18 projects, 0 errors, 0 warnings (00:00:01.08)

rtk dotnet test GamePlatform.sln -c Release --no-build --no-restore
exit 0
ok dotnet test: 244 tests passed, 0 warnings in 1 projects (3.1 s)

rtk python scripts/validate.py
exit 0
Validation passed: 15 projects, 16 evidence-aware feature states, direct/transitive references, runtime packages, module READMEs, and reviewed contract pins.

rtk python -m unittest discover -s scripts -p "test_*.py"
exit 0
Ran 33 tests in 6.457s
OK

rtk python docs/implementation/validate_manifest.py
exit 0
VALID: 52 tasks; 16 feature ledgers; 21 conservative waves

(from docs/implementation) rtk python -m unittest test_manifest
exit 0
Ran 14 tests in 0.037s
OK

rtk git diff --cached --check
exit 0
```

Two superseded command-path attempts are retained as environment/operator
limits, not product failures: `scripts/validate.ps1` did not exist, and invoking
`docs.implementation.test_manifest` from the repository root could not resolve
its sibling import. The commands above are the repository-supported replacements
and both passed.

## Integrity and limits

- Vendored managed source after the documented Unity-attribute portability
  patch: SHA-256 `2157936811c69cbd235a4545415c467b76e88f4b645853b88ca7c00c7ef21a4d`.
- Native Windows x64 DLL: SHA-256
  `e7e370938925dff66d9a4d50bfac6d18cc9505bd2f3f127a232b410491b46e3e`.
- Runtime native SQLite version: `3050001` (3.50.1).
- The abrupt OS process-kill window, real disk-full/corruption injection,
  non-Windows native libraries, Unity Editor/IL2CPP/AOT/stripping, and physical
  device behavior were not run. Those remain CL-015/INT-009 and acceptance
  gates; no production or Unity capability is claimed.
