# Portable Friends and friends rankings - 2026-10-11

This owner-selected slice follows workspace `plans/FRIENDS-2026-10-11.md` and
the canonical backend `docs/work/friends-2026-10-11/IMPLEMENTATION.md`. SDK base is
`4d4dbb6bcedcff9f82e29f7fab20c3babe6b6e7c`. Root is the sole coordinator, Git
integrator and package writer. Commit reviewed instructions before source edits.
This task does not activate gifts, private chat, social-login linking, referrals,
leaderboard rewards or broad P4. Historical closed acceptance stays intact.

## Canonical contracts and selected behavior

Mirror additive reviewed `contracts/v1/schemas/friends.schema.json`,
`contracts/friends/snapshot.json` and its logical/MessagePack byte fixtures.
Do not rewrite frozen core, Teams or global/country leaderboard schemas/pins.
Use separate domain, wire and storage types, named-map MessagePack, RFC-order UUID
bin16, UUIDv7 operation IDs and checked signed64 revisions/scores/timestamps.
Closed, bounded decoding rejects unknown/duplicate fields, invalid discriminated
payloads, malformed UTF-16, oversized lists/cursors and ambiguous numeric forms.
No JSON fallback, typeless metadata, dynamic runtime reflection or Unity API.

Authenticated POST routes are `/v1/apps/{appId}/friends/query`,
`/v1/apps/{appId}/friends/commands` and
`/v1/apps/{appId}/leaderboards/friends/query`. Backend instructions and its
reviewed schema own exact field spelling and cursor/error semantics; do not infer
new fields or silently weaken schema validation to fit client models.

Friends query views are `mine`, `friends`, `incoming`, `outgoing`, `blocked`,
`suggestions`, `lookup`. Bounded first pages default to 30 and accept 1..50;
lookup accepts one exact public code, never raw UUID/name search. Public codes
are stable server-issued 12-character uppercase Crockford base32. Only the own
profile/readback allocates an own code; discovery, lookup and suggestions never
allocate peer state/codes. Absent peer state uses logical privacy defaults.
Authorized command transactions may establish affected-participant social state
and feed revisions without allocating that peer's public code.
No unavailable/private/blocked distinction is exposed by lookup failures.
Suggestions contain bounded current same-team public summaries only.

Commands are `send`, `accept`, `decline`, `cancel`, `remove`, `block`, `unblock`,
`set_privacy`. Discriminated payloads carry only each action's approved fields.
`send` accepts exactly one of `{publicCode}` or `{targetPlayerId}`; server accepts
the ID variant only for a current same-team peer. Requests are server-owned,
expire at their returned expiry and do not become friendships by being sent or
crossed. Privacy controls allowing new requests. Removal/block and unblock use
authoritative receipts; unblock never restores an edge. Preserve exact request,
peer and operation identity and the pending expiry in models.

Friends query fields are protocolVersion=1, view, pageSize (1..50), optional
cursor and publicCode required only for lookup. Cursor is forbidden for mine
and lookup; use canonical common opaque-token wire decoding, not arbitrary text.
All Friends response fields are present: protocolVersion, appId, accountId,
playerId, view, serverTimeMilliseconds, cacheExpiresAtMilliseconds, operationId,
revision, graphRevision, myPublicCode, allowRequests, friendCount, incomingCount,
outgoingCount, friends, requests, blockedPlayers, suggestions, player, request,
relationship, relationshipRequestId, nextCursor, resultCode.
Queries have null operationId/resultCode/request; unavailable code lookup has
null player/relationship/relationshipRequestId with the same successful shape.
Visible lookup relationship is self/none/friend/outgoing_pending/incoming_pending;
relationshipRequestId is UUIDv7 exactly for either pending state and null otherwise.
Other query views and all commands have null relationship/relationshipRequestId.
Lookup returns this relationship directly from one bounded pair lookup so the
consumer can select Request/Cancel/Accept/Decline without fetching every page.
Own public code resolves self even when incoming requests are disabled.
Mine supplies metadata and empty lists. List views fill only their matching list.
Command responses have view null and carry exact operationId, action resultCode, affected peer/request
when applicable, empty lists and null nextCursor; command receipts remain original
outcomes on replay. Social revision covers
all own social changes; graphRevision changes only for accepted-edge/block graph
changes. Validate both monotonic contexts without treating them as gameplay pull
projection revision. GraphRevision alone cannot validate request/privacy caches.
Friend limit is configured in 1..200, default 200; SDK cannot raise it. Incoming
and outgoing unexpired caps are independently 1..50, default 50. Pending requests
expire at the server deadline, default 14 days. PublicPlayer has playerId,
displayName (1..64) and nullable avatarKey (up to 128). Friend has player and
sinceMilliseconds. BlockedPlayer has player and blockedAtMilliseconds. Request
has requestId UUIDv7, senderPlayerId, recipientPlayerId, status, createdAtMilliseconds,
expiresAtMilliseconds and peer; status is pending/accepted/declined/cancelled/expired.
All nested fields are required. Turning allowRequests off preserves existing
friends and pending actionable requests and permits the owner's outbound sends.

