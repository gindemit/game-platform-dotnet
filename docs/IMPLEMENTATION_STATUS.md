# Implementation status

## P3 current SDK/SQLite Unity bundle refresh — 2026-09-20

The current coordinated SDK head `686721a` was packaged in an isolated clean
worktree with a packaging-only metadata correction at `0b180ac`: the manifest
and normal verifier now require the already-promoted production
`MessagePackWireCodec` instead of mislabeling it unavailable. The unpublished
bundle contains all 15 current SDK assemblies, the exact eight-DLL MessagePack
runtime closure (nine package records; analyzer build-only), three reviewed
SQLite native targets, 38 P/Invoke declarations and 100 files including its
manifest. It is source-clean and has manifest SHA-256
`3eae6d4e278b9162101f566df25520542616619c480482e2ecce6c4d18155230`.

Locked restore/Release package build and normal inventory/hash/closure/license/
native/AOT verification pass with zero warnings/errors. Packaging tests pass
29/29, full SDK tests 485/485, validation passes and validator tests pass 19/19.
Two independent clean clones with intentionally different origin metadata
produced byte-identical complete 100-file outputs. The fresh evidence is
[CL-015 current bundle](implementation/evidence/CL-015/g3-current-bundle-2026-09-20.json).

This resolves no Unity or live gate: current-bundle Unity import, IL2CPP/device
execution, macOS native execution and the G3 journey remain unverified. Nothing
was published or deployed.

## P3 G3 receipt and durable correlation increment — 2026-09-20

The approved v2 G3 reward-receipt carrier now has a closed production
MessagePack mapper and a read-only authenticated HTTP provider on the frozen
GET route. Account production composition exposes that provider through the
neutral `IHttpExecutor`; route admission requires the exact app/operation path,
no query and no request body. Response operation identity, protocol media type,
bounded body and correlation header fail closed.

CL-107 now has production durable codec, immutable evidence and exactly-once
claim composition over the caller-owned serialized store. Durable correlation
preserves both receipt-before-pull and pull-before-receipt across restart. Feed
evidence is staged in the projection/cursor transaction and confirmation occurs
only after commit. It requires the receipt's exact feed revision and exact
kind/resource membership, retains installed component revisions, and never
applies receipt quantity or increments a value projection revision. Changed
same-revision replay fails closed; exact replay is idempotent.

Focused production receipt/correlation tests pass 15/15. Full verification and
exact commands are recorded in the P3 receipt-correlation evidence. This does
not supply the owner-assigned invalidation migration, Unity feed-sink adapter,
bundle/device/live-backend journey, INT-007, BE-025/INT-010 or G3 acceptance.


## P3 bounded live-composition bridge — 2026-09-20

The SDK now supplies the production `gsc1` command-fingerprint adapter for the
complete durable command envelope and proves it against every applicable frozen
known-answer vector. Production HTTP composition accepts the neutral
`IHttpExecutor` boundary (including a separately qualified Unity executor) and
exposes command push with private sync and receipt lookup after account capture.

Private sync now preserves remote revision zero and carries the fixed snapshot/
pull boundary plus server observation time into an observed typed projector. The
bounded G3 projector decodes profile, progression and wallet replacement/group
DTOs inside the raw-row/cursor transaction, handles removals, and rejects any
non-empty inventory or entitlement row rather than inventing catalog/origin
data. Durable feature state supports borrowed transaction reads/deletes for the
consumer sink. Frozen `invalidation` remains fail-closed: storing it distinctly
requires a new owner-assigned platform migration because migration v6 constrains
raw state to visible/removed/tombstone.

CL-108 adds the consumer-owned synchronous transaction callback required by the
real completed-run writer. Game checkpoint/progress/client presentation writes
can now join pending progression, sequence allocation and immutable outbox
admission; failure/cancellation rolls back all effects and exact replay skips the
callback. Focused CL-108 passes 12/12. Integrated Release passes 473/473; locked
restore/build cover 18 projects with zero warnings/errors. Validation, 51 script
tests, manifest validation and 14 manifest tests pass.

This does not complete receipt/feed/operation correlation: the approved G3
receipt carriers still need a production mapper/provider plus durable correlation
composition. It also does not supply the owner-assigned invalidation migration,
SDK bundle/Unity runtime evidence, INT-007, BE-025/INT-010, or G3 acceptance.
See [evidence](implementation/evidence/P3-live-composition/README.md).

## P3 SQLite confirmed-projection invalidation migration — 2026-09-20

The immutable platform migration registry now appends v7,
`platform-confirmed-projection-invalidation-v1`. It atomically rebuilds only
`gp_confirmed_projection`, preserving its existing rows and constraints while
adding the frozen `invalidation` state as distinct from `visible`, `removed`
and `tombstone`. v1--v6 migration identities, SQL and checksums are unchanged.

Real Windows x64 SQLite tests cover fresh install, v6 upgrade, all four durable
states, v7 rollback before its marker, registry drift, reopen/idempotence and
barrier-based concurrent opens. Focused registry and extension suites pass
9/9 each; Release build has zero warnings/errors and the full SDK suite passes
469/469. This is migration-only: no wire/feature enum, projector mapping or
live composition is claimed. See [v7 evidence](implementation/evidence/CL-007/P3-platform-migration-v7-invalidation-2026-09-20.md).

## G3 receipt schema bridge prepared for cross-runtime evidence — 2026-09-20

