# CL-001 current API and reference inventory

Inventory revision: `91929801c07c28e40610e7e4f9a25a3e5661406a` (the CL-001 starting tree). This records source reality. The separate foundation-contract document freezes the schema-neutral target signatures; CL-002, CL-003/G1, CL-005, CL-007/008, CL-011 and CL-014 implement or fill their owned details against that baseline.

## Public signatures

Nullable annotations shown below are part of the C# source. All asynchronous members return `Task`; none of these M0 signatures establishes working I/O.

| Assembly/type | Exact current public surface |
| --- | --- |
| Core: `PlatformFailure` | `enum PlatformFailure { None, Offline, Unauthorized, Conflict, NotFound, RateLimited, Invalid, Unavailable, NotImplemented }` |
| Core: `PlatformId` | `PlatformId(string value)`; `string Value { get; }`; `bool IsValid { get; }`; `bool Equals(PlatformId other)`; `override bool Equals(object? obj)`; `override int GetHashCode()`; `override string ToString()` |
| Core: `PlatformResult<T>` | `T? Value { get; }`; `PlatformFailure Failure { get; }`; `string Diagnostic { get; }`; `bool Succeeded { get; }`; `static PlatformResult<T> Success(T value)`; `static PlatformResult<T> Error(PlatformFailure failure, string diagnostic = "")` |
| Core: `PlatformCapabilityUnavailableException` | `PlatformCapabilityUnavailableException(string capability, string reason)`; `string Capability { get; }`; `string Reason { get; }` |
| Diagnostics | `enum AppLogLevel { Trace, Debug, Information, Warning, Error, Critical }`; `IAppLog.Write(AppLogLevel level, string eventName, IReadOnlyDictionary<string, object?> fields, Exception? exception = null)`; `NullAppLog.Instance`; same `Write` implementation |
| Feature contracts | `enum FeatureStatus { Stubbed, Implemented }`; `IFeatureCapability.FeatureId`; `IFeatureCapability.Status`; `Task<PlatformResult<string>> ExecuteUnavailableAsync(CancellationToken cancellationToken)` |
| Backend contracts | `Task<PlatformResult<byte[]>> IBackendGateway.SendAsync(byte[] request, CancellationToken cancellationToken)` |
| Storage contracts | `Task<PlatformResult<string>> IPlatformStore.OpenAccountAsync(PlatformId appId, PlatformId accountId, CancellationToken cancellationToken)` |
| Transport contracts | `Task<byte[]> IHttpExecutor.SendAsync(byte[] request, CancellationToken cancellationToken)`; `byte[] IWireCodec.Encode<T>(T value)`; `T IWireCodec.Decode<T>(byte[] payload)` |
| Wire contracts | `const string ProtocolVersion.Current = "0.1.0-draft"` |
| Feature stubs | Abstract `UnavailableFeature : IFeatureCapability` exposes `FeatureId`, `Status` and `ExecuteUnavailableAsync`; sealed `AccountsFeature`, `ProfilesFeature`, `CatalogFeature`, `InventoryFeature`, `WalletFeature`, `EntitlementsFeature`, `RewardFulfillmentFeature`, `ProgressionFeature`, `QuestsFeature`, `AchievementsFeature`, `StoreFeature`, `PurchasesFeature`, `LeaderboardsFeature`, `TeamsFeature`, `RemoteConfigFeature`, and `InboxFeature` each expose a parameterless constructor and inherit the unavailable surface. |
| Other portable stubs | `UnavailableSyncCoordinator.RunOnceAsync(CancellationToken)`; `NavigationStub.IsAvailable`; `PresentationStub.IsAvailable` |
| Adapter stubs | `UnavailableHttpTransport.EnsureAvailable()`; `UnavailableSqliteStore.Open()`; `UnavailableMessagePackCodec.Encode<T>(T value)`; `UnavailableJsonCodec.Encode<T>(T value)` |

The byte-only `IBackendGateway`, account-opening `IPlatformStore`, generic `IFeatureCapability.ExecuteUnavailableAsync`, and compatibility-shaped Core types are explicitly not the future semantic/session interfaces. Their schema-neutral replacements are frozen in `FOUNDATION_CONTRACTS.md`; only concrete operation body fields remain blocked on G1.

## Actual and allowed project references

“Transitive actual” is the closure of checked-in `ProjectReference` items. Allowed edges are permissions, not evidence that a dependency is currently used.

| Project | Direct actual | Transitive actual | Direct allowed delta |
| --- | --- | --- | --- |
| GamePlatform.Core | — | — | — |
| GamePlatform.Diagnostics.Abstractions | — | — | — |
| GamePlatform.Features.Contracts | Core | Core | none |
| GamePlatform.Backend.Contracts | Core | Core | may additionally reference Features.Contracts |
| GamePlatform.Storage.Abstractions | Core | Core | may additionally reference Features.Contracts |
| GamePlatform.Features | Core, Diagnostics.Abstractions, Features.Contracts, Backend.Contracts, Storage.Abstractions | same five | none |
| GamePlatform.Sync | Core, Diagnostics.Abstractions, Backend.Contracts, Storage.Abstractions | same four | none |
| GamePlatform.Navigation | Core | Core | none |
| GamePlatform.Features.Presentation | Core, Features.Contracts, Navigation | same three | none |
| GamePlatform.Wire.Contracts | — | — | — |
| GamePlatform.Transport.Abstractions | — | — | — |
| GamePlatform.Transport.Http | Core, Backend.Contracts, Transport.Abstractions, Wire.Contracts | same four | none |
| GamePlatform.Storage.Sqlite | Core, Storage.Abstractions | same two | none |
| GamePlatform.Serialization.MessagePack | Core, Transport.Abstractions, Wire.Contracts | same three | none |
| GamePlatform.Serialization.Json | Core, Transport.Abstractions, Wire.Contracts | same three | none |

All runtime projects currently have zero external packages in their project and lock files. `architecture.json` now requires an explicit per-project package allowance before a later qualified dependency can appear.
