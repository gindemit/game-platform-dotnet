# Remote configuration service contract 1.0.0

RC-S0 candidate, 2026-10-14. Backend owns these additive operational contracts.
`service-v1.json` is a strict draft-07 structural schema; `service-vectors-v1.json`
contains exact examples, canonical hashes, rejected shapes and deterministic state
oracles. `service-snapshot.json` pins their exact bytes and this document. SDK copies
of those four files are exact mirrors. This contract requires both the structural
schema and the semantic invariants below; schema validation alone is insufficient.
It neither changes frozen selector/core v1 nor enables a route, deployment or game.

## Representation, scope and checked counters

All public documents use the existing restricted canonical JSON/UTF-8 rules, parser
and per-file bounds from `contract-v1.json` and `README.md`. Reject duplicate decoded
keys, invalid UTF-8/Unicode, unknown fields, unknown tags, unsupported operators,
floats, exponent numbers and negative zero, including unreachable metadata. Every
string/key is at most 4096 decoded UTF-8 bytes; JSON Schema's `maxLength` alone cannot
enforce that. Reject oversized wire input before parsing, and count decoded bytes
before JS/JNI transfer or persistence. Canonicalization never normalizes Unicode.

Every sequence/generation is a JSON **decimal string**, exactly `0` or an ASCII digit
1-9 followed by ASCII digits, with checked range 0..9223372036854775807. Leading
zeros, signs, whitespace, exponent notation, numeric JSON tokens and overflow reject.
Compare through checked C# `long` / TypeScript `bigint`, never float/Number or lexical
comparison. Increment at maximum fails without allocation or side effects. Publication
and control sequences begin at 1; 0 is only an unallocated/cache-generation sentinel.
Owner and store generations can be 0. Other existing selector values/timestamps retain
the core safe-integer range; `issuedAtSeconds`, observations and created times are
nonnegative safe integer Unix seconds. CDN/device time is never economic authority.

New distributed operation/enrollment/instance/pin IDs are lowercase UUIDv7 (valid
variant bits). Existing account IDs and injected owner/subject identifiers remain
host-owned opaque bounded strings. Public documents never contain account/subject IDs.
Cache and enrollment/instance DTOs are private host/local records, never CDN files.

Exact configured app/environment plus supported schema must match on pointer,
manifest, selector, every fetched asset and control. A digest does not override scope.
Public immutable bytes may be shared across owners; selected activations/pins cannot.
An owner generation change retires old selected work and rejects its late completion,
while public emergency polling continues. Restoring another owner's private records
rejects them without exposing their contents; it does not delete game state.

`representationSha` is SHA256 of actual **decoded** UTF-8 JSON bytes, including any
whitespace. ETag and compressed transport bytes are separate identities. Semantic
digest is SHA256 of existing canonical JSON: whole manifest/selector for file refs,
payload only for assets. Lowercase hex digests have exactly 64 characters. Integrity
relies on authenticated HTTPS origins and operator credentials; hashes are neither
signatures nor entitlement/enrollment/grant evidence. No PKI or serializer is added.

## Current pointer, immutable release and compiler compatibility

The schema definitions are exact DTO shapes, with every listed field required; a
nullable field must be present as JSON null when absent. Root document tags are:

| Definition | Exact schema tag |
| --- | --- |
| current | `platform.remote-config.current.v1` |
| manifest | `platform.remote-config.release.v1` |
| control | `platform.remote-config.control.v1` |
| cache | `platform.remote-config.cache-commit.v1` |
| operation | `platform.remote-config.publish-operation.v1` |
| enrollment | `platform.remote-config.enrollment-result.v1` |
| instance | `platform.remote-config.instance-result.v1` |
| lifecycle | `platform.remote-config.lifecycle-result.v1` |

Current pointer has schema/app/environment/sequence/issuedAtSeconds/release/manifest.
`manifest` is `{path,representationSha,semanticSha,decodedBytes}`; its path must be
`manifests/<representationSha>.json`, and bytes/semantic identity must match the ref.
Serve the mutable pointer only at configured `current.json`, revalidated on each check.
The pointer release equals manifest release and selector release. Its publication
sequence is distinct from immutable content/release strings and provider versions.

