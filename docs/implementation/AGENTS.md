# Scoped planning and execution guidance

Root AGENTS.md still applies. This folder owns the dependency-ordered task model, evidence links and client-side handoff; it does not supersede accepted architecture or canonical backend schemas.

Read README.md and RUNBOOK.md, then only the selected task card, its manifest row, source sections and frozen interface files. Refresh actual HEADs and local worktree state. Never fabricate a peer task/endpoint/fixture or mark a test passed because the plan exists.

The user assigns PlatformCoordinator, ClientContractOwner, BackendContractOwner, StorageOwner, PackagingOwner, ConsumerIntegrationOwner and independent reviewers. These names are responsibilities, not agents already launched. Only BackendContractOwner edits canonical backend contracts. Only ConsumerIntegrationOwner integrates shared Unity asmdefs, package manifest/lock, scenes, startup and save migration. Isolated feature branches must not share a mutable worktree.

Task implementation updates this folder only through a coordinator-applied evidence/status patch. Every module has separate sdk_status and mrsquare_integration_status. An implemented SDK plus fake tests is not integrated gameplay. Keep immutable historical evidence and exact SDK/backend/consumer revisions. Do not broaden allowlists or delete failing tests to manufacture completion.

Planning branches must remain documentation-only apart from the narrowly scoped manifest checker/tests. Do not merge the Unity planning PR automatically: its main branch triggers Android build/distribution. Runtime implementation belongs in later task branches, with no deployment/publishing/production data changes unless separately authorized.
