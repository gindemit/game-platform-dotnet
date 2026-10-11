# Portable command stream recovery - 2026-10-11

## SDK contract and composition

The owner selected the executable backend gaps in the workspace's
`plans/BACKEND-SDK-CLIENT-GAPS-2026-10-11.md`, narrowed to SDK functionality.
Backend baseline is `c53e8f6e0b90c9e291f2d5f11310510d157e187d`; SDK base is
`239bb4222f8270fb15017c9bcf4b78be20a895ec`. Existing recovery/inspection wire
DTOs and the bounded MessagePack codec are reused without canonical schema changes.

`ProductionBackendHttpProviders.ForAccount(...).CommandStreamRecovery` exposes
account/installation-owned inspection and recovery. BoundedHttpClientExecutor admits
only the two exact additional POST paths `/sync/streams/inspect` and
`/sync/streams/recover`. Authentication refresh remains bound to the issued account;
malformed protocol responses and uncertain delivery do not prove recovery failed.

`CommandStreamRecoveryCoordinator` is explicitly composed from the issued directory
entry, that remote, existing command receipts, a persistent recovery journal, prior
command storage, a consumer-owned recovery host, and a UUIDv7 generator. Constructors
start no IO. `RecoverAsync(reason)` starts only after the host establishes the specific
damage condition and drains prior writers. `ResumeAsync` never starts a new rotation.
The owner awaits `StopAsync` before replacing this coordinator/account lifetime.

### Independent review corrections

Fresh Sol review identified two crash/retry defects in the initial delivery.
An Adopted journal now quiesces the successor, including a crash after bootstrap
commits but before the completion journal update. An uncommitted expired request
can now leave Prepared only after a validated recovery HTTP 409 envelope with
category `conflict` and code `inspection_expired`. The backend serializes on the
prior stream and checks recorded recovery success before expiry rejection.
The retained request becomes terminal Expired history; a later explicit resume
reconciles prior commands and obtains a fresh inspection and new identities using
the original reason. Offline, uncertain, generic conflict and malformed responses
never replace the retained request. Journal v2 preserves all v1 rows in the
directory transaction, and the original directory migration checksum is unchanged.
Luna independently reviewed these corrections; source and consumer qualification
are recorded separately from the initial dev.138 receipt.

The Unity consumer now also has a narrow serialized directory-extension API.
`ExecuteExtensionAsync` supplies a synchronous borrowed transaction for consumer
`pb_` tables. A `SqliteAccountsDirectoryStore` new-reservation callback shares the
actual reservation transaction, allowing an admission witness to roll back with
the reservation. The callback never runs for an existing reservation. Borrowed
operations expire at callback exit, require the callback thread, and reject
transaction controls and reentrant directory work. Consumers remain responsible
for their extension schema and must never modify SDK directory/recovery tables.
This API is an adapter extension, not a sandbox for untrusted SQL.

Corrected-source validation in `C:/Work/sdk-recovery-review-validation-20261011`:
72/72 focused recovery tests and 876/876 full Release tests, zero skips; locked
restore, schema generation check, structure validation, 69/69 Python validator
and packaging tests, and whole-solution whitespace verification pass. The first
extension test incorrectly used Task.Run as proof of another thread; its possible
inline execution exposed the test defect, repaired with an explicit Thread.
Fresh Luna review also requested execution-context reentry fencing: AsyncLocal now
rejects child-task directory calls before they can wait on the callback's writer.
The focused/full runs above include both corrections. Real PostgreSQL, Unity and
physical runtime remain separate downstream evidence; no backend change is needed
for the narrow authoritative expiry response already supported by its handler.

## Durable recovery and host requirements

`SqliteCommandStreamRecoveryJournal` uses `SqliteAccountsDirectoryDatabase`, outside
the recreatable gameplay DB. Its additive versioned schema is initialized in the
existing principal-directory transaction, preserving the original v1 checksum.
Prepared requests retain the complete immutable owner, token, recovery ID, successor
and watermark. Lost acknowledgement retries use that same request even after token
expiry. RemoteCommitted is persisted before adoption; adoption updates directory
identity and journal phase in one real transaction. Completed rows remain as history.

