# Optional configuration client

Portable netstandard2.1/C#9 lifecycle around the unchanged evaluator. Constructor-injected
transport, durable store, clock, randomness, stable owner/subject and bundled selector;
no account/SQLite/Unity/runtime NuGet dependency or constructor IO. Host calls RestoreAsync,
TickAsync while foreground, and GetFeatureAsync at a safe new context boundary. PinAsync
persists a context until explicit ReleasePinAsync; GetPinnedAsync restores it independently
of later selection. FreezeEventContext supplies immutable event facts without emitting an
exposure or automatic telemetry. The client does not establish economic authority.
The ninth constructor dependency is ConfigurationControlMemory under API
rc-client-api-1.1.0. Retain one explicitly app/environment-scoped memory across owner
client replacements; retire/dispose cannot clear accepted volatile denials. A failed
clear never enables sharing clients. Process death after a failed persistence loses
volatile facts; durable public cache remains the restart authority.

Normal metadata and selected graphs validate scope, full structure, decoded lengths,
representation/semantic digests, transitive closure and late owner generation. Cache
activation is published only after a generation-checked durable commit. File storage uses
permanent namespace binding, immutable files, cross-process locking and recoverable atomic
index replacement. Public emergency sequence/deny state has a separate app/environment
namespace and continues polling after selected-owner retirement. A host must dispose the
retired client when its independently owned public polling is handed off.

IConfigurationClock.IsTrusted is an injected host assertion that its current freshness
anchor is verifiable (including across restart), never device/CDN economic time authority.
Unknown/stale new experiment selection uses bundled defaults; intact saved pins remain.
Callers cancel their waits; shared immutable downloads remain bounded and owner retirement
cancels selected work. Native HTTP and WebGL fetch/IndexedDB adapters live under integration,
outside the portable assembly. The ordinary SDK package excludes both optional assemblies.
