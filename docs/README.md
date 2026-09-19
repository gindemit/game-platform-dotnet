# Documentation authority

Code, generated lockfiles, tests and CI establish the implementation. Accepted decisions define the target but do not prove it exists. This M0 repository is derived from MrSquareUnity `origin/main` commit `5c5233edefc8433d61b6c757fd2022bac98f76ba`; the Unity contract remains authoritative pending extraction.

Start with [ARCHITECTURE](ARCHITECTURE.md), [DECISIONS](DECISIONS.md), [DATA_AND_SYNC](DATA_AND_SYNC.md), [FEATURES](FEATURES.md), [DEPENDENCIES](DEPENDENCIES.md), [IMPLEMENTATION_STATUS](IMPLEMENTATION_STATUS.md), [ROADMAP](ROADMAP.md), [TESTING](TESTING.md), [AGENT_WORKFLOW](AGENT_WORKFLOW.md), and [PROVENANCE](PROVENANCE.md). Protocol work also reads [contracts/README.md](../contracts/README.md); Unity compatibility work reads [integration/unity/EXTRACTION.md](../integration/unity/EXTRACTION.md).

The coordinated P1 implementation is recorded in [implementation status](IMPLEMENTATION_STATUS.md) and the [client handoff](implementation/coordination/CLIENT_HANDOFF.json). [CODEX_CLIENT.md](../CODEX_CLIENT.md) and backend execution state govern phase authorization; M0 evidence below and planning documents do not certify product/runtime integration. MrSquare caller/save/import evidence is committed on its separate execution branch; the SDK remains unimported.

## Detailed execution plan — 2026-09-18

The [implementation index](implementation/README.md) contains a current source audit, traceability/migration handoff, 52 executable task cards, a dependency/ownership manifest, separate SDK and MrSquare completion ledgers, and [Codex launch/review prompts](implementation/RUNBOOK.md). It enriches the M0–M5 roadmap rather than introducing a competing architecture. Its exact audited commits are in [AUDIT.md](implementation/AUDIT.md); they are newer audit anchors, not replacements for historical provenance. Required production/native/Unity/peer evidence remains unrun unless linked explicitly. [VALIDATION.md](implementation/VALIDATION.md) records the checks actually performed on the planning artifacts.