Immutable service manifest has schema/app/environment/release/issuedAtSeconds/selector/
assets/retained. Selector is the same file-ref shape, with path
`selectors/<representationSha>.json`. An asset descriptor is exactly key/revision/
semanticSha/path/representationSha/decodedBytes/dependencies. Dependencies are exact
core `{key,revision,semanticSha}` refs. Asset path is the unchanged core compiler
`assets/<SHA256(canonical([key,revision]))>.json`; no identifier enters a URL segment.
These paths are relative to one configured scope root, without query/fragment,
percent-encoding, dot segments, alternate origin or config-provided URL. Resolve only
against injected exact origins/prefixes; no path-based host selection or credentials.

The immutable service manifest **does not reinterpret** the scaffold compiler
`platform.remote-config.manifest.v1`. The existing `compileExport` remains a pure,
validated source of selector and asset bytes. A service-layer export consumes its
validated output plus persisted publication metadata; it preserves selector bytes,
asset bytes, representation hashes and asset paths exactly, copies dependency refs
and measured byte lengths, and rewrites only the selector *descriptor* to its
immutable content-addressed path. The old scaffold manifest remains a local compiler
artifact. It must never be served as a service release or silently mutated in place.
The wrapper uses the new `platform.remote-config.release.v1` tag and its own digest;
the current pointer then names that immutable wrapper. There is no self-hash field.
`issuedAtSeconds` for the manifest is frozen at prepare; pointer time is publication
intent time, not a changing HTTP freshness timestamp. Retries preserve all prepared bytes.

All immutable `(app,environment,key,revision)` identities reject changed payload,
dependencies, semantic digest **or representation bytes**. Serving a revised whitespace
representation at an existing URL is replacement too. Duplicate refs/paths reject;
the same key may have multiple revisions with their corresponding identities.
Manifest assets/retained arrays are sorted by ordinal `canonical([key,revision])`;
dependencies and selected closure preserve their compiler/selection order, with unique
keys among one asset's direct dependencies and unique identities within a closure.
Retained refs must resolve to descriptors. All selector default/rule choices and
their exact dependency refs must resolve to matching descriptors; all listed metadata
graphs must be acyclic and complete, even when the player will not fetch their bytes.
No dangling refs or same-key/revision/different-digest aliases are accepted.

A public manifest may omit archival inventory unrelated to its selected selector,
but its descriptors contain all selector-choice roots and transitive dependencies
plus any retained refs it explicitly lists. The **deployed inventory** still contains
every previously published immutable manifest, selector and asset URL with original
bytes, including old pins/experiments/rollback. Do not prune or tombstone v1. Capacity
checks precede upload and fail explicitly rather than silently deleting history.

Parse/validate the entire pointer, manifest and selector first. This makes metadata
available, not a feature. Evaluate the unchanged selector, fetch only that chosen
feature's root and transitive shared graph, verify decoded size, both hashes, scope,
schema, root payload, dependency metadata equality and closure, then atomically commit
before exposing it. A B selection fetches B/shared without A/Store/Packs. One atomic
feature group is represented by one root dependency graph. The existing pure whole-
selector `activate` helper keeps its stronger all-direct-selector-dependencies rule;
the new lifecycle implements selected-graph activation without weakening that helper.

Core per-file bound is 1 MiB (also pointer/manifest/selector/assets); control is 16 KiB.
Manifest has at most 4096 descriptors/retained refs subject to the stricter core byte/
node/depth caps. Chosen closure is at most 64 files and 8 MiB decoded aggregate, including
the feature root but excluding already-validated pointer/manifest/selector metadata.
Declared count/bytes and closure are checked before downloading. Downloaded bytes
must exactly equal `decodedBytes`; aggregate caps apply also to cache-restored graphs.
At most two normal asset requests plus one independent control request are in flight.

## Sequence acceptance, freshness and typed lifecycle results

Each scoped normal/control channel keeps accepted sequence and exact representation
digest together. Lower sequence rejects. Same sequence/same digest is idempotent replay
and may refresh a valid observation after revalidation; same sequence/different digest
is `sequence_conflict` and never refreshes the accepted observation. Higher sequence is
a candidate, not activation proof. Normal metadata advancement does not rewrite an
old feature pin. A changed pointer release can select old immutable content only with
a new sequence. Regular publication cannot lower or clear emergency sequence.

