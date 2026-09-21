# Current client coordination

Use [CODEX_SINGLE_AGENT.md](../../CODEX_SINGLE_AGENT.md) and backend `docs/implementation/coordination/PROTOCOL.md`. One sequential writer; no mandatory parallel restart or whole-history reading. All repositories use `impl/platform-coordinated-2026-09-19`. Backend state alone releases phases/gates; the P3 queue is a next-action cursor, not a second milestone ledger.

Original `execution-manifest.json`, task acceptance IDs, `interfaces/` and `CONTRACT_HANDOFF.json` remain authoritative technical references. Read only the affected sections. Schema approval is not runtime compatibility; implement codecs before joint exchange; BE-025/INT-010 are one shared journey, not circular completed prerequisites. Device-builder preparation may precede full device acceptance. A launch never bypasses gates.

Preserve backend canonical-contract, SDK mirror/artifact and Unity consumer ownership. Update the existing CLIENT_HANDOFF.json current scope with real reachable source/artifact/Unity revisions at a gate boundary. A documentation-only descendant does not invalidate unchanged DLL bytes. Old handoff next-actions are superseded by the current checkpoint and queue, not a reason to redo completed foundations.

Owner policy 2026-09-21: no old gameplay-save migration/import/merge/reward replay. Fresh account-owned SQLite state begins after complete bootstrap. New identity/outbox/state and unrelated installation preferences remain protected.

Stop real writers before synchronization, commit evidence/ledgers/handoffs/report first, publish backend state last. Self-review is not independent review. Missing runtime/device/service evidence is blocked; no fake success, silent schema repin, main merge, publication, deployment or production data changes. G3 releases only later P4 scope; this requested runner stops after its decision. Historical coordination prose is archived, not a second active plan.
