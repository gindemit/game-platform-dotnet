# Optional configuration lifecycle

RC-S1 adds `GamePlatform.Configuration.Client`, an optional netstandard2.1/C#9 library
around the frozen evaluator. Its only project dependency is
`GamePlatform.Configuration`; it has no runtime NuGet, Unity, account, SQLite or game
dependency. Ordinary SDK delivery still has 15 runtime assemblies and excludes both
configuration projects. Service-v1 owns the network/cache envelopes; core-v1 owns pure
selection. This client changes neither frozen contract.

## Host and API

API version: `rc-client-api-1.1.0`. Construct `ConfigurationClient` with scope, owner,
policy, transport, store, clock, random, bundled selector and required
`ConfigurationControlMemory`, in that order. Construction starts no IO. Retain the
explicit app/environment-bound control memory across owner/subject replacements;
new clients reject a mismatched memory scope. Retire/Dispose cannot reset its floor.
There is no global resolver, hidden polling service or account identity dependency.

Lifecycle and deadline awaits preserve the caller's synchronization context.
Single-threaded hosts must call the client from their owning pumped context and
provide host-driven asynchronous transport, storage and clock completions. The
WebGL host preserves Unity's context through its bridge awaits; asynchronous
completion, cancellation and teardown fencing remain intact. Context-free native
callers retain normal task scheduling. This adds no Unity dependency to the
portable library and starts no background thread or automatic polling service.

Call RestoreAsync at startup, TickAsync while foreground and GetFeatureAsync at the
consumer's safe context boundary. RefreshAsync fetches and commits metadata; metadata
alone does not make a feature available. RefreshControlsAsync independently observes
emergency state. PinAsync persists a verified context, GetPinnedAsync returns it and
ReleasePinAsync explicitly removes that pin. A context carries an internal immutable
app/environment/owner/subject namespace and owner generation; foreign or stale handles
cannot be pinned. Matching stable namespace restoration revalidates graph and selection
before minting current-generation handles. FreezeEventContext returns frozen facts and
does not send telemetry or imply exposure.
The durable private index retains the highest accepted owner generation. Older clients
recheck it on selected reads, writes and CAS retries; actual file/IndexedDB commit
boundaries reject regression. A higher generation with the same stable tuple preserves
explicit old pins without rebinding their owner or subject.

ConfigurationOwner and its subject are host-provided stable configuration identity,
not public authentication credentials. Each private commit namespace is permanently
bound to those identities. Replacing the client opens the matching new namespace and
preserves old pins. Public accepted emergency sequence/digest/denial is app/environment
scoped. Failed deny persistence still restricts every sharing client in memory; failed
clear persistence cannot enable any of them. Only a verified complete durable control
can clear. Process exit after a failed write loses volatile facts; startup uses actual
durable storage rather than claiming the failed write succeeded.

## Validation, scheduling and activation

The reader validates exact shape, scope, decimal signed64 sequences, hashed paths,
metadata ordering, all selector choices against the manifest, and manifest cycles and
dangling references. A selected graph additionally validates exact original bytes,
semantic payload identities and declared transitive closure. An immutable captured
release view carries pointer, manifest, selector, freshness observation and metadata
bytes through every asynchronous download and commit. Concurrent refresh cannot mix
an old selection with a new origin. Admission restrictions are rechecked after downloads
and at the durable commit boundary. Old pins survive a later denial.
Freshness is checked again after graph/store waits and before exposing any new experiment
context. Retained selection validation associates disabled/enabled state with its reason.
Missing normal metadata cannot bypass admission restrictions through an old activation;
GetPinnedAsync remains the explicit path for retained business pins, fenced by retirement.

| Boundary | Default |
| --- | --- |
| Ordinary HTTP | Maximum two requests; five seconds each |
| Ordinary wave | Twenty seconds across metadata/downloads/waits; one immediate retry |
| Immediate retry | Two to five seconds jitter; bounded Retry-After up to 900 seconds |
| Deferred normal poll | Fifteen minutes with jitter; failures back off 30 to 900 seconds |
| Emergency poll | Separate single request; sixty seconds with jitter |
| Emergency failure | Fifteen, thirty, then sixty seconds with jitter; bounded Retry-After |
| Selected graph | At most 64 files and 8 MiB decoded |
| Per document/control | At most 1 MiB / 16 KiB decoded |
| Cache | 32 MiB soft target; retained graph and pins are protected |

The injected clock's DelayAsync drives deadlines and waits; portable code uses no
managed Timer or Task.Run. Caller cancellation cancels its shared-work wait; owner
retirement cancels selected work. No partial graph is activated. Long Retry-After defers
the next scheduled attempt instead of exceeding a wave or retrying too early.
Transport errors, timeouts, 408, 429 and 5xx are retryable; invalid metadata or missing
immutable content is not tight-looped. Background polling stops when foreground=false.

Freshness uses verified host time plus monotonic elapsed time. Unknown anchors, rollback
and stale metadata prevent new experiment admissions and optional analytics permission.
Saved intact pins remain usable. Device/CDN freshness time is not economic authority.
Optional analytics permission accepts only analytics.collect and analytics.send and
intersects explicit consent, built-in limits, normal policy and durable fresh controls.
Sampling validates each input within 0..1000 and takes the minimum permitted rate.
Unknown host keys cannot imply permission. No analytics queue or sender is installed.

## Durable stores and target adapters

