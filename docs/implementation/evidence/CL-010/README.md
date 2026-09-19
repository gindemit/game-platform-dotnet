# CL-010 private pull, bootstrap and reset evidence

Date: 2026-09-19

Effective source commit: `e7dd26813064d6114fe504983213d8f9eaa15d2d`

Initial source commit: `a9d23c611763939466f33f3817624deffe871470`

Accepted backend inputs: coordination head
`e1e687605e9f5d2fc51aa6be15d804c0f1248e96`, BE-012 corrective source
`048d3e427a5c78e4cdefd4a1cb82b0ee07bd695b`, and the BE-013 stale-view
authorization correction `ebd2bced1fbb4e34986dcabd68c644b0223825cb`.

The Windows x64 slice stages snapshot pages durably without exposing them,
checks the exact requested page/session/fixed boundary, and installs the whole
confirmed view, opaque initial cursor and Ready state in one SQLite transaction
only on a final page. Pull stores one fixed high watermark across pages and
advances solely to the server-provided opaque cursor; an empty final page can
therefore advance an invisible prefix. Projection versions prevent stale
regression and preserve the distinct `removed` and `tombstone` meanings.

Retention/visibility/log-epoch/cursor reset responses invoke the same complete
bootstrap path. Replacement does not touch `gp_outbox`; a required borrowed
overlay-rebuild callback executes before the transaction commits. Injected
failure immediately before final installation rolls back the final page and a
real close/reopen resumes from the prior durable token. Lost remote pages retain
the old confirmed view and staged continuation.

The corrective review increment compares every incoming feed revision to the
durable checkpoint across pages, not only to its response neighbor. Bootstrap
now persists and reconciles the authoritative `active`/`retired`,
`finalizedThrough` and nullable `nextSequence` stream boundary before Ready.
Server-ahead state, local gaps, missing terminal results, terminal-ahead state,
and pending work on a retired stream fail atomically. Accepted, rejected,
in-flight/uncertain and pending rows remain byte/identity stable. Retired streams
can install a readable private view but leave command admission disabled.
Expired staging is replaced only after a new remote start succeeds. Storage
port collection inputs are defensively copied. Native cases also prove distinct
view-removal/tombstone rows and their removal by reset replacement.

## Commands and results

```text
rtk dotnet test tests/GamePlatform.Tests/GamePlatform.Tests.csproj -c Release --filter FullyQualifiedName~Cl010PrivateSyncTests --no-restore
exit 0; 14 passed, 0 failed/skipped/warnings

rtk dotnet test tests/GamePlatform.Tests/GamePlatform.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~Cl007|FullyQualifiedName~Cl008AtomicOutboxTests|FullyQualifiedName~Cl009OrderedCommandSenderTests|FullyQualifiedName~Cl010PrivateSyncTests"
exit 0; 35 passed, 0 failed/skipped/warnings

rtk dotnet restore GamePlatform.sln --locked-mode
exit 0; 18 projects, 0 errors/warnings

rtk dotnet build GamePlatform.sln -c Release --no-restore
exit 0; 18 projects, 0 errors/warnings

rtk dotnet test GamePlatform.sln -c Release --no-build --no-restore
exit 0; 290 passed, 0 failed/skipped/warnings

rtk python scripts/validate.py
exit 0

rtk python -m unittest discover -s scripts -p "test_*.py"
exit 0; 33 passed

rtk python docs/implementation/validate_manifest.py
exit 0; 52 tasks, 16 feature ledgers, 21 waves

(from docs/implementation) rtk python -m unittest test_manifest
exit 0; 14 passed
```

## Limits

The remote port is intentionally not a fake production provider. Authenticated
pull/bootstrap HTTP, production codec registration, hosted backend execution,
fixed-boundary peer transcripts, final migration registry composition,
multi-app shared-row provider integration, physical process-kill/disk/corrupt
faults, Unity/IL2CPP/AOT/device execution and independent review remain unrun.
The native evidence is Windows x64 SQLite only. CL-010 and A04/A05/A07 therefore
remain partial; G3 and P4 are unchanged.
