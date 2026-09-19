# CL-008 atomic outbox evidence

Date: 2026-09-19

Source commit: `4de294141ea7d0f6cf062b8ce736ea0ce89fa112`

The qualified Windows x64 slice now admits a provisional projection, checked
stream sequence/local revision, and immutable semantic outbox row in one CL-007
transaction. Owner, Ready and stream identity are validated before mutation.
Exact operation/business-run retries return the original identity; changed body,
operation, stream or version is a typed conflict. Scheduling/lease fields remain
separate from trigger-protected immutable command fields.

The 8-case real-native suite includes an actual child process that is terminated
at the pre-commit outbox boundary and after commit. Reopen observes all three
effects or none. It also covers every injected write boundary, concurrent
duplicate callbacks, changed-body/run conflicts, non-Ready/foreign-stream and
sequence-overflow rejection, repeated reopen, immutable SQL enforcement,
abandoned lease recovery, local gaps and server-ahead restored-state detection.

## Commands and raw results

```text
rtk dotnet test tests/GamePlatform.Tests/GamePlatform.Tests.csproj -c Release --filter FullyQualifiedName~Cl008AtomicOutboxTests --no-restore
exit 0
ok dotnet test: 8 tests passed, 0 warnings in 1 projects (3.0 s)

rtk dotnet restore GamePlatform.sln --locked-mode
exit 0
ok dotnet restore: 18 projects, 0 errors, 0 warnings (unknown)

rtk dotnet build GamePlatform.sln -c Release --no-restore
exit 0
ok dotnet build: 18 projects, 0 errors, 0 warnings (00:00:01.48)

rtk dotnet test GamePlatform.sln -c Release --no-build --no-restore
exit 0
ok dotnet test: 252 tests passed, 0 warnings in 1 projects (3.4 s)

rtk python scripts/validate.py
exit 0
Validation passed: 15 projects, 16 evidence-aware feature states, direct/transitive references, runtime packages, module READMEs, and reviewed contract pins.

rtk python -m unittest discover -s scripts -p "test_*.py"
exit 0
Ran 33 tests in 6.669s
OK

rtk python docs/implementation/validate_manifest.py
exit 0
VALID: 52 tasks; 16 feature ledgers; 21 conservative waves

(from docs/implementation) rtk python -m unittest test_manifest
exit 0
Ran 14 tests in 0.037s
OK

rtk git diff --check
exit 0
```

## Remaining limits

Real disk-full/corruption injection, terminal result/finalized-through persistence,
authorized server-side stream rotation, Unity/AOT/device execution and full game
projection integration remain unimplemented or unrun. Recovery assessment fails
closed and does not rotate/relabel a stream. These limits keep CL-008
`in_progress` and keep CL-009/010, CL-015, INT-005/007/009 and G3 outstanding.

