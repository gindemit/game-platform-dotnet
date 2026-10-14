# Teams

`TeamsService` implements account-scoped online Teams queries and mutations with
typed discovery, membership, roles, invitations, join requests, chat, moderation
and help contracts. `ProductionAccountHttpProviders.Teams` composes the real
authenticated MessagePack HTTP adapter. The historical `TeamsFeature` capability
facade remains unavailable, matching the other legacy scaffold facades; callers
use the explicitly constructed service.

The service captures backend/app/account/view/generation. A bounded SQLite cache
retains one default first page per view and always labels reopened data stale.
Authority-changing actions require an online remote. A durable pending-command
journal retains the exact operation and payload after an uncertain response;
`ReadPendingAsync` exposes it for explicit retry and never replays automatically.
Confirmed command receipts invalidate query views rather than fabricate a current
roster. Account retirement and a durable invalidation epoch fence late reads.

The canonical Teams schema is mirrored under `contracts/v1/schemas` with an
additive `contracts/teams/snapshot.json` pin. Frozen v1 contracts remain unchanged.
Membership changes travel through private pull only as `teams` invalidations;
the consumer must call `TeamsService.InvalidateCache` in the same cursor
transaction and must not interpret the marker as a private-bootstrap refetch.
No shared roster/chat content is copied into private projections.

[October 11 source validation](EVIDENCE-2026-10-11.md) distinguishes local source
tests from package, Unity, IL2CPP and physical-device acceptance. Historical
CL-114/INT-014, P3/G3 and broad-P4 gates are unchanged.
