# Profiles

Owns app-scoped profile reads/revision edits. Depends on inward contracts only; excludes SQL/HTTP.

`ProfileService` is the bounded CL-102 coordinator. It is constructed for one captured
backend/app/account/view/generation and uses feature-owned neutral remote/codec ports,
the CL-011 durable feature-state seam, and the CL-008 atomic command seam. A durable
cached projection is explicitly stale until a successful remote read. One pending edit
retains its exact expected confirmed revision, operation, stream and business source;
the pending projection and immutable `profile.patch` outbox command are committed in
the same transaction callback. A receipt moves only its matching edit to
`AcceptedAwaitingPull`; a later monotonic confirmed profile is still required before
the pending edit clears. A newer unreceipted profile revision is a visible conflict;
there is no automatic merge.

Known profile data has no created/updated actor, service, causation or other audit
fields. Those remain backend-authored. Supported opaque extension bytes are bounded,
validated by the injected codec, copied defensively and retained across local edits.
The feature adds no profile HTTP adapter, SQLite schema/migration, global active-account
lookup, Unity dependency, response replay, local grant behavior or production codec.

The existing parameterless `ProfilesFeature` remains the explicit unavailable legacy
placeholder; hosts must construct the scoped service only after real account readiness.
See `docs/implementation/evidence/CL-102/README.md` for the exact desktop SQLite and
injected-remote evidence and remaining A07/INT-008 work.
