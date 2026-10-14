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

## Retained receipt HTTP increment — 2026-09-20

Source commit `7955e2d39b164d9a427e64c4c2903c620b114298` adds the
portable `CommandReceiptHttpProvider` over injected `IWireCodec`,
`IHttpExecutor`, `IAuthSession` and externally owned `AuthRefreshCoordinator`
ports. The provider freezes `POST /v1/apps/{appId}/sync/receipts/lookup`, binds
the configured app/account/installation and retained command identity, uses the
canonical v1 SHA-256 fingerprint text, and maps only full accepted or terminally
rejected receipts to defensively copied terminal result bytes. Authorized
`found:false` remains an observation only when `observedFinalizedThrough` is
below the queried sequence. Identity drift, missing results at/below the
watermark, retryable terminal errors, unsupported fingerprint versions,
malformed media/correlation/envelopes and decode/encode failures fail closed.

The focused provider selection passed 14/14. The provider plus existing CL-009
ordered-sender and CL-010 private-sync-provider regression passed 46/46 with no
warnings, and `rtk python scripts/validate.py` passed. One test uses the desktop
qualification codec through an explicit test-only port bridge; production
`IWireCodec`, a concrete executor/host and push provider remain unavailable.
No live backend, sender receipt reconciliation, process-kill, Unity/AOT/device,
independent review, G3 or P4 evidence is claimed. Exact machine-readable results
are in `receipt-provider-results.json`.
