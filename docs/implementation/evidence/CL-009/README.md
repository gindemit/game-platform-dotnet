# CL-009 ordered sender evidence

Date: 2026-09-19

Source commit: `6ee33493e2919086331918210d01aa26835243ac`

The bounded Windows x64 slice adds a consumer-owned delivery port, a real
SQLite implementation and a portable single-cycle sender. Leasing is scoped to
the stream's exact `finalizedThrough + 1`; concurrent senders cannot lease the
same row. Accepted and terminally rejected outcomes update the immutable row's
delivery metadata and stream watermark in one transaction. Retryable known
failures release the same identity; an uncertain outcome remains leased and is
retried only after expiry. Terminal rejection unblocks the next sequence.

## Commands and raw results

```text
rtk dotnet restore GamePlatform.sln --locked-mode
exit 0; 18 projects, 0 errors, 0 warnings

rtk dotnet build GamePlatform.sln -c Release --no-restore
exit 0; 18 projects, 0 errors, 0 warnings

rtk dotnet test tests/GamePlatform.Tests/GamePlatform.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~Cl009OrderedCommandSenderTests
exit 0; 5 passed, 0 failed/skipped/warnings

rtk dotnet test GamePlatform.sln -c Release --no-build --no-restore
exit 0; 276 passed, 0 failed/skipped/warnings

rtk python scripts/validate.py
exit 0

rtk python -m unittest discover -s scripts -p "test_*.py"
exit 0; 33 passed

rtk python docs/implementation/validate_manifest.py
exit 0; 52 tasks, 16 feature ledgers, 21 waves
```

## Remaining limits

This is same-agent implementation/self-review. It does not supply the frozen
push/receipt HTTP provider or claim a live backend. Single-command cycles are
bounded but multi-command batching, jitter/backoff, durable blocked-auth state,
explicit receipt reconciliation after unknown commit, rejected-overlay rebuild,
process-kill/reopen, disk/corruption, peer, Unity/AOT/device and real-game tests
remain. The M0 coordinator stays unavailable. Pull cursors are absent from this
API, so push finalization cannot advance them. CL-009 and A03/A07/A12 remain
partial; G3 and P4 are unchanged.
