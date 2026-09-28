# SDK agent instructions

## Small startup context
AGENT_ORCHESTRATOR.md routes to the backend shared workflow. **For owner-approved GOAL=CAMPAIGN_LOCAL, read backend docs/work/campaign-2026-09-28/README.md and this repository's docs/implementation/CAMPAIGN_UPM_HANDOFF.md.** Git UPM prerequisites and necessary generic campaign changes are approved for bounded local implementation, not broad P4 release. Do not dispatch closed P3 work or ask the owner to repeat accepted requirements.

Workers read only their ticket, scoped AGENTS and named source/tests. No recursive docs/reports/archives/history loading or nested coordinator. Use main in the three application repositories; do not switch to Future or former implementation branches. Fetch/inspect and fast-forward only safely; preserve unrelated work.

One coordinator and one git integrator. CAMPAIGN_LOCAL permits concurrent implementation only on explicit disjoint file/contract/resource leases, default 2–3 useful children and at most 5, overriding the older one-global-writer/two-child cap only for this goal. Otherwise retain conservative scheduling. Package producers do not edit Unity consumer manifests/imports; that integration is serial. Workers return bounded patches/evidence; coordinator commits/pushes, checks remote reachability and continues automatically. Do not perform concurrent git index operations or shared Unity/device/database work.

No registry publication, deployment, distribution, production data/secrets, reset, force-push or discarding unrelated work. Reviewed Git commits/pushes needed for the owner's package request are authorized. Preserve exact runtime-source/package/artifact identities; documentation-only descendants do not require new DLLs.

## Architecture and correctness
Keep Unity and game rules/grids/levels/presentation out of the portable SDK. No MrSquare.Logic dependency. Separate domain, wire and SQL row types. Ports belong to consumers; constructor injection and narrow factories, no global resolver/singletons or unapproved DI/reactive framework. Read decisions/dependencies/architecture/contracts only for changed boundaries. Keep src as the single runtime source; Git UPM is delivery, not a Unity-only fork.

First account-owned gameplay requires online anonymous provisioning AND complete bootstrap. Preserve issued provider IDs; new distributed IDs use UUIDv7. Revisions/sequences/economic values use checked signed64. MessagePack is selected. Local game projection, sequence and immutable outbox share a real transaction. Preserve account/app/backend/view isolation and late-result fencing. Push acknowledgements never advance pull cursors. Receipt evidence never double-applies pulled value or fabricates a projection revision.

Do not fake authentication, Ready, confirmed value, transactions, synchronization acknowledgement or provider success. Fakes are test/sample-only. No secrets/tokens/receipts/private payloads in logs or source. No old gameplay PlayerPrefs/checkpoint migration, merge or historical reward replay; preserve new SQLite/outbox/identity state. Package relocation does not authorize a database reset, new account identity or loss of pending commands.

## Finish
Run focused tests then affected regression, deterministic packaging and exact consumer/platform checks when package payload changes. Return compact exact source/runtime evidence, failures and unrun checks. Required independent review uses a fresh nonauthor child before dependent activation. A card's Stop ends that child assignment, not the workflow. Do not substitute .NET/source/docs success for Unity IL2CPP/native/browser/device evidence.

Original task acceptance and historical P3/G3 remain binding; update original ledgers only at their full scope. P3 is complete at its approved local nonproduction scope, and broad P4 remains held. CAMPAIGN_LOCAL uses its own authorized queue, not a relabelled P3 dispatcher. Stop on genuine unresolved safety/permission/resource limits after independent safe work, leaving a restartable checkpoint and exact push status. Do not reopen economy, social identity, full shell or other excluded product work.