`lifecycle` fields are exactly schema/status/reason/durable/featureKey/origin. Status
`available` means a complete validated committed graph (`activated`, `restored`, `fresh`,
`stale`, `offline`, `unknown_time`, `not_modified` or `replay`); featureKey and origin
are present for selected content. `metadata_only` has no newly exposed graph and
durable=false for that candidate. `bundled` has origin=null and is the host's bundled
default; `ephemeral` explicitly reports nonpersistent identity/storage use and has
durable=false. `preserved` means the previous compatible graph remains available after
a failure; its origin identifies the preserved graph and durable describes that graph,
never the candidate. `rejected` has durable=false and no new origin. A failure does
not manufacture an activation. Absent feature returns `missing_feature` with no origin.

Reason categories include schema/scope/integrity/graph/bounds rejection; unavailable
HTTP/network; cancellation/owner retirement; storage/quota/protected-capacity failure;
sequence replay/lower/conflict; freshness/offline/unknown time; 304 without cached
validator; and explicit ephemeral identity. Every failure keeps an intact prior durable
activation and protected pin. With none, use bundled defaults. Do not claim account
Ready, reward eligibility or exposure based on availability. Emit bounded local events/
diagnostics only; no automatic telemetry. Exposure context freezes at actual use.

Use policy cadence: regular foreground check 15 minutes +/-20%, normal extra checks
at least 60 seconds apart; control foreground check 60 seconds +/-10%; host-driven
startup/resume, no background service. HTTP attempts are at most 5 seconds, refresh
wave at most 20 seconds with one immediate retry after 2-5 seconds, then normal backoff
30/60/120 up to 900 seconds. Control retry schedule is 15/30/60 capped at 60 plus jitter.
Retry connectivity/timeouts/408/429/5xx, bounded Retry-After; no tight retries for
schema/JSON/4xx/missing immutable files. Larger graphs resume another bounded wave.

Soft observation freshness is <=86400 seconds, with trusted anchored elapsed time.
Valid 304 with matching cached bytes/validator updates observation age only; it does
not change issue time, sequence or feature expiry. 304 without cached bytes requires
one bounded unconditional retry and cannot establish freshness. Clock rollback,
implausible jump or unverifiable restart anchor makes freshness unknown. In-process
elapsed time uses monotonic host time; persist injected trusted-origin anchors, while
CDN Date/device clock remain ineligible for economic time. No hard expiry deletes an
intact supported non-economic pin. Stale/unknown unassigned subjects use bundled
defaults without new experiment enrollment; stop optional analytics after 24 hours
without valid normal/control observations and keep all existing denials. Existing
committed outcomes remain replayable after pause/end. Server policy owns actual
occurrence, experiment expiry and economic eligibility; v1 defines none of these.

## Durable cache commit and concurrent writers

`cache` is a private, config-only recoverable commit index: schema/app/environment/
storeGeneration/owner/normal/control/objects/activations/pins. Every field is required;
normal/control/owner can be null. A private commit namespace is **permanently bound**
to exactly `(app,environment,ownerId,subjectId)`; its nonnull owner names that stable
binding. Ownerless activations/pins inherit that immutable namespace, not whichever
identity happens to be current. The adapter's injected namespace key is derived from
that complete tuple and checks embedded app/environment/ownerId/subjectId on every
open/restore/commit. Do not relabel an existing index or pin by replacing owner fields.
When identity changes, retire the old selected client and its in-flight activations,
then open/create the matching new namespace. Preserve the old namespace/index/pins.
An explicit later reopen of the **same stable tuple** may restore its own validated
pins and advance owner.generation for fencing; changing generation alone never changes
ownership. A changed subject with the same ownerId is a distinct namespace too. These
are injected cache identities, with no account SDK or multiowner assignment framework.

An owner=null public namespace is scoped only by app/environment and holds shared
normal/control facts and public objects; its activations and pins must be empty.
Public metadata/bytes may be shared with private namespaces, but selected state cannot.
The accepted emergency sequence/digest and effective denial floor are app/environment
facts that survive every owner/subject switch. Keep their protected public channel
record and raw object; opening an empty private namespace must not reset them. Private
control observations can reference/copy those facts, but cannot lower or clear the
shared channel's accepted floor. A higher valid durable complete control snapshot alone
can clear an emergency restriction. Owner generation fences selected work, independently
of public control polling and sequence persistence.

