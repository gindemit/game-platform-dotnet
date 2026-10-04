# Architecture

The repository has fifteen `netstandard2.1` assemblies. Core owns neutral IDs, failures and results. Diagnostics owns the small `IAppLog` boundary, bounded safe records and injected/null sinks. Feature/backend/storage contracts are inward ports. Features, Sync, Navigation and feature presenters contain portable policy. Wire and transport abstractions isolate DTO/byte concerns. HTTP, SQLite, MessagePack and JSON are replaceable adapters.

`architecture.json` is the allowed direct-reference map and `scripts/validate.py` compares it with actual ProjectReferences. Allowed does not mean required. Core/contracts have BCL-only package dependencies. Manual constructor injection, narrow factories and explicit disposal owners are required. Runtime infrastructure may outlive an application; account services and their factories may not. No service locator, ambient account or global mutable registry.

The implemented portable runtime includes reviewed MessagePack codecs, bounded HTTP providers, SQLite durability, provisioning/bootstrap lifecycle, ordered push/pull synchronization, navigation policy and selected feature services. Compatibility facades and unimplemented feature breadth remain explicitly fail-closed. See [FEATURES.md](FEATURES.md) and [current status](IMPLEMENTATION_STATUS.md); an implemented component does not by itself establish host composition, Unity/device acceptance or broad P4 completion.

The reviewed [interface baseline](implementation/interfaces/README.md), direct-reference inventory and evidence-aware validation remain architectural controls. Runtime packages are deny-by-default. Backend contracts, wire DTOs, SQL rows and feature/domain models stay separate, and consumer-specific composition remains outside portable assemblies.