Friends rankings use existing `players-monthly` and explicitly configured
`players-daily`/`players-weekly` boards, periods and score policy. The immutable
cohort is the viewer plus accepted unblocked peers, including zero-score rows.
No completion fanout, arbitrary score submit, reward or history backfill.
Keep `client_trusted_unvalidated` score authority visible. The response retains
period/snapshot/expiry/entries/nextCursor/self fields, adds graphRevision and
cohortSize, and has snapshotLimit = configured friendLimit + 1 (2..201).
Its canonical envelope also includes protocolVersion/appId/accountId/playerId;
countrySelection and entry badgeId are null. Entry countryCode may retain its
current standing country and does not filter the cohort.
Self is mandatory from that same snapshot; cohortSize is 1..snapshotLimit.
Continuation is bound to viewer/app/board/period/graphRevision/snapshot/position
and rejected by backend after a graph change. Friends has no country filter or
team participant; original global/country positive-score contracts stay pinned.

## Portable API and composition

Create `GamePlatform.Features.Friends` domain `FriendsQuery`, `FriendsCommand`,
closed payload/public-summary/request/profile/response models and `FriendsService`.
Add inward `IFriendsRemote` and `IFriendsStateCodec` ports beside the service,
following TeamsService's explicit constructor injection and cancellation pattern.
Expose `ReadCachedAsync`, cache-first `ReadAsync`, forced-network `QueryAsync`,
`ExecuteAsync` and `ReadPendingAsync`. Constructors perform no IO. An absent remote
permits honest cache reads and forbids confirmed authority-changing operations.

 Capture bootstrapped playerId as well as ScopedOwnerContext, whose identity
covers backend/app/account/view/generation. Validate every response's app/account/
player and requested view/context, echoed mutation operation and snapshot bounds.
Namespace/key local state by captured player ID as well as the existing database
and view scope; another player mapping cannot inherit rows or pending commands.
Dispose retires the service, cancels linked lifetime operations and fences every
late reply/write/return. Consumer account leases admit/drain work before closing
storage; preserve that host lifecycle and do not invent a new global resolver.

Reuse existing `LeaderboardsService` and `LeaderboardsQuery`: allow domain
scope `friends` only for player boards with countryCode null. Provider dispatches
that domain query to a separate additive Friends leaderboard wire DTO/codec/route.
Add graphRevision/cohortSize metadata to domain response and zero-capable domain
entries, but preserve closed global/country wire validation and existing fixtures.
Either use an explicit captured-player overload or a separately injected friends
ranking context; existing callers keep current signatures and behavior. Specify
and test the chosen constructor before adding any consumer dependency.

Add distinct wire types under `GamePlatform.Wire.Contracts/Friends`, a bounded
`FriendsMessagePackCodec`, `FriendsHttpProvider`, `FriendsModelMapper` and
`FriendsStateCodec`. Add `ProductionAccountHttpProviders.Friends`, and friends
ranking dispatch to the existing account leaderboard provider. Reuse HTTP auth
refresh, bounded execution and safe typed RemoteFailure mapping. Never log codes,
names, raw IDs, auth tokens, private request payloads or operation bodies.

