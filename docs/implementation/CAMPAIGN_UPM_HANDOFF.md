# SDK handoff — campaign and navigation continuation

2026-10-01. Single execution authority: [backend campaign entry](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/README.md), [current checkpoint](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/checkpoint.md), [implementation review](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/REVIEW-2026-10-01.md) and [next-session prompt](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/NEXT-SESSION-2026-10-01.md). Goal CAMPAIGN_LOCAL; bounded PLAYER_SHELL_FOUNDATION continuation only when selected. No independent SDK coordinator, closed P3 dispatcher or repeated attachment/product questions.

## Current source versus delivery

| Layer | Pin | Actual state |
|---|---|---|
| SDK main | `55e14fe` (2026-10-01) | ON-20 restored as `4e9f925` with RV-03 lock-scoped loading cleanup; Cl008 crash-harness diagnostics `8e6c3e8`, `cbd36b6`, `55e14fe` (test-only) |
| SDK CI on those pushes | runs 36824490074, 36825045338, 36825524754 | failure, failure, success; every failure was `Cl008AtomicOutboxTests.AbruptProcessTerminationRollsBackPreCommitAndPreservesPostCommit` postcommit reopen, 674/675 then 675/675 |
| Accepted-receipt runtime repair | `5c7a02a` | Retained source; not an open missing implementation |
| Unity-consumed Git UPM | `4eb678e43e9c37ff61fdd94bb8cfb1319301bca0` | Unchanged; does not contain ON-20. Regeneration held: no .NET 9 SDK on the owner Mac for the two-clean-copy reproduction |
| Unity main | `f918a7e` | RV-04 shell characterization, RV-05 metadata boundaries, RV-06 test-auth fail-closed; campaign qualification continues on the `4eb678e` pin |
| Backend main | `328ea62` | Retained fixture `gp-be003-campaign20260930` resumed with full digest match on 2026-10-01 |

Documentation-only commits need no package repin. The ON-20 source is delivered and CI-green on main but is not package-delivered; subsequent package regeneration still requires fresh nonauthor review of the generated payload, the two-clean-copy hash/native/metadata check and a serial Unity repin by the integrator.

## RV-02 durability: what the Windows diagnostics established

The restored harness records the DB-owning child process id in the signal file, waits bounded for its exit, probes file locks and surviving processes, and enriches the reopen failure. Two failing Windows runs reported `phase=postcommit childExitWaitMs=0 childAliveAtReopen=False childPidReused=False`, all three SQLite files `locked=false` at 0, 250 and 2000 ms, and an empty `testhost/dotnet/vstest.console/datacollector` census; `PRAGMA journal_mode = WAL` is the first statement that touches the retained WAL and shm and it returns `IOError`. The third run passed with identical code. The launcher/testhost race hypothesis is therefore refuted, no lingering lock was observed, and navigation code is not on the failing path. The extended SQLite error code and the copy-open comparison added in `55e14fe` have not yet fired on a failing run. Bounded repeated A/B dispatches were not run from the agent session (classifier denied bulk `workflow_dispatch` and a comparison ref push); run them as the owner if more samples are wanted. Do not skip the test, soften the atomicity assertions, add happy-path sleeps or retry I/O errors in production code.

## RV-03 navigation: restored

`NavigationCoordinator` keeps `Root`, returns to it from `ReturnHome`, treats Back during a delayed load as the one action that cancels it, and clears `loading` inside the same lock that decides Stale/Unavailable/Applied, in the cancellation catch and in a catch-all rethrow, only when the finishing operation is still the current transition. `ShellDestinationNavigationTests` (19 cases with Cl012) cover loader exception, cancellation-ignoring late success, older operation not clearing a newer load, Back from inside a load, Back after Applied, modal-first Back, bounded history, DetailRoute caller return and 129-character key rejection; two of them failed before the fix. Local runs used the net8 target override from a `/tmp` working directory because only .NET 8 is installed; the Windows CI build and full suite are the authoritative runs.

