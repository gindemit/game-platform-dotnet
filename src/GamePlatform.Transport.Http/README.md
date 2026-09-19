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

The semantic providers are portable boundary implementations, not production
composition. `UnavailableMessagePackCodec` remains production-facing and no
concrete HTTP executor is registered. Push, receipt, account, profile and
recovery operations remain unsupported. A production codec, real host executor,
backend BE-016 composition, UnityWebRequest adapter and device execution remain
separate gates. Gates: A02, A11, A12.
