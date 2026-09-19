# Codex implementation plan — portable SDK and real MrSquare adoption

Current schema baseline: G1-reviewed `0.2.0-core-schema.1`, exact 90-file mirror;
[epoch 2 review](evidence/CL-003/G1-epoch2/README.md). Fetch central state for P2
release and finish CL-003 DTO delivery before dependent runtime implementation.

Prepared 2026-09-18; coordinated launch instructions updated 2026-09-19. **Planning complete is not implementation complete.** The baseline remains M0: sixteen unavailable modules, no SDK import in MrSquare, no real account/sync/economy acceptance.

## Current entry point

Give the implementation agent [CODEX_CLIENT.md](../../CODEX_CLIENT.md). It owns both the SDK and real MrSquare integration, reads the current phase and stops at a synchronization gate. Run the backend session in parallel, then one backend CODEX_SYNC.md session after both tracks have stopped. Read [COORDINATION.md](COORDINATION.md) before applying older detailed runbook/card gate wording; its explicit schema/runtime and device-ordering amendments are binding.

There are **52 tasks: 16 SDK foundations, 16 feature implementations, and 20 integration/reuse/verification tasks**. INT-016 is intentionally implemented in this SDK repository; its integration card lives with the Unity backlog. [execution-manifest.json](execution-manifest.json) remains the single CL/INT task/status ledger. The backend coordination state controls phase releases only, not task status. Detailed task instructions and accepted architecture remain authoritative subject to the narrow coordination amendments.

| Artifact | Purpose |
| --- | --- |
| [Coordination amendments](COORDINATION.md) | Current stop/resume flow, schema-versus-runtime gates, joint journey and early isolated builder preparation |
| [Client session handoff](coordination/CLIENT_HANDOFF.json) | Durable SDK + Unity commits and evidence for the synchronization session |
| [Audit and traceability](AUDIT.md) | Exact source revisions, actual behavior, gaps, acceptance mapping and limits of this audit |
| [Foundation cards](FOUNDATION_TASKS.md) | CL-001–016: contracts, storage, sync, transport, navigation, packaging and evidence |
| [Feature cards](FEATURE_TASKS.md) | CL-101–116: all sixteen capabilities, each with authority/persistence/tests/consumer requirements |
| [Execution manifest](execution-manifest.json) | Dependency DAG, conservative waves, file/lock ownership, peer gates and separate SDK/Unity statuses |
| [Manifest format](MANIFEST_FORMAT.md) | Schema, status transitions, path ownership and validation rules |
| [Contract handoff](CONTRACT_HANDOFF.json) | Canonical pin, distinct schema/runtime/live stages and mapped actual backend tasks |
| [Runbook and prompts](RUNBOOK.md) | Detailed commands and review/evidence contracts, bounded by the current phase |
| [Unity task cards](https://github.com/gindemit/MrSquareUnity/blob/docs/client-sdk-integration-plan-2026-09-18/Docs/Architecture/ClientImplementation/TASKS.md) | INT-001–020 and single-owner consumer surfaces |
| [Unity migration matrix](https://github.com/gindemit/MrSquareUnity/blob/docs/client-sdk-integration-plan-2026-09-18/Docs/Architecture/ClientImplementation/MIGRATION.md) | Existing types, consumers, saved formats and bridge-removal gates |
| [Live-game runbook](https://github.com/gindemit/MrSquareUnity/blob/docs/client-sdk-integration-plan-2026-09-18/Docs/Architecture/ClientImplementation/LIVE_SLICE.md) | Actual scenes/hooks, offline/reopen/reconnect proof and real-backend/device gates |

Planning branch links identify the instruction set, not runtime package pins. Use the persistent execution branch and central state for subsequent sessions; record exact reviewed plan commits and separately pin schemas/SDK artifacts by commit and digest. Do not treat moving main as a compatibility guarantee or reset newer Unity work to obtain these docs.

Earliest independent work remains **CL-001, CL-006, CL-014, INT-001**, plus schema review preparation for G1. Import the canonical core early (CL-013/INT-002), then deliver durable account/progression and actual game wiring before broad feature work. INT-010 is the G3 live-backend/device milestone, not an optional M5 footnote. The second-game proof remains a later stability/reuse gate.

Run `python docs/implementation/validate_manifest.py` and `python -m unittest discover -s docs/implementation -p 'test_*.py'` when executing the plan. They validate the local planning model only, not cross-session release or SDK/Unity runtime behavior. No runtime success or task completion is claimed by these coordination documentation changes.