## Campaign evidence already achieved and still needed

Unity `836fee6` releases the completion lock for evidence transport, then reloads/revalidates accepted identity/feed revision and current progression state before association. Prior native PlayMode 6/6 and focused EditMode 88/88 with RED/review evidence are retained, not rerun here. Do not redo this repair or the completed R03/R04/R05 source changes merely because parent gate dependencies remain blocked.

The preceding session demonstrated the retained fixture cycle and two `be6df32` test-auth Player installations reaching real Ready as one principal with distinct streams. First clear, distinct already_satisfied, initialized offline completion/force-stop/Continue/reconnect and verified-prefix Next passed at that recorded scope with zero rewards. CMP-R06 remains done locally. The separately gated retained-restart script remains live-unrun. Four `69258b9` role builds and later `be6df32` test-auth device results are different source/configuration scopes, not a matching final current set.

Final response-loss/crash-boundary injection, 40/40 Android, Review coinstallation, exact remaining browser journeys and both SDK/game pending representations remain explicit in the parent queue/checkpoint. Do not invent device acceptance from SDK tests or old artifacts.

## Preserved confirmation and package invariants

Canonical runtime source remains src; UPM is generated delivery, not a fork. Preserve managed closure, native importers, GUIDs, licenses, AOT/link/lifecycle data and explicit assembly references. The pinned macOS SQLite library was staged only for disposable qualification; this does not establish ordinary Mac Editor support.

`ProgressionService.ConfirmAcceptedReceiptAsync` associates accepted completion evidence with public PendingCompletions without fabricating reward observations. Preserve owner/app/backend/view generation fencing, immutable operation/business-source identity, evidence revisions and unrelated pending work. Campaign-set revision is not automatically private-feed revision. Receipt lookup and push acknowledgement never advance pull checkpoints or manufacture feed events/rewards. Do not directly erase pending bytes or allocate replacement operations to simulate recovery. Already-affected new-format state must recover through retained SDK/outbox identities and real evidence even when the game pending list was cleared earlier.

Required affected regressions retain first clear, distinct satisfied milestone without a new event, lost response with unchanged identity, reopen before evidence, already-cleared game list, fault/rollback between SDK/game writes, duplicate/reordered/foreign evidence, unrelated work and rejection. A retained accepted outbox receipt is permitted; inspect public PendingCompletions, confirmation evidence and game pending state together.

After genuine runtime changes, run focused tests, affected/full SDK suites, deterministic reproduction/metadata/native/hash checks and fresh nonauthor review. One integrator commits/pushes reviewed source and regenerated Git UPM, verifies reachability/actual CI, then serially updates Unity manifest/lock and exact import evidence. Consumer/device checks remain necessary for changed payloads. Preserve issued identities, credentials, SQLite/checkpoints/outbox and queued command bytes during replacement; no old gameplay/Review/browser import or database reset.

## History and ownership

The [immutable preceding SDK handoff](https://github.com/gindemit/game-platform-dotnet/blob/6a4916dec233ae583eb12379260adf8644e927c2/docs/implementation/CAMPAIGN_UPM_HANDOFF.md) retains earlier source/delivery pins and test scopes. Its September 30 no-live-fixture/current-head statements are superseded. Read history only for a named question, not as startup context.

Follow the shared exact leases: default two or three useful workers, maximum five across the workstream, no nested coordinator or child pushes. SDK workers do not edit Unity package imports/scenes. Preserve main, unrelated work and live Editors; no reset, force-push, secrets in reports, provider/billing escalation, registry publication, deployment or distribution. Keep SDK game-neutral with constructor injection, no singleton/service locator or second generic navigation framework. Historical P3/G3 and broad P4 hold remain. Reconcile actual source/package/device states in the shared checkpoint after reviewed work; a documentation update closes no runtime gate.
