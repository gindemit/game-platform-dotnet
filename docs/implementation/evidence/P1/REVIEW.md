# P1 independent review record

Review roles are actual agents in this run, not external human approval. Central G1 approval remains the synchronization session's responsibility.

## CL-001

Implementer: `/root/interfaces` (GPT-5.6 Sol). Reviewers: `/root` for tooling/design and `/root/unity` for consumer compatibility. Initial review rejected an implemented-status positive fixture pointing at unavailable production scaffolds and requested exact signatures/reference inventory plus raw reports. The corrected validator rejects known UnavailableFeature-derived modules and correlates named nonzero passing TRX results, while the positive fixture creates concrete source/behavior-test files only in a copied test sandbox. The integrated run passes 19 validator cases and all prior negative cases remain.

The schema-neutral design review required backend isolation, separation of durable owner from transient view/lifetime, explicit command ownership, post-sequence fingerprinting, independent schema/fingerprint versions, no mutable buffer exposure, pre/post-commit cancellation distinctions, snapshot null constraints and pre-account provisioning context. The foundation design resolves those requirements without implementing any new wire DTO, semantic operation body, storage transaction or codec. It is a design/API freeze for dependent tasks, not runtime acceptance of those future APIs.

Final integration review approved the exact foundation contracts from worker commit `31ce9d03099610876e5c5390e2775a1c7a562f9c` (integrated as `99423c9`). CL-001 design/policy is complete; it does not depend on implementation of its own downstream tasks or backend runtime codecs. G1 alone supplies operation-specific details.

Final status transition exposed a pre-existing planning-test assumption that the first task always has no evidence. `test_false_task_completion` now explicitly clears evidence before asserting the precise rejection; the validator was not weakened. Final policy verification passes 19 validator and 14 manifest cases after this correction, with raw command/output records in `final-policy-checks.json`.

## CL-014

Implementer: `/root`. Reviewer: `/root/interfaces` (GPT-5.6 Sol), read-only review of root source and independently executed tests.

Initial review requested changes: a substring denylist could expose numeric secrets under names such as apiKey, sessionId, deviceId or pin. Resolution: exact metric-name allowlist; arbitrary strings remain redacted even under an allowed name. Added unknown-numeric, null-factory/enumerable and duplicate-key tests.

Approved after re-review at SDK commit `3cf9a3c`:

- `SafeAppLog.cs` SHA-256 `adffa7bca274649f0c71994b585ca4b4b18c042037c63395c3750ec8f695a8b2`.
- `SafeAppLogTests.cs` SHA-256 `8c9a4f6f3df9f091b66fef44bf35a3079c79385cbdeef3b14e41ae50712ba643`.
- Command: `rtk dotnet test tests/GamePlatform.Tests/GamePlatform.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~GamePlatform.Tests.Diagnostics.SafeAppLogTests' --logger 'trx;LogFileName=cl014-rereview.trx' --results-directory C:\Temp\gp-cl014-rereview`.
- Exit 0; parsed TRX total/executed/passed 12, failed 0. Final integrated raw TRX is archived alongside P1 verification.

No remaining blocking review findings. Sink/factory calls must return promptly; metadata is code-defined, metrics non-personal, and this SDK boundary does not certify the future Unity output adapter or full A12.

## CL-006

Implementer: `/root/storage` (GPT-5.6 Sol). Reviewer: `/root`, independently inspected acquisition/source/probe and reran the real native probe after integration.

Initial review requested changes: refuse existing database files, use per-run test paths, reject dirty reused candidate source, check each subprocess exit and the actual submodule checkout, and link pinned primary sources. All corrected in integrated commit `c94aeec`. [Coordinator evidence](../CL-006/coordinator-review.json) records the passing independent native probe. Qualification is approved for Windows x64 only; native packaging/notices, other hosts, editor/AOT/device remain separate gates.

## INT-001 and extraction preparation

Implementer: `/root/unity` (GPT-5.6 Sol). Reviewer: `/root`, inspected the actual committed inventory, verifier, source-linked project paths, caller/asmdef/GUID/save mappings and raw reports; independently reran the inventory verifier (exit 0). Requested explicit raw commands/reports and partial status instead of an implied task completion. Original dirty consumer checkout and newer main visuals are preserved.

Inventory subpart is accepted as P1 review input; full INT-001 remains blocked on the pinned Unity editor/effective settings and UE/UP/fresh visual regression evidence. The pre-existing Unity documentation move-map failures remain recorded; they were not fixed through unrelated changes. Consumer owner independently reviewed CL-001 responsibilities and identified the concrete compatibility gaps in [extraction preparation](EXTRACTION_PREPARATION.md).

## CL-003 preparation

Reviewer: `/root/interfaces`. Initial G1 requirements review requested explicit stream recovery, bootstrap initial checkpoint/watermarks, replayed terminal results, provisioning retry key, total/decompression bounds and expiry/uncertain-send distinctions. All six are incorporated; re-reviewed file SHA-256 `5f88baec4b09aea0f451816ea04e9e9450414f622158bb43f393227dbcffeb4f`.

Requirements are approved as client review inputs only. The inspected backend commit had no operation-schema candidate, so actual canonical candidate approval, versions/hashes/mirror and CL-003 completion remain G1 work. No wire/runtime compatibility is inferred.