FileConfigurationStore stores exact hash-addressed original pointer/control and selected
metadata/payload bytes in separate owner namespace directories. A cross-process exclusive
lease serializes generation CAS. Objects flush before atomic index replacement; a prior
validated index and its reachable objects remain recoverable. Lost acknowledgement is
reconciled against exact persisted generation/index bytes. Cancellation before replacement,
quota/unavailability or integrity failure cannot report a new durable activation. Index
reachability protects current and backup objects; cleanup never visits another namespace.
Backup protection can temporarily exceed the soft physical footprint target.

UnityConfigurationHost is a separately owned dedicated GameObject with exactly one
MonoBehaviour. Initialize rejects duplicate receiver components; dispatch rejects a
renamed or shared receiver. Each instance has independent request IDs, native object or
browser handle and unique callback target. Narrow Preserve attributes retain only the
two string callbacks. Update marshals platform calls and fulfills host-clock delays.
OnDestroy removes owned pending work under lock and disposes registrations outside the
lock. Registration creation racing teardown relinquishes registrations safely.

Android uses the owned ConfigurationHttp Java implementation, normal TLS, no redirects
or ambient account cookies, separate normal/control capacity and decoded transfer bounds.
Editor uses BCL HTTP; native storage uses FileConfigurationStore. WebGL fetch uses
credentials=omit, CORS, redirect=error, decoded streams and normal browser HTTP cache
revalidation. The persisted ETag is forwarded for association without a custom
cross-origin If-None-Match header or preflight. A network 304 may surface as cached 200.
An unusable application-visible 304 requires one bounded unconditional retry.

WebGL IndexedDB atomically commits verified objects plus index in one readwrite
transaction with strict durability and exact BigInt signed64 CAS. Per-namespace keys
protect other owners and current/backup indexes. Disposal fences queued work and closes
a database that finishes opening after disposal. API denial or browser/OS eviction
requires fallback; API success does not promise hardware durability.

`integration/RemoteConfig/stage-client.py` accepts hash-pinned inspected core/client
DLLs and copies only those two assemblies plus owned host sources into an empty isolated
package. It rejects unexpected references, wrong framework, changed DLL identity and
existing output. No build/import/deploy or game mutation occurs during staging.

## Evidence boundary

At the 2026-10-14 repair checkpoint, task source `2aeef858` commits the reviewed
caller-context overlay on published439272a. Validation uses immutableSDK495
plus the exact reviewed overlay. Owning local gates pass
75 Python and 998 .NET tests, with nine pre-existing joined-recovery skips.
Ordinary local packaging has 23 managed and three native payloads and excludes
both configuration projects; historical CI154/dev.154 provenance remains
historical and does not establish a new game pin or delivered configuration DLL.

The repaired target candidate is separately frozen SDK557 plus exactly the
three reviewed ConfigurationClient.cs/Policies.cs/UnityConfigurationHost.cs
overlays. Independent reproduction matches core DLL SHA256
`26e9846a4401b3f851852062e1c8a06b578a757bf4c7efda49d757f633eb2c45` and client DLL
SHA256 `f060a37447b8fa0c2c2a449957ee60c19ee5b648342a98e843441d82f7123b46`.
All eight isolated matched targets pass; actual Android growth is 482,256 bytes
against the 262,144-byte objective (exceeded), while WebGL growth is 134,133
bytes (within objective). Actual native joined runtime5 is accepted at its local
scope. Actual C# browser2 lifecycle, durable cached state after actual reload and two-host CAS retry are locally reviewed; the automatic PlayerPrefs reload-marker dispatch remains unproven.
The cached post-reload proof uses visible owner replacement (generation2 to3),
not the automatic restore-only coroutine. Earlier two deadline-preserved CAS
attempts and one noncolliding commit remain separate from actual Conflict,Committed.
Exact receipts live in the workspace remote-config-service work packet.
The legacy facade remains unavailable; live/device/game integration remains held.

Focused tests exercise real Windows files, interrupted commits, CAS contention,
cancel-before-replace, lost acknowledgements, scoped LRU/pins, raw whitespace restoration,
clock uncertainty, identity fencing, captured-release and admission races. The loopback
HTTP test uses an actual OS-assigned server and gzip with the real file store for the
B/shared cold/warm/restart journey. Java compilation and JVM URI guards and mocked JS
tests are supporting boundary evidence, not JNI or Unity runtime qualification.
The managed teardown probe invokes actual shipping Editor-branch Request/OnDestroy
methods for 500 cancellation races using installed Unity DLLs and reflection. It bypasses
native MonoBehaviour construction and executes only BCL state; it verifies that boundary
without claiming actual Unity lifecycle execution.

The visible browser harness invokes the shipping jslib with real IndexedDB and two
owned loopback origins. Buttons expose reload/raw-byte restore, transaction abort,
injected labeled quota, actual opaque-origin API denial, two-tab contention/merge and
namespace-only cleanup with retained other-owner sentinel pins. It never deletes the
shipping database. Root records actual browser execution separately. Unity IL2CPP,
managed-to-Java JNI and physical devices require their named target evidence; no claim
is inferred from portable .NET tests or reference-assembly compilation. RC-LIVE and
Pocket Bloom integration remain held.

Final local browser review: `rc-s5-repaired-browser-results-review.json`,
SHA256 `12742f6d6955de28a2b3c999b4f201ea74a77bbe51486ebafdfd845ea3f1cc0e`, in the workspace service packet. Two-origin read success
and source credentials=omit are recorded; browser server receipts do not archive
request credentials/CORS header fields. Native HTTP header observations are separate.
