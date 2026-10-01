# SDK handoff — campaign and navigation continuation

2026-10-01. Single execution authority: [backend campaign entry](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/README.md), [current checkpoint](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/checkpoint.md), [implementation review](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/REVIEW-2026-10-01.md) and [next-session prompt](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/NEXT-SESSION-2026-10-01.md). Goal CAMPAIGN_LOCAL; bounded PLAYER_SHELL_FOUNDATION continuation only when selected. No independent SDK coordinator, closed P3 dispatcher or repeated attachment/product questions.

## Current source versus delivery

| Layer | Audited pin | Actual state |
|---|---|---|
| SDK main before review docs | `6a4916dec233ae583eb12379260adf8644e927c2` | Reverts ON-20 `2c431522241f375c6d8fbe6a12b70c3581bb974f`; CI 36797573578 passed |
| Accepted-receipt runtime repair | `5c7a02a` | Retained source; not an open missing implementation |
| Unity-consumed Git UPM | `4eb678e43e9c37ff61fdd94bb8cfb1319301bca0` | Unchanged; does not contain ON-20 |
| Unity inspected | `7eaa73c` | Completion-lock/admission fixes and isolated shell/metadata source; no shell activation |
| Backend inspected | `1bb0896` | Local retained same-principal fixture demonstrated; CI 36797581778 passed |

Fetch later main descendants and verify their actual CI. Documentation-only commits need no package repin. This remote review inspected source/CI logs but did not rerun .NET, native SQLite, Unity or devices.

## Immediate SDK work: RV-02 and RV-03

Do not blindly reapply ON-20. [Failed Windows run 36796370596, attempt 3](https://github.com/gindemit/game-platform-dotnet/actions/runs/36796370596/job/110163025583) built successfully and ended with 668 passed / 1 failed / 669 total. `Cl008AtomicOutboxTests.AbruptProcessTerminationRollsBackPreCommitAndPreservesPostCommit` failed with SQLite disk-I/O error during reopen at line 139, not in a navigation assertion.

The crash harness kills a dotnet-test launcher tree and waits for that launcher before reopening the database. [Process.Kill documentation](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.kill?view=net-9.0) warns that this need not establish descendant exit. An actual DB-owning testhost retirement race is plausible, not proven. Run bounded clean-parent/reverted-main/candidate A/B checks with actual child PID/phase/termination diagnostics. Preserve hard crash, precommit rollback, postcommit survival and original atomicity assertions. No skipped test, graceful-disposal substitute, unexplained sleep or broad production I/O retry to obtain green. Revert-green is containment, not a causal explanation.

In the reverted navigation patch, a non-OperationCanceledException loader failure leaves `loading=true`. A cancellation-ignoring loader returning after caller cancellation also reaches the early Stale path without clearing current loading state. Reproduce and repair both with version-aware terminal cleanup; stale work must not clear a newer transition. Preserve configured Root, game-neutral DestinationRoute/DetailRoute, modal-first single-action Back, bounded logical history/caller return, rapid replacement, cancellation, dispose and retirement. C# findings were source-derived here; require RED/GREEN tests before re-delivery.

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
