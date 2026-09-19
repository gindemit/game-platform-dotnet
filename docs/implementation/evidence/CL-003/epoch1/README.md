# P1 / epoch 1 CL-003 remediation review

Status: **blocked pending the corrected immutable backend candidate**. Both
backend fetches in this session resolved the execution branch to
`c6aa77054ec63c0ae46fe2113d3add994bf382fb`, P1/epoch 1/blocked. That commit still
contains `0.2.0-core-candidate.1`; it is the historical candidate rejected by G1.
No candidate.2 review or resolution of the backend findings is claimed.

The client work adds:

- [259 field/branch mappings and schema results](candidate-review.json), with
  exact backend SHA, artifact digests, C#/TS types, diagnostic/wire distinctions,
  presence/default/null rules, local bounds and original schema pointers.
- [All ten outcome fields and G1-R1–R5 acceptance expectations](CONSUMER_EXPECTATIONS.md),
  grounded in the actual immutable Unity source. This also specifies explicit
  handling of source values outside narrower wire bounds.
- [Read-only candidate review tooling](../review_candidate.py) and
  [ten regression tests](../test_review_candidate.py). The reviewer accepts a
  full SHA, checks every declared digest and refuses unhashed schemas/fixtures;
  it never reads mutable backend working-tree contracts or copies the mirror.
- Sixteen client-authored schema boundary checks independent of the backend's
  49 fixture expectations: signed64 bounds and >2^53, diagnostic numeric-token
  rejection, negative zero/exponents, timestamp endpoints, absent/null profile
  fields and a forged body actor.

## Findings remain explicit

| Finding | Client evidence delivered | Remaining closure requirement |
| --- | --- | --- |
| G1-R1 | Every GameplayOutcome field mapped or explicitly marked absent in candidate.1; semantic session/run/operation separation and bound mismatches specified | Corrected typed fields, exact canonical mapping and approved preserve/omit/defer disposition; no string-extension workaround |
| G1-R2 | Snapshot H, stream watermark/nextSequence, pending reset and restored/clone race acceptance matrix | Corrected canonical fields plus race-safe inspection/recovery ordering and fixtures |
| G1-R3 | Retained/compacted/unknown receipt, changed-body/identity and retryable recovery expectations | Named canonical results/tombstones, retention and authorization/fencing semantics with fixtures |
| G1-R4 | Full authenticated command canonical-byte/digest pair matrix, int64/UUID/timestamp/presence/UTF-8/extension rules | Exact revised known-answer fixtures and independent byte/digest review; no full-command KAT exists in candidate.1 |
| G1-R5 | Diagnostic/wire type inventory and aggregate-allocation/node/compression acceptance cases | Frozen counting/budget/compression policy and boundary fixtures |

The generated rows are a proposed mapping, not DTO definitions. Referenced
objects use proposed schema-derived `*Dto` names; unions and conditional
overlays retain their exact schema locations. Consumers must combine each
definition's branches. Optional absence requires presence tracking separately
from explicit null. Do not infer defaults. Field-local bounds are supplemented
by canonical operation-wide limits, which still have the R5 gaps above.

Candidate.1 also leaves `push.upgradeRequired.minimumProtocolVersion` with a
minimum but no maximum. A TypeScript number mapping needs a frozen safe range
or an explicitly different representation; the proposed safe-integer check is
not proof that this canonical bound exists. UTF-8 byte budgets are distinct
from JSON Schema maxLength and C# UTF-16 length. Canonical UUID acceptance stays
role-specific; an allowed generic UUID schema is not permission to generate
new non-v7 identities.

## Reproduction and evidence limits

The review used installed Python 3.10 and jsonschema 4.17.3 (tooling only; no
package was installed and SDK/Unity runtime dependencies did not change).
Raw output, exact argv, exit codes and tool versions are in
[verification.json](verification.json) and the adjacent `.raw` files.
The complete capture is reproducible with
`rtk proxy python docs/implementation/evidence/CL-003/verify_epoch1.py`.

```powershell
rtk proxy python docs/implementation/evidence/CL-003/review_candidate.py --backend ../game-platform-backend --commit c6aa77054ec63c0ae46fe2113d3add994bf382fb --output docs/implementation/evidence/CL-003/epoch1/candidate-review.json
rtk proxy python -m unittest discover -s docs/implementation/evidence/CL-003 -p test_*.py -v
rtk proxy python docs/implementation/validate_manifest.py
rtk proxy python -m unittest discover -s docs/implementation -p test_*.py
rtk proxy python scripts/validate.py
rtk proxy python -m unittest discover -s scripts -p test_*.py
```

The reviewer implements Draft 2020-12 plus decimal bounds and recursive scalar
checks used by these fixtures. It is not a general implementation of every
`x-*` annotation or a byte-level parser. Hash verification and schema validity
do not certify aggregate decoded allocations, compression, canonical
fingerprints, receipt retention, authorization or concurrency. Those require
the separate semantic review above; runtime proof remains G2/G3.

No runtime source changed, so .NET, native SQLite, Unity/editor/device and live
backend suites were not rerun. All sixteen features remain stubbed and the SDK
is not installed in MrSquare. Product decisions and recorded Unity/native/
device/documentation blockers remain unchanged. A01–A13 and G1–G4 remain
unpassed. Only synchronization can approve a candidate or release P2.

## Next bounded action

Once the corrected candidate is committed, rerun this reviewer with its full
SHA into a **new** evidence directory, review every changed field against
CONSUMER_EXPECTATIONS.md, and independently inspect/hash its complete command
KATs. Record any mismatches rather than weakening the expectations. Preserve
this candidate.1 evidence. Submit a same-phase epoch-1 handoff for the actual
revised candidate before G1 can be reconsidered. No mirror repin or codec work
is authorized by this client handoff.
