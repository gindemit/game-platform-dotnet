# Implementation reference

P3 is active; the SDK is implemented in bounded components and imported in Unity. The old M0/no-import baseline is historical. Use [the current entry](../../CODEX_SINGLE_AGENT.md) and [SDK status](../IMPLEMENTATION_STATUS.md), not the archived runbook.

`execution-manifest.json` remains the original task/dependency/feature ledger. Read only the selected task and relevant feature row. `FOUNDATION_TASKS.md`, `FEATURE_TASKS.md`, `interfaces/` and `CONTRACT_HANDOFF.json` retain actual requirements and acceptance IDs; do not read all of them on every task. The bounded backend queue is not a replacement feature ledger.

`coordination/` contains the current handoff and retained synchronization metadata; backend state alone releases gates. `evidence/` contains exact historical/runtime evidence, read only when needed. G1/G2 narratives and superseded startup/status docs are archived. P4 remains later scope; passing component tests does not complete the game journey or full reuse acceptance.