Objects identify decoded verified bytes by immutable
representation SHA, length and last use. An adapter maps digests to its own private
storage names, never a config-provided filesystem path. Pointer/control bytes and
their digests/accepted sequences are included atomically with observations. For each
nonnull normal/control observation, `objects` must contain the pointer/control
representationSha respectively, naming its **original decoded raw UTF-8 bytes**.
Embedded parsed DTOs are summaries, never a substitute for those raw objects: JSON
whitespace cannot be reconstructed from a DTO. Raw control remains <=16 KiB; raw
pointer remains <=1 MiB. Both objects are protected with the observation/sequence
metadata, included in budget accounting and atomically committed in the same index/
IndexedDB transaction. Manifest/
selector/assets named by observations/activations/pins must exist and revalidate.
Normal time/validator facts correspond to the exact cached pointer bytes. Control
time/validator facts correspond to its exact cached complete document. Unknown anchors
cannot enable new enrollment/analytics. Control metadata and its raw object are
protected from eviction; the accepted normal pointer raw object is likewise protected
while its observation/validator is retained. Restore verifies original raw representation
hashes/lengths, bounded parses them and checks exact DTO equivalence against embedded
observation documents. Missing/mismatched raw bytes invalidate that observation and
its validators; do not reconstruct bytes or accept a 304 for them. Intact feature
pins still follow the separate validated fallback rules, with unknown freshness.

An activation contains exact selection/origin/closure/durable=true. Selection freezes
featureKey, immutable root ref, variant/value/reason/ruleId/bucket, nullable experiment
key/version and direct dependencies. Its root key equals featureKey, origin pins
publication/manifest/selector, closure includes root and every transitive dependency.
`percentage` alone has nonnull experimentKey/version/ruleId/bucket; `rule` has a
nonnull ruleId and null bucket/experiment fields; default/disabled have null ruleId/
bucket/experiment fields. Cache restoration rechecks those associations against the
retained selector/manifest, every object, scope and graph before exposing a feature.
Pins hold immutable activation plus UUIDv7 ID/creation time. End/release is an explicit
host action, never inferred from process exit. Old graphs can coexist with new metadata.

Prepare immutable verified object writes, then atomically replace/transactionally
commit the index **only if expected storeGeneration still matches**, with a checked
increment. Native adapters need staged files, recoverable atomic index replacement
and crash-safe recovery; IndexedDB uses one transaction for named object/index changes.
A losing process/tab reloads the current generation before a bounded retry, rather
than overwriting newer sequence/owner/pins. No platform-provided durability beyond
its actual API is claimed. Report OS/browser eviction and missing bytes explicitly.

Failed write/quota/commit/process death before commit retains old durable graph/index;
only commit success may report candidate durable. A just-committed cancellation can
report actual completed commit; cancellation cannot roll it back. Generation/owner
checks are repeated at activation/commit boundaries, not only at request start.
On restore, corrupt indexes do not expose unvalidated content; recover last complete
commit or bundled defaults, without deleting unrelated game state.

32 MiB is a soft client budget. Evict least-recently-used **unreferenced public** objects
only after protecting current complete fallback, active graph, all persisted pins,
required enrollment refs and control metadata. If protected bytes exceed budget,
preserve them and reject additional content with `protected_capacity`. This cache
has no dependency on game saves, SQLite, auth, backend availability or mobile SDK.
If object storage is shared, compute protected reachability across **all retained
namespace indexes**, including inactive/old owners, plus the app/environment public
index. An object referenced by an old owner's pin is protected even after switching
identity. A missing/unreadable retained index is not proof its objects are unreferenced:
skip shared eviction until ownership/reachability is safely established. Separate
per-owner object stores are equivalent if each preserves its own index/pins and owner
switch never evicts another store. Namespace retirement is not pin release, pruning or
permission to reclaim its objects. Explicit pin release remains a host-owned operation.

## Independent complete control snapshots