The bounded qualification MessagePack schema engine now loads the two approved
additive receipt schemas without changing any of the 90 frozen core or four G2
contract bytes. Absolute canonical references normalize only to the pinned
common, live-slice receipt and G3 receipt documents. The ordered reward-line
index invariant is enforced, and focused C# tests cover required-null versus
absent reward, the exact `client_trusted_unvalidated` authority, unknown
authority rejection and signed64 overflow. Focused tests pass 2/2 and the full
SDK passes 370/370.

This is a qualification/exchange bridge, not an HTTP provider or feature. The
generated G3 carriers remain intentionally outside the production typed mapper;
source/business idempotency and the catalog/visibility bridge have no additional
wire fixture and must not acquire an invented client route. Four values passed
both TypeScript/C# directions and six authority/presence/unknown/version/signed64
negatives were rejected by both producers at SDK `469796b` and backend
`66ccc78`. The subsequent unchanged frozen-core regression passed 34 cases and
24 fingerprints each direction, 32 raw probes in both decoders, 39 diagnostic
negatives and 89 dotnet commands at backend `5963b45`.
Central G3 state and A01–A13 remain unchanged.

## CL-011 snapshot, durable-state and query-cache foundation — 2026-09-20

Source `978f87435b8d2c22f594af05fd9d857467fb9825` adds captured-owner
immutable feature snapshots, bounded canonical private query keys, and a
disposable single-flight cache with independent waiter cancellation, late-result
fencing and owner/generation validation. SQLite immutable migration version 3
adds a narrow durable feature-state adapter using caller-owned transactions and
an internal typed SQL row distinct from storage, feature and wire models. It
rejects revision/owner/view conflicts, defensively copies bounded bytes and
preserves opaque supported extension bytes across known-field read-modify-write.

Focused real Windows x64 SQLite/cache tests pass 9/9; the bounded CL-007/008/010/
011 storage regression passes 30/30. Release build covers 18 projects with zero
warnings/errors and the full SDK passes 368/368. Architecture/contract validation
passes; 51 script tests and the 52-task/16-ledger/21-wave manifest pass.

No catalog wire route, remote provider, reactive framework, Unity type, reward,
gameplay or legacy composition was added. Feature-specific authority/freshness
policy and extension shape/depth codecs, public/shared cache composition,
multi-database shared-row integration, physical fault, other native targets,
Unity/device and independent review remain unrun. CL-011 remains `in_progress`;
A07/A08 remain unpassed. See [evidence](implementation/evidence/CL-011/README.md).

## P3 same-phase G3 schema mirror and carrier checkpoint — 2026-09-20

The SDK exactly mirrors backend canonical commit
`0518ad565efdfe16df13e78e2acbb92033e8bd30`, contract
`0.4.0-g3-schema-checkpoint.1`: 98/98 files match, preserving all 90 G1 and four
G2 artifacts. Eight attribute-free C# carrier classes and two union interfaces
are generated for the v2 reward-receipt request/response, immutable
receipt/source and three typed reward lines. The recorded-completion carrier exposes the required constant
`client_trusted_unvalidated` label and a required nullable reward; receipt
signed64 values map to `long`.

Focused DTO tests pass 2/2. Pin, exact-mirror, generated-artifact and mapping
validators pass. This checkpoint deliberately does not register these types in
the production/qualification codec, add an HTTP provider, implement any feature,
change Unity, or claim C#/TypeScript binary interoperability, A01-A13 or G3.
Central coordination state remains unchanged.

## CL-015 generated-source byte identity correction — 2026-09-20

The DTO mapper generator now writes explicit UTF-8/LF bytes and its check mode
compares bytes. The prior Windows writer produced 1,149 CRLF lines in
`TypedQualificationCodec.g.cs` while Git stored LF; clean Git status and the old
newline-normalizing check hid the difference. The resulting PDB document checksum
changed MessagePack and dependent HTTP DLL/PDB/MVID identities at the same source
revision. Source Link, symbols and all package/native/license checks are retained.
The corrected check rejected the existing CRLF file, regeneration restored the
canonical LF bytes without a tracked generated-source diff, and packaging tests
pass 29/29. Source `67f79fc600ec90b9465f1c9a78ae123a4fe03c4d` passes the
independent two-clean-clone verifier once: 100 byte-identical files, 23 equal
MVIDs and 15 equal portable PDBs, with manifest SHA-256
`851856125f7109b871699a8a04bbe1ac1ddc8de9cb795d3a69b83eaf041907cc`.
An ordinary package/verify from the corrected working checkout also passes and
matches all 100 files. Locked restore/Release rebuild report zero warnings/errors.
See [archived evidence](implementation/evidence/CL-015/lf-reproduction/reproducibility-summary.json).
CL-015 remains M2/C, A09/A13, `in_progress`; macOS and new HTTP Unity/device/live
acceptance remain unverified.

## CL-015 HTTP transitive closure validation — 2026-09-20

Packaging input validation now accepts the HTTP composition's already reviewed
MessagePack project dependency. HTTP must have no direct package references and
must retain the codec project reference, the exact nine-package inventory and
versions, transitive lock types, and hashes/dependency edges matching the codec
lock. All other runtime projects still require explicit package review; bundle
resolution, notices, hashes and duplicate-acquisition checks are unchanged.

