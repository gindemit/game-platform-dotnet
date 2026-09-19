# GamePlatform.Transport.Http

Maps semantic backend calls to frozen v1 endpoints and wire DTOs through injected
executor, codec, auth-session and logical backend configuration boundaries. The
CL-005 provisioning provider enforces HTTPS authority, MessagePack-only
negotiation, bounded bodies, response owner/stream checks, explicit delivery
uncertainty and one coordinated 401 renewal per session generation. Mutations are
never replayed after timeout/5xx and never fall back to JSON.

The M0 unavailable type is retained for unsupported operations. A real host
executor, backend BE-016 composition, UnityWebRequest adapter and device execution
remain separate gates. Gates: A02, A11, A12.
