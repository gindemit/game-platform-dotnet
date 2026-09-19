# G1 client schema review requirements

P1 / epoch 0; ClientContractOwner: `/root`. Preparation only: CL-003 remains blocked until G1 approval. No operation DTOs, mirror repin, codec compatibility or backend implementation is approved here.

At backend execution commit `73f9aafe4d09709636da8d1d788f60b76302f4a6`, `git ls-tree -r --name-only <commit> contracts` contained README, policy, snapshot and primitive vectors only. The initial BE-002 branch resolved to that same commit when inspected. This is an absence finding against an immutable revision, not a claim about concurrent uncommitted backend work. Review any later committed candidate in synchronization; do not wait for backend runtime codecs to exist.

Pre-handoff fetch: published execution commit `d65301d39000e6817ca2b737bf9c9e49af0ed3d3` still authorizes P1/epoch0/parallel and contains only those four contract files. Local committed execution revision `c6cdc65a9446502cb7e2c495e6c46881e2b71f93` also had no operation candidate in `contracts/`. No fallback to the planning seed was used. The backend handoff at the fetched revision was still nonquiescent/not_started; the client does not infer backend completion or poll for it.

## Required canonical operations and client mapping

| Boundary | Required schema and semantics | C# responsibility after approval |
| --- | --- | --- |
| Authentication/provision/membership | Provider reference preserved; canonical player/app identity and membership; idempotent provision retry; version/error/auth scope rules; no Ready response before complete bootstrap | Semantic account remote port in Backend.Contracts; attribute-free operation DTO in Wire.Contracts; secure session held outside diagnostics/storage projections |
| Bootstrap begin/page/install | Immutable snapshot identity, authorized app/account/view generation, fixed boundary, bounded pages, complete collection manifest, opaque continuation, terminal marker, expiry and restart behavior | Sync stages every page then atomically installs complete data + checkpoint + Ready; no partial page visibility |
| Ordered command push/results | Authenticated owner/app/stream tuple, immutable operation ID/sequence/version/body, ordered bounded batch, terminal acceptance/rejection, retryable stopping point, changed-body duplicate and expected-sequence recovery | `long` checked sequence; immutable durable outbox; receipts finalized separately from pull cursor |
| Profile/progression intent | Semantic field/content keys, stable business run identity, expected revision, supported metrics/payload version and bounds; generic outcome is evidence, never authority to grant | Feature/domain contract mapped to wire DTO; MrSquare adapter retains mechanics, difficulty, MGD1 checkpoint serialization and validation rules |
| Private pull | Opaque cursor authorized for app/account/view; fixed high watermark, whole committed transaction groups, visibility removal, retention reset, page limits and unknown required kind rejection | Apply an entire group and checkpoint in one transaction; ACK alone never advances checkpoint; unknown required kind leaves cursor unchanged |
| Reset/snapshot recovery | Expired cursor/snapshot and changed visibility errors; consistent complete replacement boundary; server finalized stream watermark and retention/receipt reconciliation rules | Preserve pending immutable commands, stream IDs, local overlays and saved checkpoints; no destructive reset or relabelled attempted stream |
| Version/error envelope | Exact content type/version negotiation, retryability versus finality, unsupported version/kind, auth versus membership denial, bounded diagnostic code, correlation and uncertain mutation result | Typed failure mapping; no fallback to JSON; cancellation or decode failure after send does not prove absence of server effects |

For every field require exact wire name, type, required/optional status, explicit absent/null/default semantics, maximum byte/count/depth bounds, enum/version behavior and target C# type. Reject duplicate map keys at every depth. A generic map or example JSON is not a complete operation schema.

Core review must additionally specify:

- Authenticated client-stream registration/recovery, server expected/finalized sequence, clone/restored-database conflict and conditions for authorized stream rotation. Never rotate or rewrite an already attempted command to hide a gap.
- The terminal bootstrap response's initial opaque pull checkpoint and stream watermark/next-sequence meaning, including exactly which values install atomically with Ready. Distinguish snapshot/page-session expiry from pull-cursor retention/visibility expiry.
- Duplicate command retries returning the original durable terminal result; the same operation ID with a changed semantic body returns a named terminal conflict. Do not infer a new successful execution from matching fingerprints alone.
- The exact idempotent provisioning key, response-loss retry behavior and recovery of the previously issued account/membership; do not create a second anonymous identity on uncertainty.
- Total encoded message and decoded allocation limits as well as per-field/depth/count limits. If compression is allowed, freeze decompressed-size and amplification bounds; otherwise forbid it explicitly.
- Safe uncertain-after-send retry guidance in typed errors, with no private diagnostic values and no claim that cancellation proves rollback.

## Primitive and fingerprint acceptance matrix

- Platform UUID roles map to distinct `Guid` wrappers and RFC-order MessagePack bin16. Preserve issued v4/v7 per role; reject wrong length/variant/version; semantic/provider IDs remain bounded ordinal strings. New distributed IDs use v7; UUID timestamp is not feed order.
- Revisions, sequences and economic values map to checked signed `long` / TS `bigint`; field-specific sign rules apply. Include min/max, zero, overflow, compact integer encodings, >2^53 exact values and rejection of unsafe JS numbers. Diagnostic JSON carries decimal strings. Timestamps alone use integers in `0..253402300799999`.
- Specify the fingerprint version, exact canonical bytes, encoding, property order, arrays, UUID/int64 normalization and absent/null behavior. Include authenticated app/account scope and all immutable semantic command fields; exclude request/attempt identity. Equivalent allowed encodings must hash identically; changed immutable body/scope must not.
- Include semantic positive/negative vectors for all operations above, malformed/nested/oversize fields, unknown required changes, duplicate keys, truncation and unsupported versions. Raw C#/TS bytes and executable peer commands follow at G2, not as a prerequisite to G1.

## Consumer constraints and unresolved decisions

`Assets/Scripts/Platform` remains the authoritative game type family until a coordinated CL-002/INT-002 migration. CL-001 reviews future SDK boundaries; INT-001 supplies refreshed callers, enum/result/ID tests and source-linked tool impacts. No second mutable platform model or universal service bag is allowed.

Existing PlayerPrefs progress/difficulty and `MGD1:` checkpoint/signature formats must remain readable. Installation feedback preferences remain installation-owned. The future game save writer must borrow the same local transaction as projection, sequence and immutable outbox insertion. A separate Save followed by Enqueue cannot satisfy the contract.

DEC-LEGACY-TRUST and DEC-CONTENT-VALIDATION are unresolved. No historical reward replay, automatic account merge, or trusted completion inferred from a client checkpoint signature. G1 can specify generic outcome transport without activating reward policy. Minimal value/receipt schemas are the subsequent G2 review scope.

## Required synchronization output

Record canonical schema/semantic-vector versions, exact backend commit and SHA-256 hashes, field mappings, additive/breaking compatibility policy, actual client/backend review identities/findings and reviewed mirror commit. Runtime implementation references stay null/unverified until G2. Synchronization owns gate approval and state release; this client does not mutate backend contracts/state or approve its own mirror by copying bytes.

Keep A01/A02/A05 pending. This document supplies review inputs, not execution evidence for UUID/codecs/bootstrap. G1 must resolve each missing/ambiguous operation above before releasing affected P2 implementation.