The original `runtime_package_requires_bundle_license_review` failure was
reproduced at `94b70628eedff9d1b4da3e05b0fb83447b7f6464`. Focused packaging
regressions pass 29/29, including the real input graph and nine rejected HTTP
dependency mutations. Source `81376de9c0375da7091c59797283678ae527a29d`
passes a fresh ordinary package and verification with the pinned SQLite source:
23 managed assemblies, three native targets, clean source, no skipped build and
manifest SHA-256 `538c133332a91b136758027f7a7dda0ddc78b05583b0607e86f17bfa6b993fce`.
Locked restore/Release rebuild pass with zero warnings/errors; all 51 script
tests and architecture/contract validation pass. CL-015 remains `in_progress`
under M2/C, A09/A13; independent
reproduction, macOS native execution and new HTTP Unity/device/live acceptance
remain unverified. No runtime dependency pin or Unity import changed.

## Bounded production HTTP composition increment — 2026-09-20

The SDK now has a concrete portable `System.Net.Http` executor for exactly the
frozen provisioning, bootstrap start/page, pull and command-receipt routes plus
an explicit production provider bundle using `MessagePackWireCodec`. The
executor confines requests to the configured HTTPS authority, rejects
unsupported routes/methods/headers/media types and redirects, caps request and
response bodies plus response headers, copies correlation/media headers and
preserves cancellation and not-sent/uncertain/response-received delivery
semantics. Composition carries no endpoint, token or provider credential and
leaves `HttpClient`, auth-session and refresh-coordinator lifecycle with the
application host.

Focused executor/composition plus existing provisioning/bootstrap/pull/receipt
provider tests pass 59/59. The transport/serialization regression passes
231/231; Release build covers 18 projects with zero warnings/errors and
architecture/contract validation passes. The existing MessagePack dependency
versions are unchanged; the HTTP project lock now records the already pinned
transitive codec closure.

This is not live-host or G3 evidence. Automatic redirects must be disabled by
the caller-owned handler. UnityWebRequest/device execution, BE-016 runtime
composition, HTTPS against a compatible nonproduction backend, push/account/
profile/stream-recovery providers, live credentials and A01–A13 acceptance
remain unavailable or unverified.

## CL-004 production wire adapter promoted — 2026-09-20

Source `684ed49c832f203326270546414244bc2adb78e8` adds the public
`MessagePackWireCodec` production `IWireCodec` adapter over the already qualified
generated DTO mapping and bounded low-level reader/writer. It adds no JSON
fallback, resolver, reflection, typeless behavior, runtime code generation,
compression or game semantics. Unknown types and union implementations continue
to fail closed.

Promotion is supported by the prior G2 desktop bidirectional exchange and Unity
INT-009 evidence commit `ff65dbed550b390c4129f471bf098559be37f5ea`:
the pinned approved corpus passed twice on a physical Android API 31 ARM64 device
under Unity 6000.5.3f1, IL2CPP and high managed stripping, including 34 accepted/
39 rejected diagnostics, 34 TypeScript-consumed and 34 C#-produced exchanges,
24 accepted/8 rejected fingerprints and 14 accepted/18 rejected raw probes.

Focused production-interface tests pass 9/9; the full SDK passes 351/351.
Locked restore/build cover 18 projects with zero warnings/errors; 48 script tests,
14 manifest tests, generator checks and validation pass. A fresh unpublished
23-assembly/three-native-target bundle at the promotion source verifies with
manifest SHA-256 `34febc70a5cc0b1b01f05075b3c26c75e131acc055511c038b033cd40e726893`.
CL-004 remains `in_progress` only for independent completion review; CL-015
remains `in_progress` for independent reproducibility of the promoted artifact
and macOS native execution. Concrete HTTP execution/host composition, HTTPS,
live backend acceptance, G3 and P4 remain open.

## Initial MrSquare outcome/reward decision mirrored — 2026-09-20

`DEC-CONTENT-VALIDATION` / `EXT-GAME-OUTCOME-POLICY` is accepted for one
intentionally unvalidated casual/noncompetitive test tier. The architecture
supports client-side and server-side rewards; rewards are initially client-side.
If G3 requires the bounded server path, it may accept the authenticated client's
completion report under explicit client trust without game/session/replay
validation, then issue exactly one nonpremium `test.coin` through the normal
authorized Wallet/receipt and operation/business-source-idempotency flow.

This is a policy mirror, not SDK or Unity behavior. No runtime, artifact, test,
capability, A01-A13 result or G3 result changed. TypeScript replay and the
existing .NET game DLL are not required; the latter may become an optional future
game-owned validator adapter. `DEC-LEGACY-TRUST` remains unresolved and no
historical/import reward is authorized.

## CL-015 complete bundle checkpoint — 2026-09-20

Replacement packaging source `7c1f9534df3b245c6d0d30f5b56fc183a1f7758a`
replaces the early managed-only output with one unpublished, reproducible
managed/native artifact. It contains all 15 SDK assemblies, the exact eight-DLL
runtime closure for the nine pinned MessagePack packages, three reviewed SQLite
native targets, narrowed generated Unity import metadata, exact notices/hashes,
deterministic explicit-reference managed metadata, a 38-entry P/Invoke inventory,
targeted linker preservation and install/upgrade/uninstall/CL-013 rollback rules.
Duplicate UPM acquisition is forbidden and missing packages/native targets fail
closed. Semantic validation prevents Windows binaries from enabling Linux,
macOS or WSA and enforces isolated macOS/Android CPU/OS/alignment policies.
Ordinary verification rejects dirty-source and skipped-build artifacts; explicit
development inspection remains non-importable. The included codec remains
qualification-only and did not implement production `IWireCodec` at that
checkpoint; the promotion recorded above supersedes that availability limit.