Control fields are schema/app/environment/sequence/issuedAtSeconds/restrictions. It
is a complete snapshot. Restriction keys are closed: `analytics.collect`,
`analytics.send`, `analytics.sample`, `configuration.newEnrollment`,
`configuration.newInstance`. No destination name, URL, expression, private ID or
dynamic backend call is accepted. One restriction per key. `deny` requires null
ratePermille; `cap` is valid only for analytics.sample with rate 0..1000. No allow,
grant or rate increase exists. Sampling is the minimum of built-in/consent/normal/
emergency rates. Deny wins. Host/product constraints still govern every permission.

A committed higher complete snapshot can clear an omitted emergency restriction,
but does not override normal policy/consent/platform exclusions. A timeout/404/invalid/
stale document cannot clear anything. Sequence and bytes persist together. On failed
storage a deny may immediately apply in memory (retain its volatile sequence/digest
floor for the process); never report it durable. A clear/enable is not applied until
durable sequence protection commits. A pending volatile deny also prevents a lower
or conflicting document from relaxing that process's restrictions. First-run missing
control is unknown emergency state; optional analytics starts off, ordinary safe
gameplay may proceed. Normal refresh failure cannot stop this independent channel.

At collection and sending time check the effective optional analytics permission;
drop disabled optional queues per destination and keep local drop counts. Gameplay,
purchase and enrollment outbox work retains payload/identity while sending is paused.
In-flight calls may already commit; a deny/cancel never promises recall/rollback.
Control publisher uses a separate provider project/writer/credential path and durable
versioned local/job ledger, without database/auth/backend dependencies. Reserve larger
sequence only after verifying provider state; reconcile any unknown outcome before
the next command. Clearing is explicit audited operator action, not timeout recovery.

## Resumable publication and operation identity

Publish operation fields are fixed in the schema, distinct from current pointer and
provider deployment: operationId, sequence, fingerprint, state, immutable manifest
ref, expected/prepared/observed provider versions, nullable failure reason and update
time. Operation identity is scoped app/environment + UUIDv7. Reusing it with another
fingerprint conflicts before any network action; same input resumes its durable record.
Fingerprint is SHA256(canonical({app,environment,sequence,expectedActiveProviderVersion,
currentRepresentationSha,manifestRepresentationSha,inventory})), where inventory is
the complete retained immutable file set (current.json is fingerprinted separately),
sorted by ordinal path, with exact
{path,representationSha,decodedBytes} entries. No omitted archival URL or changed
representation can hide behind a matching fingerprint. Retried commands supply the
same frozen input; they do not allocate another sequence. Provider tooling limits
are separately checked before external IO.

| State | Durable meaning / permitted next action |
| --- | --- |
| prepared | Reservation, frozen bytes/inventory/fingerprint persisted; no provider version known yet. Upload or reconcile an uncertain previous upload. |
| uploaded | Exact prepared provider version persisted, graph uploaded; not active. Verify expected active version and serialize controlled writer before request. |
| activation_requested | Intent and prepared version persisted before external activation. Success still requires provider verification and durable acknowledgment. |
| active | Provider selected prepared version at 100%, manifest identity matches, DB acknowledgment persisted. |
| reconciliation_needed | Upload/activation/ack outcome uncertain or manual provider drift. Inspect provider and prepared immutable identity; no blind new command/sequence. |
| failed | Known definitive preactivation rejection/capacity/inventory failure with prior active deployment intact; never marks ambiguous network loss failed. |

PreparedProviderVersion is null in prepared, nonnull in uploaded/activation_requested/
active, and nullable in reconciliation_needed/failed depending on reached boundary.
Active requires observedActiveProviderVersion=preparedProviderVersion and no failure
reason. Reconciliation_needed requires outcome_unknown/acknowledgment_lost/provider_drift;
failed requires capacity/provider_rejected/inventory_mismatch. Other ordinary states
have failureReason=null. Updated time is trusted host time, not player time.

Persist preparation/intent before calls and release DB transactions before provider IO.
One controlled writer per app/environment checks expected active deployment; do not
invent provider CAS. External/manual drift blocks activation for reconciliation. After
uncertain upload inspect operation identity/prepared inventory to recover its version,
not generate another prepared release. After uncertain activation, if prepared version
and manifest are active, record external success and acknowledge the same operation;
if known old expected version remains active, resume the uploaded operation after
verification; any other version or mismatch remains reconciliation_needed. A definitive
failed activation leaving old version intact may return uploaded with provider_rejected
reported as an outcome; it must not assert that an unknown call failed or reset sequence.
Queries return durable facts even when previous responses/DB acknowledgments were lost.

