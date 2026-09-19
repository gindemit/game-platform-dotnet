# Documentation authority

Code, generated lockfiles, tests and CI establish the implementation. Accepted decisions define the target but do not prove it exists. The historical M0 repository was derived from MrSquareUnity `origin/main` commit `5c5233edefc8433d61b6c757fd2022bac98f76ba`. After the deliberate P2 extraction, GamePlatform.Core and GamePlatform.Features.Contracts own the migrated contracts and are imported by real MrSquare callers; remaining game/legacy responsibilities stay with their existing owner until separately migrated. Historical pre-extraction authority statements must not recreate duplicate declarations.

Start with [ARCHITECTURE](ARCHITECTURE.md), [DECISIONS](DECISIONS.md), [DATA_AND_SYNC](DATA_AND_SYNC.md), [FEATURES](FEATURES.md), [DEPENDENCIES](DEPENDENCIES.md), [IMPLEMENTATION_STATUS](IMPLEMENTATION_STATUS.md), [ROADMAP](ROADMAP.md), [TESTING](TESTING.md), [AGENT_WORKFLOW](AGENT_WORKFLOW.md), and [PROVENANCE](PROVENANCE.md). Protocol work also reads [contracts/README.md](../contracts/README.md); Unity compatibility work reads [integration/unity/EXTRACTION.md](../integration/unity/EXTRACTION.md).

The coordinated P2 delivery is recorded in [implementation status](IMPLEMENTATION_STATUS.md),
[P2 evidence](implementation/evidence/P2/README.md) and the [client handoff](implementation/coordination/CLIENT_HANDOFF.json).
[CODEX_CLIENT.md](../CODEX_CLIENT.md) and backend execution state govern phase authorization.
Core/Features.Contracts are imported into real MrSquare callers with selected
EditMode evidence; product/live/device integration and G2 interoperability remain unverified.

The [2026-09-19 G2 cross-repository source review](https://github.com/gindemit/game-platform-backend/blob/impl/platform-coordinated-2026-09-19/docs/implementation/coordination/G2_REVIEW_2026-09-19.md) reconciles the matching P2 handoffs, records the C# opaque-token defect and evidence-normalization issue, and specifies real peer execution plus additive-schema review. The client's earlier backend-unavailable observation is historical. This is a documentation review, not a fresh runtime test or gate approval. Follow the current execution-branch CODEX_SYNC.md with both tracks stopped; do not restart a completed epoch from its unchanged parallel state.

## Detailed execution plan — 2026-09-18

The [implementation index](implementation/README.md) contains a current source audit, traceability/migration handoff, 52 executable task cards, a dependency/ownership manifest, separate SDK and MrSquare completion ledgers, and [Codex launch/review prompts](implementation/RUNBOOK.md). It enriches the M0–M5 roadmap rather than introducing a competing architecture. Its exact audited commits are in [AUDIT.md](implementation/AUDIT.md); they are newer audit anchors, not replacements for historical provenance. Required production/native/Unity/peer evidence remains unrun unless linked explicitly. [VALIDATION.md](implementation/VALIDATION.md) records the checks actually performed on the planning artifacts.