The earlier same-checkout `2357a74...` anchor is superseded: ambient Git-origin
discovery changed Source Link presence, portable PDBs, MVIDs and DLLs between
checkout kinds. Packaging now pins the canonical repository URL, revision,
path map and Source Link while retaining source/debug integrity. Two independent
clean clones with different origin metadata and fresh outputs produced identical
99-entry manifests and complete 100-file artifacts with manifest SHA-256
`a4c36e6c8bbb19821b8f79dbd71622da1a22e85f5ea3b51b01d46a3f38a813b7`.
Fresh verification passed, as did 335/335 .NET tests, 48/48 script tests (26
packaging cases), validation,
18-project locked restore/build with zero warnings
or errors, the 52-task/16-ledger/21-wave manifest and 14/14 manifest tests. See
[CL-015 evidence](implementation/evidence/CL-015/README.md).

CL-015 remains `in_progress`: Android import, stripping, IL2CPP/APK inspection
and physical-device database/codec execution later passed under INT-009, while
the promoted artifact still needs its two-clean-clone reproduction and macOS
native execution. G3 and P4 remain open; no package feed or deployment changed.

## CL-004 desktop wire candidate checkpoint — 2026-09-20; corrected

Source `83caf64596869fc7a1dc35ac6440e21ac97a7d68`, corrected by
`2e46b6f90ed60492d77114c655fb9788d4112e25`, adds the internal
`DesktopQualificationWireCodec`. It deliberately does not implement `IWireCodec`;
production composition remains unavailable pending required AOT acceptance.
Unsupported generic types and union implementations fail closed; named
maps, UUID bin16 RFC order, signed-64 values, opaque-token binary encoding,
absent/null semantics, duplicate-key rejection and body/depth/string/collection/
allocation bounds remain unchanged. No JSON, typeless, reflection or runtime-code
generation fallback was added, and dependency pins/lockfiles are unchanged.

At this checkpoint, focused desktop-candidate tests passed 7/7, serialization tests 171/171, combined
CL-005/CL-010/codec provider-boundary tests 46/46, and the full SDK 335/335. The
existing codec harness self-test passes 73 schema cases, 32 fingerprint cases and
five hostile JSON cases. Locked restore/build cover 18 projects with zero warnings
or errors; validation, 33 script tests, the 52-task/16-ledger/21-wave manifest and
14 manifest tests pass. Packaging still emits the intentional 14-assembly early
bundle and publishes nothing.

This checkpoint preceded the production adapter promotion recorded above.
Concrete HTTP executor/host, live transcript and independent-review evidence
remain open. G3 was not assessed and P4 was not started. See
[CL-004 evidence](implementation/evidence/CL-004/README.md).

## P3 live-slice services and durable envelope — 2026-09-20

CL-101 through CL-108 now have real portable service implementations and real
SQLite coverage for their bounded scopes; their task states remain
`in_progress` and the feature catalog says `unverified`, not implemented.
Unity/package/live-backend evidence is still missing. Profile, progression,
inventory, entitlement, and the narrow semantic-only G3 wallet read path
preserve authoritative revision zero distinctly from absence. Platform SQLite
migration v6 stores zero and the signed-64 maximum exactly.

The v6 command envelope durably binds the exact Accounts installation identity
and captures `clientCreatedAt` once at admission. Admission replay, lease,
restart, retry, and `RemoteCommand` retain that identity and timestamp. Upgrade
from a legacy retained outbox fails atomically because the original values
cannot be reconstructed; an empty legacy stream upgrades only when it matches
one exact Accounts-directory binding. The frozen fingerprint interface now
receives the complete envelope context.

CL-010 exposes explicit consumer-owned transactional hooks for every ordered
private-feed group and for complete snapshot replacement. Raw rows, typed
projection work, overlay rebuild, and cursor publication share one SQLite
transaction and roll back together. This is a seam, not yet complete live-slice
composition.

`CommandPushHttpProvider` now maps exactly one durable profile/gameplay command
to the frozen MessagePack `/sync/push` route, binds response identity, performs
one coordinated authentication refresh, and preserves uncertain delivery for
receipt reconciliation. It does not supply the still-missing canonical
fingerprint or typed private-sync composition.

Integrated Release tests pass 467/467 after the v6, revision corrections, and
command-push adapter;
repository and manifest validation pass. Remaining G3 blockers are production
canonical fingerprint adaptation, borrowed transaction reads, snapshot
boundary/server-time propagation, remote revision-zero and invalidation
handling, durable receipt/feed correlation, complete typed projector
composition, SDK packaging/Unity integration, and the actual live/device
journey. Frozen inventory and entitlement rows are intentionally not expanded
into their richer catalog/origin-dependent services; doing so would fabricate
missing authority data.

## CL-010 private pull/bootstrap increment — 2026-09-19; corrected 2026-09-20

Effective source commit `13c36e9083a6b814896724e949f0f7f1dfd35c29` (initial source
`a9d23c611763939466f33f3817624deffe871470`) implements the bounded Windows x64
storage and portable policy slice against
backend BE-012 source `048d3e427a5c78e4cdefd4a1cb82b0ee07bd695b`
and BE-013/coordination head
`e1e687605e9f5d2fc51aa6be15d804c0f1248e96`. Distinct remote, storage and SQL
models now support resumable all-page bootstrap staging, final atomic
confirmed-view/cursor/Ready installation, ordered fixed-watermark pull pages,
server-cursor-only invisible-prefix advancement, revision-aware view removal
and tombstones, and reset replacement that preserves immutable commands and
rebuilds overlays inside the transaction. The corrective increment rejects
cross-page group replay at or before the durable checkpoint, reconciles active
or retired authoritative stream state with every retained local command before
Ready, replaces expired staging only after a newly authorized start, and makes
storage-port collection snapshots immutable. Frozen stream semantics now require
retired streams to carry no next sequence and require active streams to carry a
positive successor without an exhausted boundary. Native rollback cases cover
foreign-stream rows, pending/in-flight rows at or below server finalization, and
accepted/rejected rows beyond it. Push finalization remains unable to advance
the pull checkpoint.

