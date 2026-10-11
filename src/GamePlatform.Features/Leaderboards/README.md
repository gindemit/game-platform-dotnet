# Leaderboards

Owns the selected account-bound `LeaderboardsService`: authenticated immutable
top-500 player/team standings, global/country queries, bounded stale first-page
cache and explicit durable country-operation retry. Scores and UTC periods are
server-owned; the SDK exposes no arbitrary score submission or reward settlement.
The selected friends scope includes the viewer and accepted peers, including
zero scores, with bounded immutable cohort pages. It shares Friends' durable
player binding and invalidation epoch; fresh reopened friends reads avoid HTTP.
See [Friends evidence](../../../docs/work/friends-2026-10-11/EVIDENCE.md).
The legacy `LeaderboardsFeature` facade stays
unavailable and does not describe this narrow service's runtime acceptance.

See [selected implementation instructions](../../../docs/work/leaderboards-2026-10-11/IMPLEMENTATION.md).
Historical CL-113/A07/A12 and broad P4 acceptance remain separate; source tests
do not establish hosted, Unity AOT or physical Android qualification.
