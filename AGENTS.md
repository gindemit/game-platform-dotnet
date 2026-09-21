# SDK agent instructions

## Small startup context
For coordinated platform work start at `CODEX_SINGLE_AGENT.md`, then use the backend's `scripts/agent_context.py` to obtain one current card and checkpoint. Read only the selected card, applicable scoped AGENTS.md and named source/tests. Do not recursively load docs, reports, archives, all task rows or peer histories. Use the latest `impl/platform-coordinated-2026-09-19` branch; preserve unrelated work and fast-forward only after checking remote changes.

One implementation writer, no spawned subagents. No main push/merge, package publication, deployment, distribution, production data/secrets, reset or force-push. Keep exact source/artifact identities: documentation-only descendants do not require new DLLs.

## Architecture and correctness
Keep Unity and game rules/grids/levels/presentation out of the portable SDK. No MrSquare.Logic dependency. Separate domain, wire and SQL row types. Ports belong to consumers; constructor injection and narrow factories, no global resolver/singletons or unapproved DI/reactive framework. Read `docs/DECISIONS.md`, `docs/DEPENDENCIES.md`, `architecture.json` or `contracts/README.md` only for the boundary being changed.

First account-owned gameplay requires online anonymous provisioning AND complete bootstrap. Preserve issued provider IDs; new distributed IDs use UUIDv7. Revisions/sequences/economic values use checked signed64. MessagePack is selected. Local game projection, sequence and immutable outbox share a real transaction. Preserve account/app/backend/view isolation and late-result fencing. Push acknowledgements never advance pull cursors. Receipt evidence never double-applies pulled value or fabricates a projection revision.

Do not fake authentication, Ready, confirmed value, transactions, sync acknowledgement or provider success. Fakes are test/sample-only. No secrets/tokens/receipts/personal payloads in logs or source. Owner decision 2026-09-21: no old gameplay PlayerPrefs/checkpoint migration, merge or historical reward replay. Do not confuse this with erasing new SQLite/outbox/identity state.

## Finish
Run focused tests then affected regression. Update only the selected queue row/current checkpoint and applicable existing task evidence. Original task/feature completion still requires its full acceptance; a small step is not G3. Record exact source/runtime, failed and unrun checks. Preserve existing tests and immutable evidence. Stop at review/environment boundaries; do not reread G1/G2 history absent a concrete regression.