Add `FriendsSupportingWireCodec` for additive private pull core + existing Teams
markers + Friends markers. Replace only the account factory's pull codec selection;
frozen core and Teams-only codecs remain reusable and independently validated.
Add `FriendsProjectionInvalidation` exposing entityType `friends`, own player ID,
revision and kind `invalidation`. Backend uses the existing closed marker shape
without graphRevision; the next Friends response supplies current graphRevision.
Consumer applies FriendsService.InvalidateCache and friends leaderboard invalidation
in the same transaction as marker/cursor commit, never a private-bootstrap refetch.
Teams membership invalidation also invalidates teammate suggestions. The SDK pull
provider must decode the marker without copying shared graph content into private
projections. Unity owns applying the marker to its existing dispatcher.

## Persistent cache, expiry and graph fencing

### Lazy durable app-player binding refinement

The Unity account directory contains an account ID, but no app-player ID. Preserve
its schema and avoid an extra startup request. Add one service-owned shared
`FriendsPlayerBinding` policy/helper under `src/GamePlatform.Features/Friends`.
It captures backend/app/account/view/generation, starts no IO in its constructor,
and lazily reads only its account/view-scoped durable binding metadata from existing
feature state. A constructor's absent player is an unbound sentinel, never a
real player ID or a namespace containing Guid.Empty. No player cache/pending keys
are read or written before a positive durable binding has been established.

The first authenticated Friends query response OR friends-ranking response can
establish the same binding in a serialized local transaction after exact captured
app/account/owner/lifetime checks. Persist only the app-player identity metadata
there, not credentials or a fabricated confirmed projection. Concurrent first
responses must agree on the same nonempty player ID; conflicting responses fail
closed, without replacing/deleting the existing binding or moving pending work.
Every subsequent response and reopened cache/command validates that exact player.
Both services use the same binding key and policy, even when separately constructed;
no independent Friends/ranks identity cache may diverge. An explicitly supplied
known player must also agree with any existing durable binding.

Account-level shared invalidation epoch is readable before first binding so a
pull marker or retirement during an unbound request fences its late reply as
well. First binding, owner checking, fence checking and accepted response cache
write form one local transaction. On restart binding is read locally and a still
fresh cache remains a zero-network read. Before first binding an explicit Friends
query/ranks query may fetch its own ordinary response; offline reads return Missing
and cannot admit a mutation. Mutations lazily load existing binding, require it,
and never provision it through a command receipt.

Keep the current explicit-player constructor usable and add an owner-only overload
for normal Unity composition. Preserve old LeaderboardsService callers and add
shared lazy binding only to its selected friends path. This refinement changes
no account directory, provisioning/core schema, gameplay bootstrap or startup IO.
Tests cover first Friends/ranks binding in either order, concurrent conflicting
players, explicit-known-player conflict, fresh restart without network, invalidation
during first unbound query, offline missing binding and pending preservation.

Use existing IDurableFeatureStateStore/SqliteDurableFeatureStateStore and
ISerializedStorageExecutor; no schema migration, PlayerPrefs or memory-only cache.
Use a system UTC wall-clock default and an injected deterministic clock overload
for tests; constructors still perform no IO.
Keep one default first-page slot per view, one replaceable exact-code lookup slot,
and one first-page slot per enabled friends player board. Query context is checked
before cache reuse; arbitrary limits/codes/cursors must not grow durable rows.
Lookup slot is matched to its exact requested code. No continuation page is marked
current from a first-page slot or appended to a response from another snapshot.
Expire retained Friends views after seven days and rankings after 24 hours;
fixed-slot cleanup is bounded and never removes pending mutation identities.

Persist exact received response bytes plus local request-start/receipt timestamps and enough
bounded metadata to validate request/player/epoch and server-relative lifetime.
Friends lifetime is at most 60 seconds and bounded by earliest pending expiry;
rankings expire at the backend's five-minute bucket or period end. Cached reads
never rewrite receipt/expiry to extend freshness. Calculate freshness using
checked elapsed local wall time from request start against returned
server-time-to-expiry delta, so network delivery time cannot extend eligibility;
negative elapsed/clock rollback, expired period, invalid metadata or uncertain
time is stale. Restart while the persisted lifetime is valid returns Current
without network. No cached request past expiry is presented as actionable.

ReadAsync returns a valid current cache directly; stale/missing may refresh when
remote is available. Transient/offline failure may return an explicitly stale
previous value; authorization failures erase protected cache and never return
it as an offline fallback. QueryAsync explicitly forces network. Expose Missing,
Current and Stale through existing FeatureSnapshot types; successful receipts
are acknowledgements and do not fabricate a refreshed graph/list/rank snapshot.

