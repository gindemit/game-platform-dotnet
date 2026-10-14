# Implementation reference

P3 is complete at its approved local nonproduction scope; broad P4 remains held. The SDK is implemented in bounded components and imported in Unity. The old M0/no-import baseline is historical. Use [the shared entry](../../AGENT_ORCHESTRATOR.md) and [SDK status](../IMPLEMENTATION_STATUS.md); the workspace campaign packet owns current execution routing.

`execution-manifest.json` contains only SDK-owned task/evidence fields used by local validation. The full original CL/INT dependency and feature ledger is maintained in [game-platform-workspace](https://github.com/gindemit/game-platform-workspace/blob/main/plans/sdk-integration/execution-manifest.json). `FOUNDATION_TASKS.md`, `FEATURE_TASKS.md`, `interfaces/` and `CONTRACT_HANDOFF.json` retain SDK requirements and acceptance IDs; read only the selected rows.

`coordination/` contains redirects to shared handoffs in the workspace. `evidence/` contains SDK-owned historical/runtime evidence, read only when needed. G1/G2 narratives and superseded startup/status docs are archived. P4 remains later scope; passing component tests does not complete the game journey or full reuse acceptance.
