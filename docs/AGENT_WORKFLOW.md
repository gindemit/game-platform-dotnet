# Agent workflow

Read root and scoped guidance, name the milestone and acceptance IDs, inspect existing contracts, and keep one owner for shared protocol/integration. Make one vertical slice, update code/tests/docs/catalogs together, run exact validation, and record evidence in IMPLEMENTATION_STATUS. Cross-repository protocol changes begin in the backend canonical directory and are reviewed/pinned here. Never repin unexplained drift or edit the Unity consumer incidentally.

## Ready-task execution

The [detailed runbook](implementation/RUNBOOK.md) supplies orchestrator, worker, independent reviewer and integration-verifier prompts. Select a card from the [validated manifest](implementation/execution-manifest.json) using `python docs/implementation/validate_manifest.py --ready`, then give the worker only its minimal sources/frozen interfaces and explicit owned paths. Read [the audit](implementation/AUDIT.md) once as coordinator; every feature worker need not ingest all three repositories.

Use isolated branches/worktrees. PlatformCoordinator owns shared SDK policy/status/project surfaces; ClientContractOwner reviews the backend-owned canonical protocol and pinned mirror; StorageOwner owns transaction/migration registration; PackagingOwner owns bundle/dependency pins; ConsumerIntegrationOwner owns shared Unity asmdefs/manifests/scenes/startup/save migration. Independent feature directories can proceed concurrently only after required interfaces freeze and with locks free. A blocked peer schema/API does not justify inventing successful stubs or blocking unrelated pure work.

Workers deliver exact tests/evidence and a coordinator-applied docs/status patch. Reviewers inspect actual code, references and evidence before each merge boundary. Keep SDK and actual MrSquare statuses separate. No publication/deployment/production migration/secret change/force push is authorized by this plan. The Unity planning branch must not be automatically merged to main because main pushes trigger Android distribution.
