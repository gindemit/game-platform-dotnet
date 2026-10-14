# CL-003 G1 epoch 2 schema agreement

Canonical commit: `0109936f0ebf232924cec70adb79c4f790b604bc`, version
`0.2.0-core-schema.1`. Candidate review input:
`a891020d8a33f70c2cdbf5bef9a756992c6e7c1a`; only approval metadata/pins changed
after that schema review. All 90 mirrored contract files and snapshot are exact
canonical bytes. The gate report records the final mirror commit after commit.

Reviewer: Codex /root synchronization session acting as ClientContractOwner and
ConsumerIntegrationOwner as well as backend contract owner. No separate
independent reviewer or product approval claimed. User confirmed both tracks
stopped. Immutable consumer remains `bc53723a8eec3521dd7bd8e05eaa726fca29795d`;
no Unity file was changed and no prior runtime evidence was regenerated.

## Completed review and bounded corrections

[reviewed-schema.json](reviewed-schema.json) contains 290 property/branch
C#/TS/wire/diagnostic mapping rows (no REVIEW_REQUIRED placeholders), 73/73 schema
cases and 16/16 independent boundary cases. Backend's 300-row inventory additionally
enumerates array items and dynamic map values. Together with the referenced
schemas they specify requiredness, union branches, ranges and absent/null behavior.

G1-R1: accept all ten GameplayOutcome fields, preserving semantic IDs, exact
duration/metrics and empty untrusted validation reference. The source uses UTF-16
lengths; canonical bounds count scalars plus UTF-8 limits. Source-valid values are
preserved if admitted; any count/byte mismatch is an explicit local failure retaining
the original outcome. The 256-metric cap intentionally rejects larger legacy
outcomes without truncation. Content version and validation scheme require game
configuration; checkpoint signatures never grant trust or rewards.

G1-R2: accept same-snapshot H and stream F, all-page atomic install, original
pending identities and checked local allocator reconciliation; receipt acceptance
after H does not remove overlays prematurely. Inspect/CAS/retire/register fences
old writers. A repeated historical recovery response must trigger successor
inspection/reconciliation before new admission, never overwrite its allocator
with the original nextSequence=1. Conflicting clones remain quarantined.

G1-R3: accept permanent **full** terminal receipts, so the earlier request for
compacted-result fixtures is superseded by explicit prohibition/negative fixtures.
Retain exact operation/fingerprint/sequence conflicts; absence cannot prove
noncommit. Authenticate every lookup/retry and check recovery idempotency before
token expiry. Account erasure/replay permission is not introduced.

G1-R4: accept the gsc1 specification, full authenticated byte/SHA-256 known answers,
scope/attempt/ordering/null cases, schema normalization and reference checks.
Actual MessagePack compact/full-width token equivalence, duplicate-key/lexical
rejection and C#/TS exchange remain G2 evidence, not claimed here.

G1-R5: accept total node/logical allocation limits, explicit copied-data accounting,
bounded encoded/scratch buffers, identity-only transport compression, opaque
binary/token mappings and typed signed64 fields. Recovery nextSequence's schema
was corrected from a bare diagnostic string constant to positiveInt64 + const 1.
SDK reviewer previously mapped tokens as text; it now maps bin to byte[]/Uint8Array.

## Actual checks and limitations

The initial `.2` run is retained in candidate2-review.json: 69/73 cases passed
because reviewer tooling lacked new custom keyword implementations. The corrected
run passed 73/73 plus 16/16 independent boundary cases. Fifteen review-tool tests
pass, including new token, watermark, rotation and depth regressions. The exact
final cross-repository commands/raw outputs and hashes are recorded in backend
`docs/implementation/evidence/G1-epoch2/commands.json` and its immutable gate report.
Copy tooling adds tests for exact complete copying, bad hashes and path escapes.

Final SDK checks: 15 review-tool tests, 22 architecture/copy-tool tests and 14
manifest tests passed; architecture validator, complete mirror pins and manifest
validator passed. Backend G1 schema/reference checks also passed. Unchanged Unity
documentation map/tests pass, but its validator retains 123 pre-existing stale
paths. Raw outputs, including that failure, remain in the backend evidence.

Schema and mirror delivery are complete; CL-003 attribute-free DTO implementation
remains partial and is authorized P2 work, not silently marked complete. No SDK
runtime code, canonical consumer types, saves/checkpoints or Unity assets changed.
A01–A13 and G2–G4, device/editor/native, full runtime exchange and trust/legacy
decisions remain unpassed/unresolved. Only central state releases P2.

Future field/semantic changes require a reviewed versioned delta and exact mirror
update. No affected implementation may drift from the frozen schema. Public
versioned feature/value expansions still require their later G2 schema review.
