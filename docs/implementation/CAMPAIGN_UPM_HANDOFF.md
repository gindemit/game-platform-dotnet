# SDK handoff — current local campaign repair

Single execution authority: [backend campaign entry](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/README.md), [checkpoint](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/checkpoint.md) and [repair plan](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/CMP-10-REPAIR-PLAN.md). Goal `CAMPAIGN_LOCAL`. Both external review attachments are incorporated; do not request them or launch an independent SDK coordinator/P3 dispatcher.

## Current state (2026-09-30)

| Layer | Pin | Status |
| --- | --- | --- |
| SDK runtime source | `5c7a02a` (accepted-receipt confirmation) | Implemented, 18/18 focused and full SDK suite green |
| SDK Git UPM delivery | `4eb678e` (unchanged; SDK CI `36631966644` passed) | Imported by Unity manifest |
| Unity main | `836fee6` | Implemented, locally tested |
| Backend main | `dad903d` | Gated same-principal admission fixture added; live runs unrun |

Implemented: `5c7a02a` associates accepted completion receipts with pending SDK state, so `ProgressionService.ConfirmAcceptedReceiptAsync` clears public `PendingCompletions` without a reward-receipt observation. Unity `836fee6` releases the completion lock during campaign evidence reads: `ReconcileCampaignEvidenceCoreAsync` collects accepted receipt identities under the lock, reads SDK evidence unlocked, then reacquires and revalidates identity and feed revision before confirming.

Locally tested: nonauthor review PASS on `836fee6`; native-SQLite PlayMode 6/6 (`CampaignEvidenceLockTests`, `CampaignDurableNextTests`); EditMode 88/88 focused.

Integration-unqualified: authenticated HTTP acceptance, Android device offline/reconnect/no-op acceptance and same-account two-device races. Backend `tests/hosts/supabase/admission-*.mjs` and `retained-restart.test.mjs` exist but have no live run in this session. No SDK runtime change happened on 2026-09-30; the `4eb678e` pin stays.

Canonical runtime source remains src; UPM is generated delivery, not a fork. Preserve managed dependency closure, native importers, GUIDs, licenses, AOT/link/lifecycle data and explicit assembly references. The normal package includes reviewed Android ARM64/Windows native targets; the pinned macOS SQLite library was staged only for disposable qualification. Do not silently infer normal Mac Editor/native support from that test setup.

## Historical (2026-09-29): pre-repair delivery state

Unity source `8431266e54a25f01d000ab8cf946e19981508652` consumed delivery pin `3008b3576052e6192ceeeeb236bdd8bae3b94efc`; runtime source `32f2ba6c25f540db32eab9d6a9d129d81a5c4aea` added atomic game-state reconciliation to completion rejection. Older source `208aab7`/package `c33ef45` and CMP-03 integration are completed milestones. The copied Unity SDK Assets payload was removed.

The [follow-up report](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/CMP-10-followup-2026-09-29.md) recorded 18/18 focused SDK and 662/662 full tests; baseline CI run `36576253188` on delivery `3008b35…` passed.

## Historical (2026-09-29): CMP-R02 gap, now repaired by `5c7a02a`/`836fee6`

`ProgressionService` provides real generic local admission, pending state, rejection and confirmation; it is not a campaign selector or puzzle validator. Its transactional rejection callback and rollback/reopen tests remain in place.

The gap was accepted/no-op confirmation: at Unity `8431266`, `ReconcileCampaignEvidenceCoreAsync` and `ApplyCampaignReceiptEvidenceAsync` updated game-owned campaign state but did not install the SDK confirmation association, so an accepted outbox row was not proof that `PendingCompletions` was empty. The rules below still bind any further change on this path.

Preserve owner/app/environment/view generation fencing, immutable operation/source association, evidence revisions and unrelated pending work. Campaign set revision is not automatically the private-feed revision. Receipt lookup must not advance a pull cursor, manufacture a feed event/reward, or treat bare push/empty pull as confirmation. Do not directly delete pending bytes to simulate a resolved lifecycle.

Already-affected new-format saves whose game pending IDs were cleared by the old code while SDK pending remains recover through retained SDK/outbox identities plus real evidence, never from the game pending list alone or by allocating replacement operations.

Required tests: first clear; distinct attempt on a satisfied milestone with no new event; response loss; reopen before evidence; already-cleared game list; fault/rollback between steps; duplicate/reordered/foreign evidence; unrelated work preservation; and existing rejection regressions. Inspect the SDK's PendingCompletions and confirmation evidence together with game state/outbox, not merely database row existence. Retaining the accepted outbox receipt is permitted and not a failure.

## Packaging and integration after actual runtime changes

SDK workers return bounded reviewed patches/tests. One coordinator commits/pushes source and regenerated deterministic Git UPM payload, verifies delivery reachability and then serially updates Unity manifests/locks/import evidence. Keep runtime-source/package/artifact identities distinct. No DLL churn or Unity package-pin update for documentation-only commits.

Run focused progression/storage tests, affected/full SDK suites, reproducibility/metadata/native/hash checks and the exact consumer tests required by the changed boundary. Fresh nonauthor review precedes downstream activation. Preserve issued identities, SQLite/checkpoints/outbox and queued command bytes during upgrades; no old gameplay or Review/browser data import. Final Android offline/reconnect/no-op/two-device proof remains necessary when affected.

## Ownership and limits

Use the shared queue's file/contract/resource leases. SDK subpatches and Unity adapters may run in parallel only after the coordinator freezes their boundary; workers never edit the same files or push independently. Default two or three useful children across the workstream, maximum five, not an SDK-only nested coordinator. Do not load all histories/peer repositories or build new orchestration tooling.

Preserve main, unrelated dirty work and the live Unity Editor; integrate in safe owned copies when necessary. No reset/force-push, credentials in reports, provider/billing escalation, registry publication, deployment or distribution. No game types/singletons/service locator in the SDK, account-linking UI, rewards, remote config/analytics or full shell. P3/G3 remains historical and broad P4 held. Update the shared checkpoint with evidence, keep genuine blocked work explicit and continue independent safe work without routine product questions.
