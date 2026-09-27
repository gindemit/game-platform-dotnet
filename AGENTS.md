# SDK agent instructions

## Small startup context
For coordinated platform work start at AGENT_ORCHESTRATOR.md, which routes the coordinator to the backend shared Claude/Codex workflow. Workers read only their assigned card/ticket, applicable scoped AGENTS.md and named source/tests; do not launch another coordinator. No recursive docs/reports/archives/ledger/history loading. Use latest `main` for all new feature work; never switch to `Future`, `future/*`, or the former implementation branch; preserve unrelated work and fast-forward only after inspecting remote changes.

One coordinator, bounded subagents, ONE implementation writer across all three repositories. The coordinator commits/pushes and updates shared queue/gates; workers return results and it continues automatically. Push task-owned reviewed changes to `main` after checking the remote head. No package publication, deployment, distribution, production data/secrets, reset or force-push without explicit authorization. Preserve exact source/artifact identities: documentation-only descendants do not require new DLLs.

## Architecture and correctness
Keep Unity and game rules/grids/levels/presentation out of the portable SDK. No MrSquare.Logic dependency. Separate domain, wire and SQL row types. Ports belong to consumers; constructor injection and narrow factories, no global resolver/singletons or unapproved DI/reactive framework. Read docs/DECISIONS.md, docs/DEPENDENCIES.md, architecture.json or contracts/README.md only for the changed boundary.

First account-owned gameplay requires online anonymous provisioning AND complete bootstrap. Preserve issued provider IDs; new distributed IDs use UUIDv7. Revisions/sequences/economic values use checked signed64. MessagePack is selected. Local game projection, sequence and immutable outbox share a real transaction. Preserve account/app/backend/view isolation and late-result fencing. Push acknowledgements never advance pull cursors. Receipt evidence never double-applies pulled value or fabricates a projection revision.

Do not fake authentication, Ready, confirmed value, transactions, sync acknowledgement or provider success. Fakes are test/sample-only. No secrets/tokens/receipts/personal payloads in logs or source. Owner decision 2026-09-21: no old gameplay PlayerPrefs/checkpoint migration, merge or historical reward replay. This does not authorize erasing new SQLite/outbox/identity state.

## Finish
Run focused tests then affected regression. Return compact exact source/runtime evidence, failures and unrun checks. Required independent reviews are spawned automatically in the active provider before dependent activation. A card's Stop boundary returns the worker, not the overall workflow. Full original acceptance remains binding; a small step is not G3. Preserve tests/immutable evidence; do not reread G1/G2 history absent a concrete regression. Default P3 ends at G3 decision and P4 preparation; optional P4 requires the explicit launch goal and genuine release in backend AGENT_RUNTIME.md.
