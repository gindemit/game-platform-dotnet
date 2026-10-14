# SDK agent instructions

## Local entry

Cross-repository goals, queues, CL/INT sequencing and work orders are owned by
[game-platform-workspace](https://github.com/gindemit/game-platform-workspace).
Follow [AGENT_ORCHESTRATOR.md](AGENT_ORCHESTRATOR.md). This repository owns portable
SDK requirements, source, tests, packages and [agent/provider.json](agent/provider.json).
Read only the selected ticket, scoped AGENTS and relevant source/tests.

Use main and preserve unrelated work. Do not reset, stash/discard or commit another
session's files. Build/package from owned disposable exact-source copies. Serialize
git indexes and package production; consumer Unity manifest/import changes need a
separate integration lease. Keep canonical runtime source in src, with generated
Git UPM delivery and exact source/payload identities. No automatic publishing,
deployment, production changes or broader product activation.

## Architecture and correctness
Keep Unity and game rules/grids/levels/presentation out of the portable SDK. No MrSquare.Logic dependency. Separate domain, wire and SQL row types. Ports belong to consumers; constructor injection and narrow factories, no global resolver/singletons or unapproved DI/reactive framework. Read decisions/dependencies/architecture/contracts only for changed boundaries. Keep src as the single runtime source; Git UPM is delivery, not a Unity-only fork.

First account-owned gameplay requires online anonymous provisioning AND complete bootstrap. Preserve issued provider IDs; new distributed IDs use UUIDv7. Revisions/sequences/economic values use checked signed64. MessagePack is selected. Local game projection, sequence and immutable outbox share a real transaction. Preserve account/app/backend/view isolation and late-result fencing. Push acknowledgements never advance pull cursors. Receipt evidence never double-applies pulled value or fabricates a projection revision.

Do not fake authentication, Ready, confirmed value, transactions, synchronization acknowledgement or provider success. Fakes are test/sample-only. No secrets/tokens/receipts/private payloads in logs or source. No old gameplay PlayerPrefs/checkpoint migration, merge or historical reward replay; preserve new SQLite/outbox/identity state. Package relocation does not authorize a database reset, new account identity or loss of pending commands.

## Finish
Run focused tests then affected regression, deterministic packaging and exact consumer/platform checks when package payload changes. Return compact exact source/runtime evidence, failures and unrun checks. Required independent review uses a fresh nonauthor child before dependent activation. A card's Stop ends that child assignment, not the workflow. Do not substitute .NET/source/docs success for Unity IL2CPP/native/browser/device evidence.

Original task acceptance and historical P3/G3 remain binding; update original ledgers only at their full scope. P3 is complete at its approved local nonproduction scope, and broad P4 remains held. CAMPAIGN_LOCAL uses its own authorized queue, not a relabelled P3 dispatcher. Stop on genuine unresolved safety/permission/resource limits after independent safe work, leaving a restartable checkpoint and exact push status. Do not reopen economy, social identity, full shell or other excluded product work.
