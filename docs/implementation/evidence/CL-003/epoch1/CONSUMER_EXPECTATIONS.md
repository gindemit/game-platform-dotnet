# CL-003 epoch 1 consumer preservation and schema acceptance

M1/A, P1/epoch 1; G1 review inputs only. A01, A02 and A05 are the relevant
CL-003 gates; all A01–A13 whole production gates remain unpassed. Client reviewer:
Codex /root, acting as ClientContractOwner and ConsumerIntegrationOwner. No
independent review, product decision, canonical approval or runtime proof is claimed.

Authority is backend state at `c6aa77054ec63c0ae46fe2113d3add994bf382fb`:
CL-003 remediation only. The immutable consumer is MrSquare
`bc53723a8eec3521dd7bd8e05eaa726fca29795d`. The source facts below come from
`Assets/Scripts/Platform/PlatformContracts.cs` (GameplayOutcome, PlatformId,
ContractGuard) and `Assets/Scripts/Garden/PocketBloomOutcomeAdapter.cs` at that
commit. SDK source and Unity remain unchanged.

## R1 — every existing outcome field

The destination names below are client review requests, not a competing schema.
The corrected backend candidate must supply exact canonical names and approve
each disposition. A mismatched bound is an explicit admission failure with the
original local result retained; never truncate, normalize IDs, drop metrics, or
claim successful submission. Generic extensions cannot replace typed metrics.

| Existing field / C# type | Existing source semantics | Required transport disposition / TypeScript |
| --- | --- | --- |
| Session / PlatformId | Nonblank ordinal string, at most 128 UTF-16 code units; no UUID restriction | Preserve semantic session string. Stable business run identity must be separate from operationId. If a new UUID run identity is chosen, persist an explicit session-to-run association before admission, once per run; do not parse or hash an existing session into a UUID. |
| Mode / PlatformId | Same semantic string rules; adapter emits `pocket-bloom` | Preserve as mode string; explicit validation if canonical semantic-key bounds are narrower. |
| Content / PlatformId | Same rules; adapter uses session.LevelId | Preserve as contentId string. Content version is additional game-owned input, not derived from a string or silently defaulted. |
| Difficulty / PlatformId | Required by source constructor; adapter emits `preview` | Preserve difficulty string even if wire schema makes it optional. Absence cannot stand for an existing value. |
| Success / bool | Adapter uses session.IsComplete | Preserve boolean; no local reward authority. |
| Score / long | Entire signed64 range; adapter emits 0 or 1 | Preserve exact signed64 / bigint, decimal string only in diagnostic JSON. Do not infer general nonnegative score from this adapter. |
| DurationTicks / long | 0..Int64.MaxValue; adapter uses session.CurrentTick.Value | Preserve ticks as nonnegative signed64 / bigint. Never substitute rounded seconds or milliseconds. |
| TicksPerSecond / int | 1..1,000,000 | Preserve exact integer / bounded number together with durationTicks; no assumed clock frequency. |
| Metrics / IReadOnlyDictionary<string,long> | Defensive ordinal copy; null becomes empty; at most 1,000 entries; keys nonblank, at most 128 UTF-16 units; full signed64 values | Typed bounded string-to-signed64 map / ReadonlyMap<string,bigint>. Adapter keys are `moves` and `content.activated`. Preserve these plus all admitted keys; reject duplicate decoded keys before materialization. Explicitly retain/reject source results exceeding negotiated wire count/UTF-8 budgets. |
| ValidationReference / string | Null becomes empty, at most 512 UTF-16 units; adapter emits checkpoint signature | Preserve reference as untrusted evidence. Empty input needs explicit omit/unavailable-validation disposition; cannot satisfy a required nonempty validation reference. Validation scheme is additional approved adapter input; never infer trust from the signature. |

The old source permits Unicode, embedded spaces and punctuation in semantic IDs
and metric keys, whereas candidate.1 semanticKey is an ASCII pattern. UTF-16
source length is not UTF-8 byte length or JSON Schema Unicode character count.
This mismatch requires an explicit bounded adapter rejection or a reviewed
canonical widening; it cannot be described as transparent compatibility.

Business uniqueness must be scoped by authenticated backend/app/player and the
stable run identity. Retry changes only attempt metadata. Repeated callbacks for
one run reuse the same durable operation; a changed operation ID must not create
a second business effect. New run IDs follow UUIDv7 only if the canonical role
is a distributed UUID; existing provider/semantic IDs remain unchanged.

## R2 — snapshot, stream and pending-command reconciliation

Review the corrected fields together with their multi-operation protocol. Do
not approve merely because a field called watermark exists.

| Scenario | Required client/schema acceptance expectation |
| --- | --- |
| Provision response lost | Retry for the same authenticated principal/app/installation recovers the issued identity. No second anonymous account or blind stream rotation. |
| Snapshot at feed boundary H | Every page belongs to the same snapshot session/H/view/log epoch. Final initialPullCursor starts strictly after that snapshot boundary, not after the last push acknowledgement. |
| Stream finalization at/beyond H | Distinguish stream finalized sequence from feed revision H. A receipt finalized after H can confirm delivery but cannot remove its overlay until the corresponding projection arrives. Exact relation must be normative. |
| Pending N..M while bootstrap/reset runs | Preserve owner, stream, operation, sequence, canonical payload and game checkpoint. Install complete confirmed projection, pull cursor, initialization marker and reconciliation metadata atomically; rebuild pending overlays without resequencing. |
| Stale provision.nextSequence | Never overwrite a greater durable local allocation or reset an existing stream to 1. Reject conflicting stream claims and enter recovery. Checked signed64 overflow prohibits new admission. |
| Restored/cloned database | Authenticate original scope, inspect prior stream/watermarks and all uncertain receipts before registration. Define authority and ordering when the old stream races recovery. A new stream alone does not resolve old commands. |
| Partial/expired page session | Stage only; no Ready. Discard/restart staging within the same owner while retaining pending work. Distinguish snapshot expiry from pull retention/visibility reset. |

