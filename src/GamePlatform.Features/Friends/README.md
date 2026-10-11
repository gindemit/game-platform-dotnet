# Friends

The selected account-owned FriendsService implements bounded authenticated friends,
requests, exact public-code lookup, teammate suggestions, privacy and blocking.
It shares a lazy durable app-player binding with friends LeaderboardsService reads;
neither constructor performs IO. Existing SQLite feature-state stores retain
bounded first pages, fixed expiry, owner/view/player isolation and invalidation
epochs. Fresh reopened reads perform no network call. Offline or expired views
remain explicitly stale and grant no authority to mutate.

One immutable pending operation/body is journaled before send. Outcome uncertainty
requires explicit same-ID retry. Matching retained success/rejection resolves the
operation even when its historical revision predates current query floors. An
unattested short admission throttle retains pending; an attested admitted business
rejection releases it for a later new user operation. Receipts never install a
current graph snapshot.

Production account HTTP providers expose Friends and the separately pinned
friends-ranking route. The additive pull codec preserves core/Teams plus Friends
invalidation markers. Hosts apply InvalidateCache in the cursor transaction and
discard mounted pages/cursors. Reset/bootstrap replacement uses its owner-only
overload. Gifts, private chat, rewards and broad P4 remain unselected.

See [implementation instructions](../../../docs/work/friends-2026-10-11/IMPLEMENTATION.md)
and [local evidence](../../../docs/work/friends-2026-10-11/EVIDENCE.md). SDK tests
do not establish generated-package, Unity AOT, native or physical Android acceptance.
