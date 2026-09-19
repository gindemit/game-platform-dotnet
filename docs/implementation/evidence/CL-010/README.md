# CL-010 private pull, bootstrap and reset evidence

Initial date: 2026-09-19; corrective review finalized 2026-09-20

Effective source commit: `6462d471d498eb92c4a773cd008e979924afd6dc`

Initial HTTP provider source commit: `ebef5ae2e5efc63b897f12b1f5d53e138dfb90de`

Storage/policy source commit: `13c36e9083a6b814896724e949f0f7f1dfd35c29`

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

The second corrective review increment applies the frozen state invariant
directly: retired always means `nextSequence = null`; active requires a positive
non-null successor and an exhausted active stream is rejected before staging.
Dedicated native cases inject a foreign-stream row, pending and in-flight rows
at/below authoritative finalization, and accepted and terminal-rejected rows
beyond it. Every case proves transaction rollback plus preservation of the old
Ready flag, opaque cursor, committed checkpoint and confirmed view.

## Commands and results

```text
rtk dotnet test tests/GamePlatform.Tests/GamePlatform.Tests.csproj -c Release --filter FullyQualifiedName~Cl010PrivateSyncTests --no-restore
exit 0; 20 passed, 0 failed/skipped/warnings

rtk dotnet test tests/GamePlatform.Tests/GamePlatform.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~Cl007|FullyQualifiedName~Cl008AtomicOutboxTests|FullyQualifiedName~Cl009OrderedCommandSenderTests|FullyQualifiedName~Cl010PrivateSyncTests"
exit 0; 41 passed, 0 failed/skipped/warnings

rtk dotnet restore GamePlatform.sln --locked-mode
exit 0; 18 projects, 0 errors/warnings

rtk dotnet build GamePlatform.sln -c Release --no-restore
exit 0; 18 projects, 0 errors/warnings

rtk dotnet test GamePlatform.sln -c Release --no-build --no-restore
exit 0; 296 passed, 0 failed/skipped/warnings

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

The remote port now has a portable authenticated pull/bootstrap semantic HTTP
provider. Production codec registration, concrete executor/hosted backend execution,
fixed-boundary peer transcripts, final migration registry composition,
multi-app shared-row provider integration, physical process-kill/disk/corrupt
faults, Unity/IL2CPP/AOT/device execution and independent review remain unrun.
The native evidence is Windows x64 SQLite only. CL-010 and A04/A05/A07 therefore
remain partial; G3 and P4 are unchanged.

## 2026-09-20 authenticated HTTP provider increment

The dependency check ran at coordinated SDK head
`568d945c5597f085484465ac4372e4d2e2a3b9b3` against accepted backend head
`e1e687605e9f5d2fc51aa6be15d804c0f1248e96`. Backend route and HTTP tests freeze
the authenticated bootstrap start/page and pull routes, fixed-boundary final
cursor behavior, successful reset envelopes, MessagePack preference, explicit
diagnostic JSON negotiation, bounded bodies, and protocol error mapping.

The portable provider is implemented over consumer-owned interfaces without
claiming their production composition. It sends the exact frozen methods,
routes, query and bodies; selects MessagePack by default or diagnostic JSON only
when explicitly constructed; bounds every top-level codec invocation; refreshes
one rejected auth generation; maps cancellation, transport and pre-envelope
failures; decodes protocol errors; and maps final bootstrap cursors, complete
pull groups and reset reasons into the existing coordinator port. Decode failure
never triggers representation fallback.

Twenty-one new scripted provider cases bring focused CL-005/CL-010 coverage to
53/53 and the full suite to 317/317. They are portable boundary tests, not live
backend or production-codec/executor evidence. `TypedQualificationCodec` remains
qualification-only, `UnavailableMessagePackCodec` remains production-facing,
and no concrete executor/host is registered. CL-010 remains `in_progress`.

The corrective provider increment requires the expected `PlatformUserId` at
construction and rejects a bootstrap response for another account before it can
reach the coordinator/storage path. An externally owned, disposable refresh
coordinator supplies auth-session/account-keyed single-flight across provider
instances; providers borrow it and never dispose it. Protocol error correlation
must exactly match the response header. Bootstrap pages and pull pages use the
minimum of global and requested byte caps, including pre-envelope/error bodies.
Reset reasons outside the four frozen values fail protocol validation. Six new
negative/race cases bring provider boundary coverage to 27, focused CL-005/
CL-010 to 59/59 and the full suite to 323/323.
