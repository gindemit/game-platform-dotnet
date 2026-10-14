# P2 epoch 2 client review and evidence

The client resumed from backend `19aef14eaa6ca428775ec7ff7a4f69cfeab0e20a`:
G1 passed, P2 parallel, G2 pending. Independent audit recomputed canonical snapshot
SHA-256 `7ec4f790219dfcde44308ad86c0fbd8b2527eb3f2116b68541020dd01422bd4b`.
The old P1 candidate.1 handoff was historical, not the release authority.

Implementation: SDK `b8b60bbe1f3478274d5d2ebd9e8c4a739fdff86c`; real Unity
consumer `7375251e5302db308198b7956037c1b33e6edfb6`. CL-013 early bundle source
is immutable task commit `5dfac45875fa4b829e9e04729938eb4a984e1db5`, published
on `impl/cl013-p2`; it contains the same reviewed Core/Features implementations.
The imported bundle manifest hash is
`acfbf1f679b79186cf074ca6e9dc9917835dad86ff7d54af134825eb4ee092be`.
Raw single-message probe modes were subsequently added at
`750f1909e56c1f7362823290e28d0b87c4d32679`, with a successful harness rebuild and
six selected CLI tests. The producer corpus below intentionally retains its
actual earlier producer revision. [CL-013 evidence](../CL-013/README.md) archives
both identical manifests and fresh verification of all 33 files in each bundle;
original build stdout was not retained and is not claimed as independent evidence.

## Delivered scope

- CL-002: distinct UUID roles, RFC bytes/comparison, injected UUIDv7 generation,
  captured ownership, checked numbers and compatibility-preserving generic
  outcome/progress/state contracts. Existing semantic/provider strings and old
  numeric failure meanings remain. Specialized unextracted providers stay in the
  consumer; no universal service bag moves into the SDK.
- CL-003: 64 immutable attribute-free DTO shapes, 12 union interfaces, explicit
  absent/null carriers and schema-to-C# catalog. G1's reviewed mirror is unchanged.
- CL-004 partial: bounded desktop MessagePack qualification, explicit generated
  typed mapping and all canonical fingerprints. Production unavailable seam
  remains; actual peer exchange/AOT acceptance is not claimed.
- CL-013: reproducible 14-assembly managed bundle, symbols, notices, dependency
  closure, exact source/hash inventory and previous-bundle preservation. The
  qualification MessagePack assembly is explicitly excluded pending CL-015.
- CL-016 partial: executable producer/consumer CLI and fail-closed tests. Actual
  service/fault modes are unavailable. See [commands and exchange format](../../../../tests/acceptance/README.md).
- INT-002 partial: two canonical SDK DLLs imported, duplicate moved declarations
  removed, inventoried real callers updated, explicit references and hash verifier.
  Unity report is `Reports/PlatformIntegration/INT-002/` at the consumer commit.

## Actual checks

`rtk proxy python docs/implementation/evidence/verify_p2.py` records exact argv,
exit codes, raw logs and TRX counters in [results.json](results.json).
All commands exit 0: architecture/mirror validation; 33 validator/packaging tests;
14 manifest tests; generated schema/mapper checks; locked restore and Release
build (zero warnings/errors); **222 .NET tests**, zero skipped/failed; harness
**73 schema cases, 32 fingerprint cases and 5 hostile JSON cases**; six CLI
failure/conversion tests; original immutable consumer's 14 source-linked Platform tests.

Actual Unity 6000.5.3f1 is now installed. Baseline selected EditMode 4/4 and final
7/7 pass, including both actual scene assets and assembly uniqueness. Imported
consumer Platform 14/14, GameplayDomain 27/27, documentation 57/57 and 29-node
architecture checks pass. Headless scene loading does not prove visual controls.
The clean tracked snapshot retains the same **123 documentation failures**; the
populated Library additionally exposes a non-UTF8 scanner failure. Neither is waived.

The C# producer emitted [34 real MessagePack cases and 24 fingerprints](csharp-corpus.json)
using the SDK implementation above, backend observation `19aef14...` and Unity
`7375251...`. Corpus SHA-256:
`c4988f5a1fee3eba832cfcfeed9dc2f561f5a17d0321c5db5afc93ad8d2aabc0`.
This is producer evidence, not TypeScript consumption or a compatibility claim.
The producer command is in [producer.json](producer.json). G2 synchronization
must rerun against the actual final backend runtime commit in both directions.

## Review roles and corrections

Three actual sub-agents were reused across contract author, core/consumer owner,
codec author, packaging owner and independent reviewer responsibilities; the
session supports no more than three child threads. Coordinator `/root` owns
shared integration, harness and typed mapper. `/root/consumer_audit` independently
verified G1; `/root/dto` reviewed core and the independently authored codec/mapper.
No self-authored DTO review is described as independent.

Review caught and fixed conditional bootstrap double-conversion, ignored `$ref`
sibling constants, nullable entitlement timestamp representation, scalar-versus-
container depth, actual group bytes and retained typed-mapping allocation budgets.
Regression tests cover each. Typed mapping reserves three retained logical copies;
combined typed encode/diagnostic roundtrip reserves four. Large input can therefore
fail before a single-tree limit; no low-allocation or optimal-capacity claim is made.

## Remaining boundaries

STOP AT G2. Backend runtime publication and TypeScript/C# exchange have not yet
been observed; its fetched handoff remains P1 while P2 work proceeds. Run backend
CODEX_SYNC.md only after both implementation tracks stop. Minimal live-slice
feature schemas still need backend proposal/client review; no mirror delta or P3
release is performed here. CL-004 and CL-016 remain partial until their remaining
acceptance is supplied.

INT-001/002 retain unrun effective-settings, visual/control PlayMode and tested
rollback evidence. Existing PlayMode tests delete normal PlayerPrefs keys, so they
were not run as an isolated acceptance shortcut. Full native bundle/IL2CPP/device,
live backend/bootstrap/sync/rewards, provider sandboxes and second-game reuse are
unverified. A01-A13 whole production gates remain pending. All sixteen product
features remain stubbed/not installed; importing generic contracts is not feature
adoption. CL-005/storage/lifecycle foundations remain future dependency-ready work.
No account readiness, reward authority, product trust decision, deployment,
publication, main merge or distribution is claimed.