Focused storage/policy CL-010 tests passed 20/20 and combined CL-007/008/009/010 native SQLite
regressions passed 41/41. The original full SDK suite passed 296/296; locked restore/build
covered 18 projects with zero warnings/errors, architecture validation and 33
script tests passed, and the 52-task/16-ledger/21-wave manifest plus 14 tests
passed. CL-010 remains `in_progress`: live backend transcript,
process-kill/disk/corruption, multi-app shared projection
provider, Unity/AOT/device and independent-review evidence remain unrun.
G3 was not assessed and P4 was not started. See
[CL-010 evidence](implementation/evidence/CL-010/README.md).

Provider source `ebef5ae2e5efc63b897f12b1f5d53e138dfb90de` corrects
the preflight scope and implements the actionable portable semantic provider
over injected `IWireCodec`, `IHttpExecutor` and auth-session ports. It freezes
the authenticated bootstrap GET, bootstrap-page POST and pull POST routes,
explicit MessagePack/diagnostic negotiation, bounded codec invocation, one
coordinated auth refresh, read cancellation/transport mapping, protocol/pre-
envelope errors, final-page cursor handoff and reset mapping. Twenty-one new
scripted boundary tests bring the focused CL-005/CL-010 selection to 53/53 and
the full SDK to 317/317. At that checkpoint they were not live or production-
composition evidence; the later CL-004 section records only a desktop candidate
behind a test-local port adapter. The codec and bounded portable HTTP composition
were later promoted as recorded above; live-host evidence remains unrun.

Hardening source `6462d471d498eb92c4a773cd008e979924afd6dc`
scopes private sync to the expected platform account, shares externally owned
session/account-keyed refresh single-flight across provider instances, binds
protocol error envelopes to the response correlation header, enforces the
requested page/pull byte budget in addition to the global cap, bounds
pre-envelope errors, and rejects reset reasons outside the frozen set. Six new
cases bring the focused selection to 59/59 and the full suite to 323/323.

Migration-registry source `cc329e39d309808150af760d30beb33861ed9fc7`
integrates the unchanged CL-008 outbox schema as fixed version 1 and the additive
CL-010 private-sync schema as version 2 in one immutable scope-owned registry.
Five Windows x64 native cases prove fresh installation, retained-v1 upgrade,
idempotent reopen, checksum-drift rejection, and rollback between schema effects
and journal insertion. Upgrade preserves Ready/outbox state; reopen preserves the
confirmed view, opaque checkpoint and staged bootstrap rows. Focused CL-010
storage/migration tests pass 25/25, the combined CL-007/008/009/010 regression
passes 46/46, and the full suite passes 328/328. Locked restore/build remains
clean across 18 projects; validation, 33 script tests and 14 manifest tests pass.

## CL-009 ordered sender increment — 2026-09-19

Source commit `6ee33493e2919086331918210d01aa26835243ac` implements
the dependency-ready local sender foundation over a narrow remote command port.
Real SQLite transactions lease only the next contiguous sequence, exclude a
competing sender, retain uncertain deliveries in-flight until lease expiry, and
atomically persist accepted or terminally rejected results with
`finalizedThrough`. Known non-delivery/retryable failures release only the same
immutable operation. No push acknowledgement can touch a pull cursor.

At the clean source commit, focused CL-009 tests passed 5/5 and the full SDK
suite passed 276/276. Locked restore/build covered 18 projects with zero
warnings/errors; architecture validation, 33 script tests and the 52-task/
16-ledger/21-wave manifest validation passed. CL-009 remains `in_progress`:
the frozen push/receipt HTTP provider, bounded batching/backoff, blocked-auth
state, real backend/peer fault execution, overlay rejection rebuild,
restart/process-kill/device/Unity evidence and independent A03/A07/A12 review
remain open. G3 was not assessed and P4 was not started.

Receipt-provider source `7955e2d39b164d9a427e64c4c2903c620b114298`
adds the frozen authenticated receipt lookup over injected codec, executor,
auth-session and shared refresh ports. It binds app/account/installation plus
the retained stream/operation/sequence/fingerprint identity, maps accepted and
terminally rejected receipts to immutable terminal bytes, preserves authorized
absence only below the observed finalized watermark, and fails closed on
identity, watermark, media, correlation or envelope drift. Focused provider
tests pass 14/14; the CL-009 sender plus CL-010 provider regression passes 46/46;
architecture/contract validation passes. This is scripted boundary evidence,
not production composition or a live backend transcript. Push HTTP, sender
receipt reconciliation, batching/backoff, blocked-auth persistence, qualified
codec/executor hosting, Unity/device evidence and G3 remain open.

## CL-012 minimal navigation complete — 2026-09-19

Source commit `010d47c07907baf86d8a1c9396f81cd5628d03d2` completes
the pure SDK task with 7/7 focused and 271/271 full tests passing, plus clean
build/planning validation. See
[CL-012 evidence](implementation/evidence/CL-012/README.md).

This does not claim INT-008: no Unity scene, game binding, visual/control
PlayMode or device execution occurred. Those consumer gates remain outstanding,
as do G3 and P4.

## CL-005 semantic provisioning HTTP — 2026-09-19