Store durable invalidation epoch and last required social revision. Every remote
read captures epoch before IO and compares inside its serialized commit and before
return, including uncached lookup/continuation. Response cannot regress below
required revisions. Invalidation deletes all fixed Friends slots, all friends
ranking slots and increments epochs; do not delete the pending mutation journal.
Pending request/privacy changes invalidate lists too. Successful friends receipts
perform invalidation and pending removal in one real local transaction. Any newer
Friends pull marker clears/fences Friends and friends rankings, even without its
graphRevision. Fence late network pages and validate backend cursor rejection;
all previously mounted pages/cursors are discarded by the Unity consumer.

## Durable explicit mutation retry

Encode and validate before transaction/network. Admit one immutable pending
command with UUIDv7 operation ID and canonical request bytes before sending.
Concurrent/new different bodies conflict rather than overwrite pending identity.
ReadPendingAsync survives process restart and preserves exact operation/payload.
Retry is explicit and resends the same identity/body; never auto-replay on boot.
Timeout, transport failure, cancellation or unavailable ownership cannot prove
rollback. Retain pending identity across these and malformed/mismatched receipts.
Only reviewed canonical terminal rejection semantics that cannot mask a prior
commit may release a pending operation. Backend retains immutable rejection
receipts for admitted domain command failures, including cooldown/rate limit,
after serialized dedup and bounded new-ID admission. Its canonical error has
retryable=false and details
`{operationId: canonical UUID text, receiptRetained: true,
retryAfterMilliseconds: delay when relevant}` (the delay stays inside details).
The HTTP adapter maps this to bounded typed FriendsOperationException metadata;
do not expose a raw vendor/details dictionary into the service. Retire pending only if this attestation
matches the admitted operation and a reviewed domain rejection, never from a
coarse HTTP status. A later user attempt after delay uses a new UUIDv7; retrying
the original ID correctly replays its retained rejection. Query rate errors have
no command receipt and remain retryable. Pre-admission `friends.admission_limited`
also has no receipt attestation, retryable=true and top-level
retryAfterMilliseconds in 1..60,000. Preserve its uncertain pending bytes/identity
and expose a temporary pending delay; explicit retry after that delay uses the
same ID, because this reply cannot settle an earlier in-flight duplicate. Queries
and cached reads remain available while the mutation is pending. Do not promote
an unattested ingress throttle into a terminal 24-hour cooldown or create a new
identity for that uncertain action. Only an admitted retained business receipt
releases the mutation journal. Backend dedup replays retained receipt
before current action policy; operation-body mismatch must not erase original.
After valid success atomically clear pending and invalidate dependent caches;
failure of the local commit retains an explicitly retryable journal. No optimistic
accepted edge, success, privacy or leaderboard mutation is displayed as confirmed.
Immutable command success and retained rejection receipts are exempt from query
freshness, expiry and revision floors: a valid original receipt must retire the
matching pending identity even when its recorded revision/time predates a newer
query or invalidation. Still validate exact owner/app/player/operation and original
admitted body identity. Never install retained original receipt metadata into a
current query cache or regress the separately stored query revision floor.

## Repository-qualified writable paths after instruction commit

Only `dotnet:docs/work/friends-2026-10-11/IMPLEMENTATION.md` is writable during
document preparation. After root activates the reviewed implementation packet:

- `dotnet:src/GamePlatform.Features/Friends/**` and
  `dotnet:src/GamePlatform.Wire.Contracts/Friends/**`.
- `dotnet:src/GamePlatform.Features/Leaderboards/LeaderboardsService.cs`,
  `LeaderboardsModels.cs`, `LeaderboardsRequests.cs`, `README.md`.
- `dotnet:src/GamePlatform.Serialization.MessagePack/FriendsMessagePackCodec.cs`,
  `FriendsSupportingWireCodec.cs`, `generate-schema.py`, `QualificationSchemas.g.cs`.
