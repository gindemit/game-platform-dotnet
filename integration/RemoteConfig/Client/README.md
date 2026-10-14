# Optional configuration host adapters

UnityConfigurationHost is an explicit, separately owned MonoBehaviour implementing
transport/store/clock/random. Initialize(policy,nativeStoreRoot) assigns a unique local
SendMessage receiver and platform bridge handle; it creates no game account or polling
service. Inject the host into the optional ConfigurationClient and call its foreground
TickAsync from the consumer. Update marshals Unity/JNI calls and fulfills injected-clock
deadlines; no Task.Run or managed Timer is required on WebGL. SetTimeAnchorTrusted is
an explicit outer-host assertion of verifiable freshness time, never economic authority.
Use one dedicated GameObject containing exactly one MonoBehaviour, this host. Initialize
rejects duplicate components and dispatch checks receiver ownership/name again. Keep
its assigned receiver name stable for its lifetime; two hosts use separate GameObjects
and independent handles even when request counters both begin at 1. Only the two
SendMessage callback methods have narrow Preserve attributes for IL2CPP linking.

Android owns a config-only ConfigurationHttp Java instance with two ordinary and one
control slot, 5-second socket timeouts, exact origins/paths, no redirects/TLS override,
and decoded-body bounds before Base64/JNI transfer. No jar:file fixture loader or native
singleton was promoted. Ambient CookieHandler is rejected; hosts must keep configuration
outside ambient account authentication/credentials. Proguard rules preserve the owned
JNI entry; hosts selecting minification must include that file in their build policy.
Java compilation/URI checks do not qualify physical Android/JNI behavior.

WebGL owns independent jslib handles/roots/pending requests and disposes them explicitly.
Browser fetch uses credential-free CORS, no redirect, bounded decoded streams and normal
HTTP-cache revalidation; the forwarded persisted ETag is association evidence, not an
app-supplied cross-origin If-None-Match header. Network 304 may surface as cached 200.
IndexedDB commits objects and namespace indexes in one strict-durability transaction
with signed64 generation CAS. Original pointer/control bytes remain protected alongside
their sequence/observation. Objects are stored per permanent namespace; old owner's
pins remain reachable. Current/backup index reachability protects eviction. Browser/OS
eviction and API denial still require explicit fallback and cannot promise physical-disk
durability. The adapter emits quota/conflict/unavailable/unknown outcomes, never success
for an aborted transaction.

Dispose/OnDestroy cancels owned work and releases registrations outside the pending-map
lock. Private handles include immutable app/env/owner/subject/generation binding; a
restored matching namespace creates current-generation handles after revalidation.
Ownership and optional analytics gates are checked by the portable client. These are
standalone adapters: no Pocket Bloom, game imports, authentication, SQLite, rewards,
telemetry emission or production route/deployment is added.

The visible browser-tests.html harness calls the shipped jslib directly through explicit
buttons and reports DOM diagnostics. serve-tests.py supplies two ephemeral loopback
origins with real CORS/cache/gzip and a logged lifecycle; it records adapter evidence,
separate from C# lifecycle/Unity evidence. Mocked boundary tests are supporting checks.
Injected quota is labeled; no attempt fills a real disk/browser quota. Opaque sandbox
storage denial and transaction abort/CAS/reload/two-tab tests use actual browser APIs.

stage-client.py stages only inspected core/client assemblies and these owned adapter
sources into a fresh isolated package. The regular SDK export excludes both optional
assemblies. Unity build, real browser execution and physical device qualification need
their separately coordinated leases; staging never edits an existing game/project.
