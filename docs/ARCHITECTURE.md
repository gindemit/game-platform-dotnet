# Architecture

The repository has fifteen `netstandard2.1` assemblies. Core owns neutral IDs, failures and results. Diagnostics owns only `IAppLog`. Feature/backend/storage contracts are inward ports. Features, Sync, Navigation and feature presenters contain portable policy. Wire and transport abstractions isolate DTO/byte concerns. HTTP, SQLite, MessagePack and JSON are replaceable adapters.

`architecture.json` is the allowed direct-reference map and `scripts/validate.py` compares it with actual ProjectReferences. Allowed does not mean required. Core/contracts have BCL-only package dependencies. Manual constructor injection, narrow factories and explicit disposal owners are required. Runtime infrastructure may outlive an application; account services and their factories may not. No service locator, ambient account or global mutable registry.

M0 reserves seams and explicit fail-closed stubs. It does not implement storage, codecs, transport, provisioning, sync, navigation policy or product features and does not establish Unity/native compatibility.
