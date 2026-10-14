# Portable leaderboards implementation — 2026-10-11

This owner-selected slice follows workspace `plans/LEADERBOARDS-2026-10-11.md`
and backend `docs/work/leaderboards-2026-10-11/IMPLEMENTATION.md`. Base is
`ac03c39713e629ae766e90a51b7d16c0e21f4dfd`. Root owns Git and package integration;
this instruction is committed before coding. Historical CL-113/P4 remain held.

## Contract and behavior

Mirror the reviewed canonical `contracts/v1/schemas/leaderboards.schema.json`,
`contracts/leaderboards/snapshot.json` and logical/byte exchange fixtures exactly.
Preserve existing frozen core and Teams pins. Named-map MessagePack, RFC-order UUID
bin16, checked signed64 and closed bounded decoding apply; never fall back to JSON.

Authenticated POST operations are `/v1/apps/{appId}/leaderboards/query` and
`/v1/apps/{appId}/leaderboards/country`. Query fields are boardId, global/country
scope, countryCode only for country, limit 1..50 and optional opaque cursor.
Response includes immutable period/snapshot identity and expiry, fixed entries,
nullable self from that same top-500 snapshot, nextCursor and snapshotLimit.
Rank/score/timestamps are signed64; timestamps use epoch milliseconds. Null self
means outside or absent from the bounded snapshot, never globally unranked.
Continuation remains bound to principal/app/board/period/scope/country/snapshot
and paging position; the initial viewer team stays bound across continuation.

Country request carries UUIDv7 operationId, boardId, participantKind, teamId only
for team, and ISO2 countryCode. Response carries boardId, participantKind,
participantId, countryCode, effectiveAt and echoed operationId. Validate response context before
exposing success. Retain one immutable request identity/body for retry; ambiguous
transport failure is not proof of rollback. Country assignment is per boardId,
so player/team selectors and alternate schedules never share a preference.
Query responses also carry authenticated participant country-assignment readback,
serverTime (signed64 epoch milliseconds) and countrySelection: null or an object
with currentCountryCode, pendingCountryCode, pendingEffectiveAt. Pending code/time
are both null or both present. Null selection means no participant (no team); an
unassigned participant has all three fields null. Independently timed assignment
metadata may refresh on continuation; standings/self stay immutable. Cohort
countryCode is a query filter, never a substitute for authoritative assignment.

Initial boards are players-monthly/teams-monthly. Explicit configuration may
enable players-daily/teams-daily/players-weekly/teams-weekly. Server owns UTC
boundaries, reset timing, contribution policy and country effectiveness.
One first accepted eligible new completion earns +1; no arbitrary score submit,
historical backfill or reward. Preserve client_trusted_unvalidated authority.
Friends remains explicitly unavailable pending real authenticated graph support.

## Narrow source scope

Use namespaces GamePlatform.Features.Leaderboards and
GamePlatform.Wire.Contracts.Leaderboards for distinct domain/wire models.
Implement source beneath src/GamePlatform.Features/Leaderboards and
src/GamePlatform.Wire.Contracts/Leaderboards. Add LeaderboardsMessagePackCodec
under src/GamePlatform.Serialization.MessagePack and LeaderboardsHttpProvider /
LeaderboardsModelMapper under src/GamePlatform.Transport.Http. Integrate only
the account factory in ProductionBackendHttpProviders.cs. Follow TeamsService,
TeamsHttpProvider and explicit FeatureSnapshot owner-generation fencing;
constructors start no IO and account owners dispose services.

Update generate-schema.py to read separate leaderboard pins and regenerate
QualificationSchemas.g.cs. Name any additional generator/validator registration
before editing it. Remove/replace the LeaderboardsFeature unavailable facade only
if its actual contract becomes implemented; preserve unrelated unavailable APIs.
Tests belong to tests/GamePlatform.Tests with focused Leaderboards service,
MessagePack and HTTP cases. Canonical src remains the sole runtime source;
generated Git UPM delivery is serialized by root, never a Unity-only fork.

## Checks and remaining gates

Test schema/byte parity with backend, UUID/int64 boundaries, oversized/unknown/
duplicate fields, malformed responses, auth refresh, cancellation, context
mismatch, owner retirement and late reply fencing. Test immutable cursor/query
binding, null self, truthful availability and separate board country mutations.
Exercise retry identity/conflict and preserve exact server effectiveAt.
Run focused dotnet tests then affected regression, scripts/validate.py,
generate-schema.py --check and deterministic unpublished package/UPM verification
from disposable exact-source copies. Root records exact source/payload hashes.
Independent nonauthor review and cross-language fixtures precede activation;
desktop checks do not certify Unity AOT, native TLS/SQLite or physical Android.
