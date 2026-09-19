# G1 client and consumer schema review — 2026-09-19

Result: BLOCKED, P1 retained. Reviewer: Codex /root synchronization session,
acting as ClientContractOwner and ConsumerIntegrationOwner; no separate
independent reviewer or product approval is claimed. Operator confirmed both
P1 tracks stopped. Reviewed backend bcd9d2436428cd965fc7739ef84d25a71dacf4dd,
SDK 42fd2b0c5d349da0cfb2d63b8e543f194dd00bb6 and Unity
bc53723a8eec3521dd7bd8e05eaa726fca29795d. Both handoffs agree on run
platform-2026-09-19, P1, epoch 0; all referenced implementations are reachable.

The backend candidate exists now; the earlier candidate-absence finding was
correct only at its historical revision. Candidate 0.2.0-core-candidate.1 has
62 hashed files, 49 schema cases and six small semantic fingerprint vectors.
Schema tests passing does not establish completeness or approve this mirror.

## Required bounded remediation before another G1

- G1-R1: Map every existing GameplayOutcome field explicitly. Session,
  DurationTicks, TicksPerSecond and Metrics have no typed transport mapping.
  The Unity session is a semantic PlatformId, not automatically a UUID. Define
  the stable run/business identity independently from retry operation identity,
  plus deliberate preserve/omit/defer decisions for duration and metrics.
  Generic string-only extensions do not establish that mapping. Reward trust
  and legacy import approval remain separate, unresolved product decisions.
- G1-R2: Specify the bootstrap/stream reconciliation protocol. The final page
  carries initialPullCursor but no stream watermark; provisioning returns
  nextSequence separately. Define exactly how those values relate to snapshot H,
  how local pending sequences survive resets, and how restored/cloned databases
  inspect prior receipts/watermarks before any new-stream registration. Either
  explicit fields or a complete race-safe multi-operation protocol may solve it.
- G1-R3: Define retained terminal-result/tombstone semantics and named outcomes
  for compacted receipts and changed-body/identity conflicts. found:false alone
  must never prove an uncertain operation did not commit. State recovery retry
  idempotency and authorization conditions; do not relabel attempted commands.
- G1-R4: Add complete authenticated command fingerprint known-answer vectors
  with exact canonical bytes and SHA-256, signed64/UUID/timestamp normalization,
  attempt exclusion, changed scope/body, absent/null, UTF-8 key ordering and
  extension cases. Current vectors have zero full commands and zero expected
  digests; the existing validator only hash-checks this semantic file. These are
  schema-level reference fixtures, not a demand for runtime peer codecs at G1.
- G1-R5: Freeze total decoded allocation/node budgets and a compression policy,
  plus wire-vs-diagnostic field types and byte/count limits. Existing encoded
  byte and individual depth/collection caps do not specify those requirements.
- Client follow-up: prepare complete field-by-field C#/TS mappings and schema
  acceptance tests against the corrected immutable candidate. Submit a new
  same-phase handoff at the published epoch. Do not copy/repin unapproved files,
  implement affected DTOs, or start P2 codecs before the next G1 release.

The synchronization session corrected the older CL-003 card and RUNBOOK
runtime prerequisites to match COORDINATION.md. Dependencies and task IDs are
unchanged; CL-003 remains blocked. Exact command results are preserved in backend
docs/implementation/evidence/G1; the immutable gate report is published there
before central state changes. No SDK runtime, mirror or Unity files changed.

All runtime compatibility references remain null, all sixteen features remain
stubbed/not_installed, and A01–A13 remain unpassed. Unity's missing pinned editor,
device/native evidence and existing documentation failures remain explicit
later-environment limitations, not reasons to require codecs at G1.