Source commit `5b7308d5537462623ef7cf6b3b53bcd1312439bf` implements
the portable provisioning provider over injected neutral executor/codec/auth
ports. Focused tests pass 12/12 and the full suite passes 264/264 with zero build
warnings/errors. See [CL-005 evidence](implementation/evidence/CL-005/README.md).

CL-005 remains `in_progress`: provisioning plus portable bootstrap/pull semantic
providers are implemented, while the other frozen operations,
host registration, BE-016 composition, Unity executor and live/device evidence
remain unavailable. The provider does not make incomplete BE-008 provisioning
truthful and does not enable a route, feature, G3 or P4.

The provider boundary is actionable independently of host composition. Its
original scripted tests verify the injected-port contract only. The later CL-004
checkpoint supplies a desktop candidate behind a test-local port adapter; no
the then-unavailable codec or real executor/host was registered. The codec was
later promoted; CL-005 remains partial for its other operations and host work.

## CL-008 atomic projection/sequence/outbox — 2026-09-19

Source commit `4de294141ea7d0f6cf062b8ce736ea0ce89fa112` implements
the qualified Windows x64 atomic admission slice. Eight real-native cases pass,
including actual pre/post-commit process termination and reopen; the full suite
passes 252/252 with zero build warnings/errors. See
[CL-008 evidence](implementation/evidence/CL-008/README.md).

CL-008 remains `in_progress`: disk-full/corruption, terminal result persistence,
authorized server stream rotation, Unity/AOT/device and real game projection
integration remain. No stream is silently reset or relabeled. CL-009/010,
CL-015, INT-005/007/009, G3 and P4 remain outstanding.

## CL-007 real SQLite executor — 2026-09-19

Source commit `91a67c7805ff59bc3b0ca286fddc2a5124291188` implements
the qualified Windows x64 portion of CL-007: one serialized native connection,
scope-owned migrations with checksummed journal markers, callback-borrowed
transactions, cancellation and bounded quiescent disposal, and typed storage
failures. The real-native CL-007 suite passes 8/8 and the full SDK suite passes
244/244; locked restore/build, 33 script tests, manifest validation and 14
manifest tests also pass with zero build warnings/errors. See
[CL-007 evidence](implementation/evidence/CL-007/README.md).

CL-007 remains `in_progress`, not production-accepted: abrupt process-kill,
disk-full/corruption, other native targets, Unity IL2CPP/AOT/stripping and device
tests remain unrun. CL-015 and INT-009 therefore remain outstanding, SQLite is
not yet composed into the consumer, and G3/P4 are not claimed. CL-008 is now
dependency-ready for bounded implementation.

## P3 dependency-ready SDK preflight — 2026-09-19

The coordinated branch remains at `06ba050929117bd48cba5993c28156bbb2c6418c`.
Fresh locked restore and Release build passed with 18 projects and zero
warnings/errors; the full SDK suite passed 236/236. The pinned CL-006 SQLite
candidate also passed its real Windows x64 rollback/commit/reopen probe against
SQLite 3.50.1, preserving signed-64 value `9007199254740993`.

An initial invocation with Windows PowerShell 5 failed the runner's environment
guard because that shell does not define `$IsWindows`; the PowerShell 7
invocation passed. This failed attempt is retained and is not an SQLite test
failure. CL-005 and CL-007 predecessor evidence is present; their manifest
`Await predecessor evidence` reasons are stale scheduling metadata. Both tasks
remain planned: no HTTP or durable SQLite implementation, package, Unity import,
native/device acceptance, feature capability, G3 result or P4 work is claimed.

## G2 verified desktop core - 2026-09-19

G2 post-promotion core exchange passed: 34 cases/24 fingerprints each direction, 32 raw probes in both decoders and 39 diagnostic negatives. Backend 229 tests; SDK 236 tests. Additive receipt schema approved separately with 30 cases/six known answers; its runtime remains unverified. All service features, production/native/device, G3/G4 and product trust/reward/legacy approval remain unavailable or unverified. See [G2 evidence](implementation/evidence/G2-epoch3/README.md). Central state is published last and alone releases P3.

## P2 epoch 2 client delivery — 2026-09-19

G1 is passed and P2 authorized by backend `19aef14eaa6ca428775ec7ff7a4f69cfeab0e20a`.
CL-002 canonical/compatible core, CL-003 DTO delivery and CL-013 early managed
bundle are implemented. CL-004/016 provide a qualification codec, typed mappings,
fingerprints and real producer/consumer CLI; production codec and live-service
modes remain unavailable. See [exact P2 evidence](implementation/evidence/P2/README.md).

Locked Release build and 222 .NET tests pass, alongside 73 schema cases, 32
fingerprint vectors, five hostile JSON cases, six CLI failure/conversion tests and the
architecture/manifest/package checks. An exact Core/Features bundle is imported
into the real consumer at `7375251e5302db308198b7956037c1b33e6edfb6`: Unity
6000.5.3f1 selected EditMode 7/7, Platform 14/14 and GameplayDomain 27/27 pass.
INT-002 remains partial for visual/control PlayMode and tested rollback; the
historical unavailable-editor blocker is superseded, not evidence of device/AOT
acceptance. Existing 123 documentation errors remain unwaived.

34 C# MessagePack fixtures and 24 fingerprints are committed for G2. Actual
TypeScript exchange and minimal live-slice schema review remain unrun. STOPPED AT
G2; P3 is not released. Full A01–A13 production, native/device/provider/live-game
gates and unresolved product policies remain pending. All sixteen product feature
ledgers remain stubbed/not_installed. Changes are published on coordinated
execution/task branches, not main, per CODEX_CLIENT.md.