Rollback is a new monotonic publication selecting older content with **cumulative**
retained inventory. Never redeploy a historical smaller file set. Client activation
is atomic after selected-graph verification; edge/player cutover is not instantaneous.
Mixed old/new 404 or bad graphs retain LKG and retry on bounded policy. Separate
emergency sequences do not follow normal rollback. No local fake-provider success is
live provider acceptance.

## Trusted experiment enrollment and separate occurrence binding

The TypeScript ports are **internal application ports**, not authenticated routes.
Trusted admission is created by the host's verified identity/published-policy/control/
clock/attribute adapters. It derives app/environment/account/subject, trusted server
now, verified/fresh attributes and confirmed published selector/manifest/sequence.
Public `app` is the configured app key; the host resolves `apps.app_id` and verifies
`app_players(app_id,platform_user_id)` membership. Internal accountId is the canonical
platform_user_id, never an independent client account string. canonicalAppId is a
trusted internal port field only, not part of static JSON or public URL generation.
The host must verify normal/control freshness and trusted feature admission, and load
retained history. Neither caller JSON nor a type cast creates this authority. Later
public transport must resolve these facts from authenticated identity and server
services; do not pass through client account, purchase totals, time, variant, revision,
experiment version, closure, eligibility, occurrence key or grant parameters.

Enrollment operation fingerprint is exactly SHA256(canonical({kind:"enrollment",
app,environment,canonicalAppId,accountId,featureKey})). OperationId is its separately
scoped immutable identity, not a fingerprint field. The app/key/canonical owner fields
are host-validated stable logical request inputs. Exclude subject observations, time,
attributes, selected experiment/version, release/origin, controls, eligibility, variant,
revision and all output; they may change during a legitimate retry. First authenticate
and resolve canonical membership/owner scope, then look up the operation within that
scope and compare stable fingerprint. A matching committed operation returns retained/
existing with its exact stored record before loading/evaluating refreshed definitions
or admission; changed logical
feature/scope/owner cannot turn that operation into another request. Wrong-owner lookup
never reveals another owner's operation/result. An unknown operation has no replay
authority and must follow the current trusted definition and admission checks.

Enrollment uniqueness is app/environment/account/experimentKey/experimentVersion,
derived from the matched verified published percentage outcome. Existing enrollment
wins across operations/devices/attribute changes; its full treatment/origin/closure
remains immutable. After resolving the trusted published experiment identity for an
unknown operation, consult its unique retained enrollment **before** re-evaluating new
eligibility/attributes/admission, so pause/staleness does not erase committed results.
Same operation always uses its committed original experiment/version even after current
policy changes; a genuinely new operation resolves the current published experiment
identity and still reuses any enrollment already committed for that identity. Nonpercentage
selection yields `not_in_experiment` and no enrollment. A paused/stale/unknown-time/
invalid trusted-context new enrollment rejects with no record. Same operation with
different immutable request fingerprint conflicts; wrong owner rejects without
revealing the record. Reused definition identity with inconsistent treatment conflicts.
SQL uniqueness/transactions, not an in-memory lock, settle cross-process races.

Enrollment result has status/reason/nullable record. created/created and retained/
existing require a record; rejected and conflict require null. Conflict reasons are
operation_conflict/definition_conflict; rejected reasons are the remaining listed
admission/owner/nonexperiment reasons. Record binds app/environment/account/subject,
UUIDv7 enrollmentId, experiment identity, exact percentage selection, origin/closure
and enrolledAtSeconds. Record experiment fields must equal selection fields. Creating
an enrollment creates no feature instance, reward, balance or exposure event.

