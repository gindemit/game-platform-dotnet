# Agent instructions — reusable game platform C# SDK

## Every task
Read docs/README.md, docs/DECISIONS.md, docs/IMPLEMENTATION_STATUS.md, and every applicable scoped AGENTS.md along the paths being edited. Explicitly read deeper scopes. For a feature, read its README and docs/FEATURES.md. For protocol work, read contracts/README.md. For dependencies, read docs/DEPENDENCIES.md and architecture.json.

Implement accepted decisions, not superseded research. Code/tests establish what exists; they do not silently repeal accepted requirements. Distinguish implemented, stubbed, planned and unverified states. Passing scaffold checks is not production acceptance.

## Architecture
Keep game rules, grids, plants, levels, scenes and rendering out. No MrSquare.Logic dependency. Keep domain, wire and SQL-row types distinct. Ports belong to their consumer boundary; no universal repository/service bag. Use constructor injection and narrow factories, never global singletons or service locators. Do not add DI/reactive frameworks without an explicit decision.

Account-owned gameplay requires online anonymous provisioning and complete bootstrap on first launch. Preserve issued provider IDs; new distributed IDs use UUIDv7. Revisions, sequences and economic values use checked signed 64-bit semantics. MessagePack is selected; JSON is diagnostic or explicitly negotiated only.

Local projection, sequence and immutable outbox command share one transaction. Server use cases own real transactions. Rewards reuse Wallet/Inventory/Entitlements with operation and business-source uniqueness. One committed private-player feed has app-authorized opaque cursors. A push acknowledgement is not a pull checkpoint. Isolate backend/app/account/view data, caches, workers and late results.

## Workflow and acceptance
Identify the milestone and A01–A13 gates before coding. Keep contracts, features, fixtures and status coherent. Stubs throw typed not-implemented errors or remain explicitly unavailable. Never authenticate, grant value, acknowledge sync, mark bootstrap Ready or simulate a transaction. Fakes exist only in tests/samples.

No secrets, receipts, tokens or personal payloads in source/logs. Never deploy, publish packages, force-push or change visibility incidentally. Preserve unrelated changes. Update docs/IMPLEMENTATION_STATUS.md with exact evidence and unrun gates. Reject layer leakage, snapshot drift, global account access, unsafe full-width numbers and successful fake stubs.
