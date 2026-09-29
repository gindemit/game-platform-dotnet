# SDK handoff — current local campaign repair

2026-09-29. Single execution authority: [backend campaign entry](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/README.md), [checkpoint](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/checkpoint.md) and [repair plan](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/CMP-10-REPAIR-PLAN.md). Goal `CAMPAIGN_LOCAL`. Both external review attachments are incorporated; do not request them or launch an independent SDK coordinator/P3 dispatcher.

## Current delivery, not the old pre-integration state

Unity's reviewed source `8431266e54a25f01d000ab8cf946e19981508652` already consumes `upm/com.gindemit.game-platform` through full Git delivery pin `3008b3576052e6192ceeeeb236bdd8bae3b94efc`. The latest runtime-source increment is `32f2ba6c25f540db32eab9d6a9d129d81a5c4aea`, adding atomic game-state reconciliation to completion rejection. Its deterministic UPM payload is in the separate delivery commit. The older source `208aab7`/package `c33ef45` and CMP-03 integration are historical completed milestones, not the next task. The copied Unity SDK Assets payload has been removed.

Canonical runtime source remains src; UPM is generated delivery, not a fork. Preserve managed dependency closure, native importers, GUIDs, licenses, AOT/link/lifecycle data and explicit assembly references. The normal package includes reviewed Android ARM64/Windows native targets; the pinned macOS SQLite library was staged only for disposable qualification. Do not silently infer normal Mac Editor/native support from that test setup.

The [follow-up report](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/CMP-10-followup-2026-09-29.md) records 18/18 focused SDK and 662/662 full tests. Baseline GitHub CI run `36576253188` on delivery `3008b35…` passed. These results support their tested scope; they do not prove Unity's accepted campaign completions clear SDK pending state or qualify final Android builds. Later documentation-only commits do not change the runtime pin or establish new executable evidence.

## Next SDK-related work: CMP-R02

`ProgressionService` provides real generic local admission, pending state, rejection and confirmation; it is not a campaign selector or puzzle validator. Preserve its current transactional rejection callback and rollback/reopen tests. Do not redo Git packaging or reopen all feature facades.

The remaining integration gap is accepted/no-op confirmation. At reviewed Unity source, `ReconcileCampaignEvidenceCoreAsync` and `ApplyCampaignReceiptEvidenceAsync` update game-owned campaign pending/confirmed state but do not install the corresponding SDK confirmation association. The generic confirmation loop still depends on reward-receipt observations. A game-confirmed UI and an accepted outbox row are not proof that the SDK's public PendingCompletions is empty. Cross-layer rejection tests do not close this accepted path.

The coordinator first reproduces the gap through real native-SQLite admission and authenticated receipt/projection evidence, then freezes the smallest interface/transaction change. Use existing generic SDK confirmation interfaces when sufficient; add a generic transaction hook only if required. Game-specific slot/route/content logic stays in Unity. Wire/SQL changes remain backend-owned and reviewed; do not silently alter frozen G1 contracts.

Preserve owner/app/environment/view generation fencing, immutable operation/source association, evidence revisions and unrelated pending work. Campaign set revision is not automatically the private-feed revision. Receipt lookup must not advance a pull cursor, manufacture a feed event/reward, or treat bare push/empty pull as confirmation. Do not directly delete pending bytes to simulate a resolved lifecycle.

Include already-affected new-format saves whose game pending IDs were cleared by the old code while SDK pending remains. Recovery must use retained SDK/outbox identities plus real evidence; it must not rely only on the game pending list or allocate replacement operations. Make cross-layer reconciliation atomic where possible or durably recoverable with tested crash behavior.

Required tests: first clear; distinct attempt on a satisfied milestone with no new event; response loss; reopen before evidence; already-cleared game list; fault/rollback between steps; duplicate/reordered/foreign evidence; unrelated work preservation; and existing rejection regressions. Inspect the SDK's PendingCompletions and confirmation evidence together with game state/outbox, not merely database row existence. Retaining the accepted outbox receipt is permitted and not a failure.

## Packaging and integration after actual runtime changes

SDK workers return bounded reviewed patches/tests. One coordinator commits/pushes source and regenerated deterministic Git UPM payload, verifies delivery reachability and then serially updates Unity manifests/locks/import evidence. Keep runtime-source/package/artifact identities distinct. No DLL churn or Unity package-pin update for this documentation-only commit.

Run focused progression/storage tests, affected/full SDK suites, reproducibility/metadata/native/hash checks and the exact consumer tests required by the changed boundary. Fresh nonauthor review precedes downstream activation. Preserve issued identities, SQLite/checkpoints/outbox and queued command bytes during upgrades; no old gameplay or Review/browser data import. Final Android offline/reconnect/no-op/two-device proof remains necessary when affected.

## Ownership and limits

Use the shared queue's file/contract/resource leases. SDK subpatches and Unity adapters may run in parallel only after the coordinator freezes their boundary; workers never edit the same files or push independently. Default two or three useful children across the workstream, maximum five, not an SDK-only nested coordinator. Do not load all histories/peer repositories or build new orchestration tooling.

Preserve main, unrelated dirty work and the live Unity Editor; integrate in safe owned copies when necessary. No reset/force-push, credentials in reports, provider/billing escalation, registry publication, deployment or distribution. No game types/singletons/service locator in the SDK, account-linking UI, rewards, remote config/analytics or full shell. P3/G3 remains historical and broad P4 held. Update the shared checkpoint with evidence, keep genuine blocked work explicit and continue independent safe work without routine product questions.