Required schema fixtures: complete final page with all reconciliation fields;
missing or inconsistent snapshot/stream fields; continuation page with a final
cursor; wrong owner/view; unsupported required collection; pending sequence
boundary including Int64.MaxValue. Race scenarios remain normative expectations
at G1 and require actual service/storage tests later, not simulated acceptance.

## R3 — receipt retention, conflicts and recovery

| Server observation | Required client action / frozen semantics |
| --- | --- |
| Full retained accepted or rejected receipt | Persist original terminal result by original operation/stream/sequence; no second execution. A rejection is terminal only where the result contract says so. |
| Receipt compacted to tombstone | Named compacted outcome retains enough immutable identity/fingerprint/finality to prevent replay; if detailed projection is gone, reconcile by snapshot/feed under the original owner. |
| Receipt absent / unknown | Never proof that uncertain work did not commit. Retain the command; use the approved receipt/watermark recovery flow. |
| Same operation, changed body or scope | Named identity/fingerprint conflict; do not overwrite the original receipt, treat changed body as retry, or advance the cursor. Specify whether the conflicting request consumes any sequence. |
| Same sequence, different operation | Named stream identity conflict; original attempted operation retained. No guessed new sequence. |
| Response lost while recovering | Retry the same recovery identity and get the same stream allocation or a named conflict, not another registration. Authorization binds both prior/new streams to the same legitimate scope. |
| Prior stream receives a concurrent write | Explicit fencing/precondition/snapshot ordering must prevent inspection-then-rotation races; client cannot manufacture server quiescence. |

Require distinct valid fixtures for retained, compacted and uncertain outcomes;
invalid contradictory receipt states; immutable identity conflicts; unauthorized
recovery; repeated recovery. Semantic prose must describe retention duration or
permanent tombstone policy, not leave found:false as an ambiguous success path.

## R4 — full authenticated fingerprint expectations

Require exact canonical bytes (hex or base64) and SHA-256 for full commands,
including authenticated backend/app/player scope, stream, installation,
operation ID, sequence, command type/version, created-at and typed payload.
Diagnostic input alone or a digest of the fixture file is insufficient.

Paired vectors must cover: identical command with changed attempt/correlation
(same digest); changed authenticated scope, stream, operation and payload
(different digest); UUID case/byte representation normalization; compact and
full-width permitted signed integers including >2^53 and both signed64 limits;
timestamp endpoints; absent versus allowed explicit null (distinct); prohibited
null/negative-zero/overflow rejection; UTF-8 ordering that differs from UTF-16
ordinal ordering; extensions with nested arrays/objects, no Unicode
normalization and explicit numeric-token rejection under the candidate policy.

The C# mapping must preserve absent/null with a presence bit or explicit union,
not one nullable value. TS uses bigint for full-width values and never passes
them through number. Canonical sorting compares unsigned UTF-8 bytes; default
.NET ordinal or JS string sorting does not establish that order. A schema-level
reference calculation is allowed here; C#/TS MessagePack exchange stays G2.

## R5 — representation and aggregate budgets

The field inventory records diagnostic versus MessagePack representation,
signedness/range, branch-requiredness, schema pointer and proposed C#/TS type.
All conditional/union branches must be considered together. Strings and maps
retain ordinal semantics; unknown fields/kinds follow negotiated policy, never
silent field loss. Body-selected actor/app/user cannot override authenticated
scope. Provider credentials do not become wire/domain projection fields.

Require normative total decoded node and allocation budgets, counting rules
(keys, scalar/container overhead, UTF-8/UTF-16 storage, copied buffers and nested
collections), and enforcement before allocation. Per-array/depth/string limits
alone do not bound aggregate decoded memory. Check exact limit and limit+1,
deep small containers, many small strings, multibyte keys/values and a small
encoded value with excessive expanded allocation.

Compression must be explicitly forbidden, or specify allowed algorithms,
maximum decompressed bytes and amplification plus enforcement before decode.
HTTP Content-Encoding and MessagePack extension/compression wrappers need
explicit treatment; a transport adapter must not decompress unbounded data
before the codec sees it. Schema JSON maxLength is not a UTF-8 byte check.

## Consumer preservation and stop conditions

Keep PlayerPrefs progress/difficulty keys, `MGD1:` checkpoints/signatures and
installation feedback preferences unchanged. No historical outcome replay,
automatic account merge or server reward from a client checkpoint signature.
DEC-LEGACY-TRUST and DEC-CONTENT-VALIDATION remain unresolved. Missing pinned
Unity/editor/device/native evidence and the existing documentation-validator
failure remain recorded. No Unity write or new Unity commit is necessary for
this SDK-hosted review; the handoff retains its actual inspected consumer SHA.

Only a corrected immutable candidate can close these findings. Automated
diagnostic schema passes do not close semantic/race/retention/budget review.
Synchronization owns G1 approval, approved mirror copying and P2 release.
