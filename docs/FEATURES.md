# Feature catalog

`features.json` is the original sixteen-feature acceptance ledger. Its `unverified`
and `stubbed` values retain their full historical scope, including the copy in a
generated package. They do not inventory the scoped services added since that
ledger was established. Accounts, Profiles, Catalog, Inventory, Wallet,
Entitlements, RewardFulfillment and Progression remain `unverified` there; Quests,
Achievements, Store, Purchases, Leaderboards, Teams, RemoteConfig and Inbox retain
their original `stubbed` entries. No broad acceptance flag changes in this update.

## Current scoped implementations — 2026-10-12

The [current SDK status](IMPLEMENTATION_STATUS.md) identifies dev.144 source,
delivery and consumer evidence. The table distinguishes reusable implementation
from the remaining product or acceptance work.

| Area | Available scoped implementation | Remaining boundary |
| --- | --- | --- |
| Accounts, Profiles and Progression | Explicit account lifecycle, profile and durable gameplay-outcome services. | Full original feature acceptance, provider identity linking and actual account-switch journeys remain separate. |
| Catalog, Wallet, Inventory, Entitlements and RewardFulfillment | Catalog-bound confirmed value, durable intent/receipt state, ordered-feed correlation and one-time reward presentation. | Each new game feature still needs its server contracts and consumer composition; cached value cannot authorize a purchase or grant. |
| [Store](../src/GamePlatform.Features/Store/README.md) | `StoreService` durably queues an immutable server-priced `store.offer.purchase` intent and reuses its operation after retry/reopen. | Capability defaults off. Offer browsing, eligibility/current price presentation, game UI and full purchase acceptance remain pending. |
| [Quests](../src/GamePlatform.Features/Quests/README.md) | `QuestService` durably queues one `quest.claim` intent per server occurrence identity. | Capability defaults off. A general objective engine, timed occurrences, claim eligibility and game UI are not supplied by this service. |
| [Teams](../src/GamePlatform.Features/Teams/README.md) | Authenticated account-bound discovery, membership, roles, invitations, requests, chat, moderation and help; durable caches and explicit uncertain-command retry. | Local generated-DLL, hosted DEV and selected physical Android acceptance are recorded for their dated payloads. Full CL-114/INT-014 and broad P4 acceptance remain separate. |
| [Leaderboards](../src/GamePlatform.Features/Leaderboards/README.md) | Server-owned player/team periods, global/country snapshots, country assignment and friends standings. | Selected hosted/Android completion-to-score and offline/reconnect acceptance are recorded. Rankings grant no rewards; broader settlement and historical acceptance remain separate. |
| [Friends](../src/GamePlatform.Features/Friends/README.md) | Core graph, requests, exact-code discovery, teammate suggestions, privacy/blocking, durable cache/journal and friends rankings. | Selected hosted DEV and physical Android acceptance are recorded. Gifts/private chat remain outside scope; Friends is additive to the original sixteen-feature ledger. |
| Achievements, Purchases, RemoteConfig and Inbox | Unavailable legacy facades and documented requirements. | These modules still need production services and consumer integration. Purchases means real-money provider verification, separate from the virtual Store intent. |

Teams' later [physical DEV receipt](https://github.com/gindemit/MrSquareUnity/blob/main/Docs/Implementation/DualEdition/2026-10-11-teams-device.md)
supersedes its earlier pending physical result. [Friends qualification](https://github.com/gindemit/game-platform-workspace/blob/main/docs/FRIENDS-VALIDATION-2026-10-11.md)
and [Leaderboard qualification](https://github.com/gindemit/game-platform-workspace/blob/main/docs/LEADERBOARDS-VALIDATION-2026-10-11.md)
likewise preserve exact source, delivery, device and hosted evidence. The later
dev.144 physical rerun is native-only; it does not silently rerun those ordinary
gameplay/hosted journeys.

There is no dedicated daily-rewards service. A selected daily reward can reuse
the durable claim and reward infrastructure, but still needs server-owned time,
occurrence/eligibility and claim uniqueness, an agreed reset/missed-day policy,
and actual game presentation.

Legacy parameterless feature facades stay fail-closed even where a scoped service exists; hosts must explicitly compose the scoped implementation and its real ports. No stub may authenticate, grant, acknowledge, mark Ready or simulate successful transactions. Module READMEs define each implemented boundary, exclusions and remaining evidence.