## G1 epoch 2 schema review — 2026-09-19

G1-R1–R5 are resolved at schema level against canonical
`0109936f0ebf232924cec70adb79c4f790b604bc`, version `0.2.0-core-schema.1`.
The exact reviewed 90-file mirror is installed, with all original policy/primitive
bytes preserved. Client review validates 73 schema cases and 16 independent
boundary cases and supplies 290 property/branch mappings. Review tooling now
enforces new token/stream/distinctness/depth constraints; no DTO or codec is
implemented by this work. See [review evidence](implementation/evidence/CL-003/G1-epoch2/README.md).

CL-003 remains partial for attribute-free DTO delivery during P2; schema/mirror
review is complete. Other tasks/features retain their actual status. Central
backend state releases P2 only after cross-repository publication. Runtime
C#/TS compatibility, A01–A13, native/device, product trust/legacy/reward decisions
and the existing Unity documentation path failures remain unverified/unresolved.
Historical candidate.1 observations below are preserved as dated evidence.

## P1 epoch 1 client remediation — 2026-09-19

CL-003 client review inputs now include 259 field/branch C#/TS mappings, all ten
existing GameplayOutcome fields, and explicit G1-R1–R5 consumer-preservation and
schema acceptance expectations. Read-only review tooling verifies immutable Git
objects and hashes without copying the unapproved mirror. Candidate.1 passes
49 diagnostic schema fixtures and 16 independent client boundary cases; ten
review-tool regression tests pass. These results do not resolve its semantic
gaps. See [epoch 1 review](implementation/evidence/CL-003/epoch1/README.md).

The fetched backend execution SHA remains
`c6aa77054ec63c0ae46fe2113d3add994bf382fb` (P1/epoch 1/blocked), still publishing
candidate.1. Review against the corrected immutable candidate is blocked until
it is published. CL-003 remains blocked; no G1 approval, mirror repin, DTO or P2
codec work occurred. The unchanged consumer SHA is
`bc53723a8eec3521dd7bd8e05eaa726fca29795d`. No Unity write was needed.

Exact command/exit/test evidence is recorded in the epoch-1 verification report.
Architecture/mirror validation, 19 validator tests, manifest validation and 14
manifest tests pass. No runtime source changed; .NET/native/Unity/device/live
backend suites were not rerun. A01–A13, G1–G4, product-policy and existing
consumer environment blockers remain unpassed/unresolved.

## G1 synchronization review — 2026-09-19

SDK remains P1; G1 is blocked on candidate/consumer mapping, stream and
receipt recovery semantics, full fingerprint known-answer vectors and decoded
allocation/compression policy (G1-R1–R5). No runtime behavior, canonical/mirror
pins or product authority changed. BE-002/CL-003 remain partial/blocked; P2 is
not released. A01–A13 and G2–G4 remain unpassed. The coordinator reviewed all
three immutable input heads; original Unity settings edits are preserved.

Review and next work: [G1 client findings](implementation/evidence/CL-003/G1_REVIEW.md).
SDK planning validation and 14 manifest tests passed; architecture validation,
19 validator tests and existing mirror pins passed. Raw results are preserved
in the backend G1 evidence. Older CL-003/runbook codec prerequisites were
corrected to schema-only G1 acceptance; no runtime suite was needed for these
documentation edits. Unity/editor/device and runtime peer exchange were not run.

## Coordinated P1 evidence — 2026-09-19

Run `platform-2026-09-19`, P1 / epoch 0; stop gate G1. Work is on `impl/platform-coordinated-2026-09-19`, not main. The backend state observed at published commit `d65301d39000e6817ca2b737bf9c9e49af0ed3d3` is parallel P1 with every gate pending and no approved artifacts. See the [task ledger](implementation/execution-manifest.json), [client handoff](implementation/coordination/CLIENT_HANDOFF.json), [review record](implementation/evidence/P1/REVIEW.md) and [raw SDK verification](implementation/evidence/P1/results.json).

- CL-001 complete as design/policy: exact current API/direct/transitive dependency inventory, reviewed schema-neutral target signatures and ownership/error rules, narrow feature-contract reference allowances, deny-by-default packages and evidence-aware validator. It rejects known unavailable feature relabels, missing/escaped reports, zero/mismatched tests, package/reference leakage and contract drift. Target transaction/snapshot/command types are explicitly future implementations; operation body/wire details require G1. This is tooling/interface evidence, not feature implementation.
- CL-006: pinned unity-sqlite-net 1.3.2 and native inputs, single SDK-bundle acquisition decision, real Windows x64 rollback/commit/reopen probe with exact signed-64 value `9007199254740993`. Independent rerun passed. Existing databases and dirty acquisition trees are refused. SQLite remains absent from production SDK/Unity imports; other platforms, AOT and device acceptance remain unrun.
- CL-014: implemented BCL-only safe diagnostics with enabled/deferred fields, stable code-defined event/category symbols, explicit scalar metric allowlist, default string/object redaction, immutable captured correlation/generation, bounded copied fields and failure containment. Preserves legacy IAppLog/NullAppLog. Independent review and 12 selected tests pass. No framework, queue or Unity output provider added.
- CL-003 preparation: G1 operation/schema/fingerprint/negative-vector requirements and candidate absence findings committed. No canonical contract, mirror hash, DTO or codec implementation changed. Actual schema approval and runtime compatibility remain unverified.
- INT-001: MrSquare exhaustive caller/save/import/asmdef/GUID/source-linked-project inventory and baseline regression evidence committed on its execution branch while preserving newer main work and original local edits. Inventory subpart is complete; the whole task remains blocked on the pinned Unity editor/effective settings and fresh UE/UP/visual evidence. [Extraction preparation](implementation/evidence/P1/EXTRACTION_PREPARATION.md) records compatibility guards; no SDK import or runtime game integration occurred.