- `dotnet:src/GamePlatform.Transport.Http/FriendsHttpProvider.cs`,
  `FriendsModelMapper.cs`, `FriendsStateCodec.cs`, `LeaderboardsHttpProvider.cs`,
  `LeaderboardsStateCodec.cs`, `LeaderboardsModelMapper.cs`,
  `ProductionBackendHttpProviders.cs`.
- `dotnet:contracts/v1/schemas/friends.schema.json`, `contracts/friends/**`.
- `dotnet:tests/GamePlatform.Tests/Features/Friends/**`,
  `Features/Leaderboards/LeaderboardsServiceTests.cs`,
  `Serialization/FriendsCodecTests.cs`, `Serialization/LeaderboardsCodecTests.cs`,
  `Transport/FriendsHttpProviderTests.cs`, `Transport/LeaderboardsHttpProviderTests.cs`,
  `Transport/Cl010PrivateSyncHttpProviderTests.cs`.
- `dotnet:scripts/validate.py`, `scripts/test_validation.py`,
  `docs/FEATURES.md`, `docs/IMPLEMENTATION_STATUS.md`,
  `src/GamePlatform.Features/Teams/README.md`, `docs/work/friends-2026-10-11/**`.

Update generate-schema.py to include the separate friends pin. Update validate.py
to verify additive Friends pins without changing frozen contract version or
declaring historical catalog acceptance complete; cover that pin registration in
test_validation.py. Do not change architecture references or add NuGet packages.
Any additional writable file is named to root before editing. Root alone writes
packages/UPM imports, indexes, source commits/pushes and consumer changes.

## Required checks and separate runtime gates

Root authorized the final required integration seams in
`G3PrivateSyncProjector.cs`, `PrivateSyncHttpProvider.cs`,
`BoundedHttpClientExecutor.cs` and `SqlitePrivateSyncStore.cs`, with regression
tests. SQLite accepts Teams/Friends only as empty, positive-player-keyed feed
invalidation markers; bootstrap manifests, snapshot inventories and social
upserts remain unsupported. Hosts invalidate Friends when Teams membership
changes so suggestions and pending reads use the same account epoch.

`FeatureSnapshot.LastConfirmedAtMilliseconds` exposes the original local UTC
request-start anchor for query caches. Reopening or rereading does not replace
that anchor or extend server expiry. Hosts calculate remaining freshness from
the unchanged response server time and this anchor, then use their monotonic
elapsed clock. Command receipts do not supply a current query anchor.

Retained rejection identities resolve only for the reviewed admitted business
codes target_unavailable, already_friends, outgoing_request_pending,
incoming_request_pending, pair_cooldown, friend_limit, outgoing_limit,
send_cooldown, request_unavailable, request_expired and not_friends (all with
the friends. prefix), and only Validation/Conflict/RateLimited failure kinds.
Authentication, authorization, unknown future codes and admission errors retain
the original immutable pending command.

Use deterministic clocks/IDs and controlled races, not sleeps. Focused tests cover
real SQLite restart with a fresh cache and zero remote calls; expired/offline stale
cache; clock rollback; exact-code/query binding; player/account/app/backend/view
isolation; retired-owner completion; graph/private-pull invalidation during late
first/continuation page; revision regression; all-slot boundedness; empty lists;
pending expiry; privacy and public-code closure; zero-score mandatory viewer row;
friend-cap/period/snapshot/cursor binding; durable pending retry after cancellation,
lost receipt/local commit failure; mismatched operation/body; no optimistic value.

Test MessagePack backend byte parity, UUIDv7/bin16/signed64 boundaries, duplicate/
unknown fields, bad discriminated payloads, oversized lists, malformed responses,
auth refresh and additive core/Teams/Friends pull preservation. HTTP tests verify
the exact three routes and account/request context, no raw-ID lookup, no JSON
fallback, and no broadened legacy leaderboard route.

From disposable exact-source copies run focused SDK tests then affected regression,
scripts/validate.py, validator tests, generate-schema.py --check, locked restore,
whitespace verification, Release build and full Release tests. Root serializes
deterministic unpublished package/UPM production and records exact source/payload
hashes. SDK success is not Unity import/AOT/native SQLite/TLS/device acceptance.
Fresh nonauthor review precedes activation/push. Unity local/mounted and physical
Android checks precede the separately authorized Supabase dev stage; host and real
database checks remain distinct. Record exact evidence and every unrun gate.
