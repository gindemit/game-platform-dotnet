# GamePlatform.Transport.Http

Maps semantic backend calls to frozen v1 endpoints and wire DTOs through injected
executor, codec, auth-session and logical backend configuration boundaries. The
CL-005 provisioning provider enforces HTTPS authority, MessagePack-only
negotiation, bounded bodies, response owner/stream checks, explicit delivery
uncertainty and one coordinated 401 renewal per session generation. The bounded
CL-005/CL-010 private-sync provider maps authenticated bootstrap start/pages and
pull/reset through the same injected ports, with MessagePack preferred and
diagnostic JSON available only by explicit construction. Mutations are never
replayed after timeout/5xx and no operation falls back to JSON after decode
failure.

Private sync instances are scoped to an expected app and platform account and
reject mismatched bootstrap ownership before returning data to the coordinator.
An externally owned `AuthRefreshCoordinator` provides session/account-keyed
single-flight renewal across provider instances, including provisioning. Its
owner disposes it only after all borrowing providers and admitted requests have
quiesced. Page and pull responses are bounded by both the global cap and their
requested budget; error correlation and reset reasons are closed protocol data.

The CL-009 command-receipt provider maps the frozen authenticated lookup route
through the same ports. It binds app, account, installation, stream, operation,
sequence and fingerprint; returns only retained terminal outcomes; rejects a
missing receipt at or below the observed finalized watermark; and preserves the
encoded terminal result for durable sender finalization. Receipt absence never
permits a new operation identity.

`BoundedHttpClientExecutor` is the portable `System.Net.Http` production executor
for these frozen operations. It admits only the configured HTTPS authority and
the provisioning, bootstrap, pull and receipt routes, accepts only the frozen
MessagePack media type, bounds request/response bodies and response headers,
rejects redirects and preserves caller cancellation plus delivery certainty.
The caller owns its `HttpClient` and must disable automatic redirects.

`ProductionBackendHttpProviders` is the explicit manual composition path. It
uses `MessagePackWireCodec`, exposes provisioning before an account exists and
creates an immutable account-owned private-sync/receipt scope only after
provisioning; the caller owns and quiesces the executor, auth session and shared
refresh coordinator. Push, account, profile and stream recovery
operations remain fail-closed and unsupported. No credentials or endpoint are
embedded. Backend BE-016, UnityWebRequest and live hosted execution remain
separate gates.
Gates: A02, A11, A12.