`SqliteRecoveryCommandStore` durably suspends the retained prior DB and preserves
commands/sequence/projections/checkpoints. Receipt lookup may mark real terminal
outcomes; it neither applies confirmed value nor advances stream/pull boundaries.
Absent/conflicting/unavailable receipts block rotation. There is no command transfer,
historical reward replay or implicit discard. Backlogs exceeding 256 unresolved
commands or 8 MiB require a bounded handoff and remain on disk, suspended.

For a genuinely recreated game DB, the host may explicitly select
`verifiedRecreatedAndEmpty: true` only after proving damage and retaining the scoped
prior directory identity. The adapter requires every core content table empty, no
extension migration and no unknown table. It does not manufacture a prior stream
row. Lost unsent progress cannot be restored by recovery; the consumer must explain
that loss honestly. Without retained prior identity, remain blocked. Ordinary
network/authentication failure is not damage evidence and does not rotate a stream.

The game-owned host must drain/fence gameplay, push, pull, bootstrap and callbacks,
keep account/app/backend/installation scope intact, retain quarantined prior files,
and open successor storage with sequence 1 without copying old commands. It must
install a genuine complete private bootstrap with the existing private-sync rules.
Only successful bootstrap plus durable journal convergence permits admission.
BootstrapComplete journal state alone is not Ready; resume rechecks actual bootstrap
and quiesces the current successor. A new later rotation uses a new lifetime bound
to the current adopted directory entry. No Unity host or damage detector is supplied
by this SDK change; those remain consumer work.

## Actual route and command coverage

| Backend surface | Current SDK production support |
| --- | --- |
| Provision, bootstrap/pages, pull, push, receipt lookup | Existing providers preserved |
| Stream inspect, stream recover | New account-owned CommandStreamRecovery provider |
| Gameplay reward receipts | Existing G3 provider and limits preserved |
| Teams query/commands, Friends query/commands/friends rankings, leaderboard query/country | Existing qualified providers preserved |
| Campaign receipt evidence | Intentionally game-owned; no SDK DTO migration |
| profile.patch, gameplay.session.completed | Existing command encoding/services preserved |
| store.offer.purchase, quest.claim | Existing capability-gated support; still default off |

No executable backend route in the selected audit remains missing from the portable
SDK except the intentionally game-owned campaign boundary. This does not establish
parity for internal/admin services or client feature activation. Historical P3/G3,
broad P4, ordinary shell, WebLite and store/quest gates are unchanged.

## Validation and remaining acceptance

Disposable SDK source: `C:/Work/sdk-recovery-validation-20261011`.
Full Release `dotnet test GamePlatform.sln -c Release --no-restore` passed
867/867, no skips, after adding AOT preservation for the three new SQLite row types.
The first full pass had one missing-link.xml failure (866 passed); that was repaired.
The first compile also exposed test nullable annotations and an incorrect projector
method name; those were repaired before the focused pass (95/95).
`python scripts/validate.py` passed; validator/packaging Python regression passed
69/69. Fresh nonauthor source review approved after fixing nested SQLite transaction
controls, true empty-DB handling, completed resume identity and empty-state bounds.

Disposable backend `C:/Work/backend-recovery-validation-20261011` at the exact
baseline passed `npm ci --ignore-scripts`, `npm run build`, and
`node scripts/run-suite.mjs http --case BE-010` (35/35, zero skips).
Those backend handler tests and SDK tests are separate evidence, not a joined
PostgreSQL/C# production-peer run. `docker version --format '{{.Server.Version}}'`
did not return within the local probe and was cancelled; isolated BE-010 PostgreSQL
tests and joined real peer acceptance were not run.

Tests cover three recovery reasons, a truly empty recreated DB, immutable request
restart/lost response including expiry, real SQLite directory/journal rollback,
unresolved receipt retention, healthy stream untouched, offline retry, late callback
retirement, bootstrap failure and complete real SQLite successor bootstrap with
sequence 1. The retained same-stream regression tests also passed in the full suite.
Backend tampered/expired/state-advance/collision and broader joined failure matrices
remain actual peer qualification, not assertions of source-only completion.

Package hashes and final source identity are recorded separately in DELIVERY.md.
Unity pin/import, EditMode/PlayMode, Android IL2CPP/native SQLite/Keystore/TLS,
physical process death and Player flows are not run or activated by this SDK-only
change. The pre-existing Unity working tree is untouched.
