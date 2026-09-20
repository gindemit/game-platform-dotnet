# CL-102 profile projection and honest pending-edit evidence

Source task branch: `impl/cl102-profiles-20260920`, based on integrated SDK
`8adf434`. Milestone M3/C-D; status `in_progress`; A07 is not passed.

## Implemented bounded scope

`ProfileService` is a portable per-captured-owner coordinator over the existing
CL-008 atomic command and CL-011 durable feature-state seams. It has immutable
confirmed and pending profile observations, exact expected-revision admission,
one durable pending edit per account/view/generation, command/business-source
duplicate recognition, receipt-to-awaiting-pull handling, monotonic confirmed
replacement, and explicit conflict rather than automatic merge when an
unreceipted local edit loses to a newer server revision.

The only edit fields are frozen `displayName`, `avatarKey`, and `locale` patch
fields. The public SDK contracts do not expose `createdBy`, `updatedBy`, actor,
executing-service, causation, or server audit time inputs. Codec validation
owns supported extension shape/depth; the service bounds and defensively copies
the opaque bytes and the underlying durable store preserves them across a local
pending edit. Remote reads are coalesced by the complete backend/app/account/
view/generation key. A remote result from a different owner/generation is
rejected before it can publish or write state.

No profile HTTP adapter, wire registration, production codec implementation,
new SQLite migration, global account selector, Unity integration, audit-field
shortcut, response replay, or local authority is included.

## Commands and results

```text
rtk dotnet restore GamePlatform.sln --locked-mode
exit 0
ok dotnet restore: 18 projects, 0 errors, 0 warnings (unknown)

rtk dotnet build GamePlatform.sln -c Release --no-restore
exit 0
ok dotnet build: 18 projects, 0 errors, 0 warnings

rtk dotnet test tests/GamePlatform.Tests/GamePlatform.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~Cl102
exit 0
ok dotnet test: 6 tests passed, 0 warnings in 1 projects

rtk dotnet test GamePlatform.sln -c Release --no-build --no-restore
exit 0
ok dotnet test: 389 tests passed, 0 warnings in 1 projects

rtk python scripts/validate.py
exit 0
Validation passed: 15 projects, 16 evidence-aware feature states, direct/transitive references, runtime packages, module READMEs, and reviewed contract pins.

rtk python -m unittest discover -s scripts -p "test_*.py"
exit 0
Ran 51 tests; OK

rtk git diff --check
exit 0
```

The first three focused cases run through the pinned Windows x64 SQLite native
library. They prove a confirmed profile plus opaque extension survives reopen;
the pending projection, stream sequence, and immutable outbox command commit
together; duplicate callback does not add a command; accepted receipt alone is
not confirmation; matching later pull clears it; and a newer unreceipted device
revision yields durable conflict. The refresh coalescing and foreign-late-owner
cases deliberately use an injected non-production `IProfileRemote`; they are
not HTTP, host, Unity, device, or peer-runtime evidence.

## Remaining gates and limits

BE-016 real host composition and a production profile remote/codec adapter are
still prerequisites. No compatible live backend exchange, ordered command sender
receipt hookup, private-feed wiring, cache-reset host lifecycle, Unity INT-008
presenter, offline-to-reconnect journey, device test, or independent review ran.
Consequently this task remains `in_progress`, A07 remains unpassed, INT-008 is
not claimed, and G3 is not advanced.
