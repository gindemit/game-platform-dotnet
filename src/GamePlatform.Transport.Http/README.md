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

The semantic providers remain portable boundaries, not production composition.
Desktop tests exercise the `DesktopQualificationWireCodec` only through an
explicit test-local `IWireCodec` bridge. Production remains fail-closed through
the unavailable codec seam, and no concrete HTTP executor or host is registered.
Push, receipt, account, profile and recovery operations remain unsupported.
Backend BE-016, UnityWebRequest, codec/native packaging, IL2CPP/AOT/stripping and
device execution remain separate gates. Gates: A02, A11, A12.
