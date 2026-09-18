# Codex implementation plan — portable SDK and real MrSquare adoption

Prepared 2026-09-18. **Planning complete is not implementation complete.** The baseline remains M0: sixteen unavailable modules, no SDK import in MrSquare, no real account/sync/economy acceptance.

Start with [RUNBOOK.md](RUNBOOK.md), then select one card using [execution-manifest.json](execution-manifest.json). There are **52 tasks: 16 SDK foundations, 16 feature implementations, and 20 integration/reuse/verification tasks**. INT-016 is intentionally implemented in this SDK repository; its integration card lives with the Unity backlog. The manifest is the single cross-repository task/status ledger. The task books own detailed implementation instructions; the existing roadmap and accepted architecture remain authoritative.

| Artifact | Purpose |
| --- | --- |
| [Audit and traceability](AUDIT.md) | Exact source revisions, actual behavior, gaps, acceptance mapping and limits of this audit |
| [Foundation cards](FOUNDATION_TASKS.md) | CL-001–016: contracts, storage, sync, transport, navigation, packaging and evidence |
| [Feature cards](FEATURE_TASKS.md) | CL-101–116: all sixteen capabilities, each with authority/persistence/tests/consumer requirements |
| [Execution manifest](execution-manifest.json) | Dependency DAG, conservative waves, file/lock ownership, peer gates and separate SDK/Unity statuses |
| [Manifest format](MANIFEST_FORMAT.md) | Schema, status transitions, path ownership and validation rules |
| [Contract handoff](CONTRACT_HANDOFF.json) | Canonical backend pin, draft limitations and explicit peer requests |
| [Runbook and prompts](RUNBOOK.md) | Ready work, safe orchestration, exact commands and review/evidence contracts |
| [Unity task cards](https://github.com/gindemit/MrSquareUnity/blob/docs/client-sdk-integration-plan-2026-09-18/Docs/Architecture/ClientImplementation/TASKS.md) | INT-001–020 and single-owner consumer surfaces |
| [Unity migration matrix](https://github.com/gindemit/MrSquareUnity/blob/docs/client-sdk-integration-plan-2026-09-18/Docs/Architecture/ClientImplementation/MIGRATION.md) | Existing types, consumers, saved formats and bridge-removal gates |
| [Live-game runbook](https://github.com/gindemit/MrSquareUnity/blob/docs/client-sdk-integration-plan-2026-09-18/Docs/Architecture/ClientImplementation/LIVE_SLICE.md) | Actual scenes/hooks, offline/reopen/reconnect proof and blocked real-backend/device gates |

The cross-repository branch links identify this paired planning review, not a runtime package pin. Before implementation, record the exact reviewed plan commit in the launch record. Pin SDK artifacts and canonical backend schemas separately by commit and file digest. Do not consume a moving branch as a compatibility guarantee.

Earliest independent work: **CL-001, CL-006, CL-014, INT-001**. The shortest useful path imports the canonical core early (CL-013/INT-002), then delivers durable account/progression and actual game wiring before broad feature work. INT-010 is the first live-backend/device slice, not an optional M5 footnote. The small second-game proof remains a later stability/reuse gate; existing repository scaffolds are not that proof.

Run `python docs/implementation/validate_manifest.py` and `python -m unittest discover -s docs/implementation -p 'test_*.py'`. These validate this planning model only, not the SDK or Unity runtime.