Exact SDK command vectors, exit codes and TRX counters are in `implementation/evidence/P1/results.json`; raw output is alongside it. `rtk proxy python docs/implementation/evidence/verify_p1.py` completed all checks with exit 0:

- Architecture validator passed; Python validator tests 19/19 and manifest tests 14/14 passed. Manifest: 52 tasks, 16 feature ledgers, 21 conservative waves.
- Locked restore and Release build passed with zero errors/warnings. Full .NET suite: 31 executed/passed, 0 failed/skipped; diagnostics filter: 12 executed/passed, 0 failed/skipped.
- Package listing confirms runtime NuGet dependencies remain absent. Local packaging produced 15 NuGet files and 15 managed DLLs, no native bundle; nothing published. Diff check passed.
- Separate `rtk proxy pwsh -NoProfile -File integration/unity/sqlite-qualification/run-desktop-probe.ps1`: exit 0, real Windows native SQLite 3.50.1 / .NET 9.0.4 x64; see CL-006 raw probe output and coordinator review.
- MrSquare source-linked Platform 14/14 and GameplayDomain 27/27 passed; documentation unit tests 57/57 and 29-node architecture validation passed. Unity documentation validator fails on 123 pre-existing stale maintenance move-map paths; baseline validator/map content matches incorporated main. This remains an explicit failure, not a waived pass.

All sixteen product features remain stubbed and MrSquare remains not_installed in the feature ledger. A01–A13 production acceptance remains pending; scoped desktop tooling/redaction/native-probe evidence above does not pass those whole gates. Unity 6000.5.3f1, EditMode/PlayMode/effective settings, IL2CPP/AOT/stripping, physical devices, real peer C#/TS codecs, PostgreSQL/live bootstrap/sync/rewards, provider sandboxes and independent reuse were not run. G1–G4 remain unpassed. No main merge, deployment, Firebase distribution, package publication, production migration, secret change or backend write occurred.

## Historical M0 record

Date: 2026-09-17. Milestone: M0 compatibility/tooling.

## Implemented in M0

- Fifteen `netstandard2.1` project seams and one .NET 9 xUnit project.
- Central build/package configuration, deterministic builds and genuine NuGet lockfile workflow.
- Explicit typed unavailable results/exceptions for all sixteen product features and the HTTP, SQLite, MessagePack and JSON adapters.
- Machine-readable architecture/feature catalogs, validator and validator self-tests.
- Backend-owned `0.1.0-draft` protocol mirror with SHA-256 pins.
- Documentation, scoped agent instructions, CI, PR checklist, security/private-license guidance and local non-publishing package tooling.

## Not implemented or accepted

All product features, identity/provisioning, local durability, synchronization, HTTP, codecs, native SQLite, navigation behavior and presenters remain stubs/planned. A01–A13 production gates remain pending. No Unity import, IL2CPP/AOT/stripping, device, PostgreSQL, Supabase/Deno, Workers, Node/container or cross-language MessagePack runtime test was run. No package was published and the Unity game was not modified.

## M0 validation record

Environment: Windows 10.0.26200, .NET SDK 9.0.203/MSBuild 17.13.20, Python 3.10.4 and Git 2.47.0.windows.2.

- `python scripts/validate.py`: passed; 15 runtime projects, 16 stubbed features, architecture references/module READMEs and 2 pinned contract files.
- `python -m unittest discover -s scripts -p 'test_*.py'`: 7 passed in 0.024 seconds.
- `dotnet restore GamePlatform.sln --use-lock-file`: passed; generated 16 genuine `packages.lock.json` files (15 runtime + test).
- `dotnet restore GamePlatform.sln --locked-mode`: passed with 0 errors and 0 warnings.
- `dotnet build GamePlatform.sln -c Release --no-restore`: passed with 0 errors and 0 warnings. An earlier authoring run failed because the test project inherited C# 9 while its generated xUnit global using required C# 10; the test project now explicitly uses the installed latest language version while runtime projects remain C# 9.
- `dotnet test GamePlatform.sln -c Release --no-build --no-restore`: 19 test cases passed, 0 failed/skipped/warnings, one test project, 989 ms.
- `dotnet list GamePlatform.sln package --include-transitive`: all 15 runtime projects have no NuGet packages; the test project resolved the centrally pinned test packages and lockfile-recorded transitives.
- `python scripts/package-sdk.py`: passed after correcting directory enumeration; produced 15 local `.nupkg` files, 15 managed DLLs and a SHA-256 dependency manifest. It included no native libraries and published nothing.
- Contract SHA-256: policy `3529f3d92ee3e368e91c6d1ca9176022ebe5ae5124b5072cdb76d2fd8474047c`; primitive vectors `d76ccd7aa7e26a4a8b2603061f2709e6391dcc9abfe85bd5739fb58138da30a5`.

## Publication

The private repository is published at `https://github.com/gindemit/game-platform-dotnet` with default branch `main`. The reviewed bootstrap commit is `9c7ceca148a658fffaf30e6b43388184a6140f6d`; a follow-up status-only commit records publication. There were no repository-creation, push, local-filesystem or package-feed permission blockers.