Feature-instance uniqueness is app/environment/account/featureKey/**server-derived
business occurrence key**. It is independent of operation ID. The owning feature
host supplies a trusted occurrence derived from its existing business state; v1
introduces no scheduler, daily cycle or Store policy. A client-chosen new operation
cannot create another genuine occurrence. Instance binding derives its choice from
the retained enrollment when present, otherwise verified published selection; the
caller cannot nominate a revision. An enrollment may span many occurrences, while
a nonexperiment instance has enrollmentId=null. Explicit enrollment IDs are resolved
internally and checked for matching app/environment/account/feature; they never grant.
Percentage selection requires an already committed matching retained enrollment;
null rejects as wrong_enrollment. Nonpercentage selection alone permits null. Binding
never implicitly enrolls. Verify associated enrollment scope/owner/feature, experiment
key/version and its exact frozen selection/origin/closure against retained definitions
and verified bytes. The bound instance copies those exact retained facts, rather than
mixing a new current choice with an old enrollment. Missing/mismatched association
rejects wrong_enrollment without creating an instance; valid prior occurrences still
replay their committed association before refreshed admission/policy checks.

Instance operation fingerprint is exactly SHA256(canonical({kind:"instance",app,
environment,canonicalAppId,accountId,featureKey,occurrenceKey,enrollmentId})). The occurrence
is the stable server business occurrence; enrollmentId is the explicit host association:
null requires nonpercentage selection; nonnull means bind precisely that verified retained
enrollment. There is no implicit association lookup that can change on retry. Exclude
time/attributes/release/origin/control/eligibility/selection/revision and other refreshed
observations/output. Authenticate/resolve scope and stable occurrence first, then compare
the scoped operation fingerprint and return retained/existing with its exact committed
record before current policy/
admission. With a new operation, look up the unique occurrence next: matching association
returns its retained result before new admission; a different enrollmentId (including
null versus nonnull) yields occurrence_conflict and never replaces the prior instance.
Changing occurrence, feature or association under the same operation yields
operation_conflict. Association owner/feature checks occur before any retained result
is disclosed; cross-owner access rejects without leaking records.

Instance results use created/created, retained/existing with record; rejected/conflict
with null. operation_conflict/occurrence_conflict are conflicts; wrong_enrollment/
wrong_owner/admission/freshness/context reasons are rejected. A retained occurrence
returns its complete old origin/closure despite updated config, pause or expiry.
Different operation ID for that same occurrence still returns the same instance.
Creation and its owning feature state bind in the **same existing unit of work**;
the port receives that caller-owned transaction boundary, never commits privately.
An aborted owning transaction leaves no instance/operation acknowledgment. Later
features add their actual transactional operation and product policy; this stage
creates no grants, public routes or Pocket Bloom integration.

## Owning implementation requirements and delivery limits

SDK optional lifecycle supports netstandard2.1/C#9 with injected transport/store/time/
randomness/owner/scheduling; no hidden threads, singleton, account dependence or new
serializer/decompressor. Native file and browser IndexedDB adapters enforce these
records through actual local tests, not production success fakes. Host HTTP decoding
is bounded before storage; browser normal cache revalidation avoids unnecessary
cross-origin custom conditional headers. Native validators are a separately tested
path. Origins use HTTPS except explicit local test harness origins.

Backend real PostgreSQL adapters store exact canonical bytes and immutable refs/
history with least privilege, optimistic drafts, sequence reservation, audit and
caller-owned transactions. Compilation on every update supplies repository-retained
experiment history; omission never means no history. Existing core limitation of one
prior definition per experiment key is handled by orchestration checking every retained
version and passing the relevant latest prior definition; a historical same-version
treatment cannot be changed by first publishing another version. Store complete
history and enforce same-key/version signatures against all history before freeze.

Static-only normal/control projects are separate. Generate JSON MIME, immutable
max-age=31536000, current/control revalidation, public credential-free CORS and required
validator headers; preserve HTTP Content-Encoding/Vary. No SPA fallback, Worker-first
route, R2 or per-player requests. Missing immutable files are 404. Qualify provider
versions/file/header limits when implementing; template output/local HTTP does not
prove real edge CORS/compression/cache/billing/invocation behavior.

Contract candidate validation covers shape/hash/mirror/port compatibility only.
Deterministic state vectors are implementation oracles, not executed service evidence.
Fresh independent review must clear this candidate before dependent implementation.
Local lifecycle/PostgreSQL/provider/browser/Unity tests and their exact receipts belong
to RC-S1..RC-S5; physical Android, real Cloudflare and game integration remain unrun/held.
