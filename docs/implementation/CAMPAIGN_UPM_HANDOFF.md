# SDK handoff — current campaign follow-ups

2026-10-01. Current coordination lives in the backend [entry](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/README.md), [checkpoint](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/checkpoint.md), [follow-up review](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/FOLLOWUP-REVIEW-2026-10-01.md), [board](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/overnight-queue.json) and [next-session plan](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/NEXT-SESSION-2026-10-01.md). GOAL=CAMPAIGN_LOCAL; PLAYER_SHELL_FOUNDATION only when selected by the owner. Root and scoped AGENTS continue to apply.

## Verified baseline and delivery distinction

| Layer | Reviewed baseline | State |
|---|---|---|
| SDK | `04d959c11f23789e889e15f4c98c96758662ac94` | ON-20 restored in `4e9f925`, RV-03 transition-aware loading cleanup under the outcome lock; crash diagnostics and terminal-auth characterizations retained |
| SDK CI | Run 36827226808 | Prior review inspected 680 passing C# tests, zero failed/skipped, 66 Python tests and successful build/package steps; intermittent durability remains unresolved |
| Unity SDK Git UPM | `4eb678e43e9c37ff61fdd94bb8cfb1319301bca0` | Unchanged and lacks restored ON-20; source delivery is not consumer-package delivery |
| Unity | `0dad792092a67783c589d0b0085e4f01949504e0` | RV-04/05/06 and store repair integrated; additional source-read characterization remains; no check-runs at this baseline |
| Backend | `8e5b24d161db55aaf6300bc3209bca8a1b3ef83e` | validate/postgres passed; fixture was last reported running, not checked live in this documentation session |

These are evidence pins, not reset targets. Fetch newer main and preserve unrelated work. Documentation-only descendants need their own CI observation but no regenerated DLLs. ON-20's reported 19 navigation tests include the earlier two RED cases. Restore is complete; package delivery is still open.

## Auth follow-up: FU-AUTH-DESIGN and FU-AUTH-RECOVERY

The existing lifecycle returns RecoveryRequired without HTTP from RecoveryRequired/RefreshPending, even with a retained refresh secret. HTTP 500 or uncertain refresh delivery can produce that state. The exact event on the retained phones is not established. The five added characterizations describe existing behavior rather than a chosen new policy.

A separate explicit owner decision is required before changing authentication semantics. Until that decision is received, design, characterization and independent safe work can proceed. A stored launcher template is not a decision record.

Recommended implementation after approval: keep normal startup fail-closed, and let an explicit recovery action attempt one finite-time single-flight refresh for the same known principal. Preserve issuer/backend context, exact subject, account generation and CAS fencing. Persist rotated credentials before authenticated publication; real provisioning/bootstrap/pull still establishes Ready. Cover recoverable RecoveryRequired and interrupted RefreshPending while retaining unknown-signup, corrupt/missing-secret and conflict restrictions.

Retain all account/installation/stream identities, SQLite, outbox, attempts, checkpoints and immutable commands. New-account provisioning or data deletion is not an acceptable recovery fallback. Rejected credentials, wrong identity and uncertain persistence remain visible failure states.

Tests cover success; server/uncertain failure followed by recovery; rejected/missing/corrupt credentials; subject mismatch; duplicate/concurrent intents; CAS/account retirement; interruption before/after credential persistence; process restart. Exercise both AuthenticateAsync and in-session GetAsync/RefreshAsync, including version handling after temporary failures. Preserve NotSent/offline and no-implicit-signup assertions. Confirm the pinned provider's behavior using its official documentation and isolated test credentials. Fresh-install tests do not qualify retained-account recovery.

## Durability follow-up: FU-DURABILITY / RV-02

The instrumented Windows failure 36825045338 recorded child exit, no observed file holder and a postcommit SQLite IOError. This does not support the original live-child explanation for that failure, but the native/OS/filesystem/harness cause remains unproven. Later green runs do not close it; results across changing revisions are not a controlled failure rate.

The harness already has child PID/exit wait, lock probes, process census, extended-error and copy-open diagnostics. Use them rather than repeat their implementation. Predeclare a small exact-ref comparison with equivalent diagnostic overlays and pinned toolchain/native/runner provenance. Preserve the hard-kill precommit rollback and postcommit durability assertions.

Retain sanitized synthetic database/WAL/SHM evidence before diagnostic opens change recovery state. Upload failure diagnostics even when the test step fails, separately from successful package artifacts. The previously inspected failing job skipped normal artifact upload. Real device data and credentials must not enter artifacts. Record all attempts, not selected passes. Permission-restricted operations remain restricted until authorized through the normal mechanism.

## Build environment and package follow-up: FU-TOOLCHAIN / FU-UPM

Inspect current global.json and packaging scripts. The reviewed CI used .NET 9.0.203, whereas the owner Mac reportedly lacked the required SDK. Resolve the exact SDK through an official isolated installation or existing authorized CI environment; keep framework and dependency pins unchanged merely to accommodate the host.

After the selected runtime repairs and required SDK validation, generate canonical Git UPM through existing scripts. Prove the required two-clean-copy reproduction and obtain fresh nonauthor review of managed/native hashes, importers, GUIDs, licenses, AOT/link/lifecycle metadata and source provenance. An earlier successful CI artifact is not proof of the two-copy requirement. Packaging rehearsal can happen earlier; final delivery must include the selected approved repairs.

One integrator serially updates Unity manifest/lock and verifies the resolved import. Canonical runtime source stays in src; generated UPM remains delivery, not a fork. Documentation-only edits require no package churn. The previously staged macOS SQLite library is qualification-only, not evidence of ordinary Editor support.

## Preserved campaign semantics and acceptance

Accepted-receipt repair 5c7a02a, Unity completion-lock 836fee6 and the R03/R04/R05 source repairs are retained. CMP-R06 remains accepted at its recorded local scope: real retained-host cycle and independent same-account admission. The newly inapplicable fresh-zero fixture assertion is a separate FU-RETAINED-FIXTURE task, not grounds to reset the retained account.

ProgressionService.ConfirmAcceptedReceiptAsync associates real accepted evidence with public PendingCompletions without fabricating rewards. Preserve owner/app/backend/view generation fencing, immutable operation/business-source identity, evidence revisions and unrelated pending work. Campaign-set revision is not automatically private-feed revision. Receipt lookup/push acknowledgement never advances pull checkpoints or manufactures feed events/value. Already-affected new-format state must recover using retained identities/evidence even where the game pending list was cleared earlier.

Affected regressions retain first clear, distinct already-satisfied without a new event, response loss with unchanged identity, reopen before evidence, already-cleared game list, fault/rollback between SDK/game writes, duplicate/reordered/foreign evidence, rejection and unrelated work. Inspect SDK/game pending state and accepted evidence together; a retained accepted receipt is permitted.

Final six-role freeze follows the selected repairs, reviewed Unity repin and fresh full Unity validation. Existing f918a7e integrity and earlier phone runs are not final-source acceptance. Retained recovery, visible browser input, complete Android traversal, loss/crash boundaries and coinstallation remain at their original parent scope. Default-off shell preparation is distinct from ordinary activation; campaign and configured-shell acceptance gate activation.

## Ownership and history

Use main, one coordinator/git integrator and existing exact lease rules. Preserve unrelated work, issued identities and pending data. Keep the SDK game-neutral, constructor-injected and free of a duplicate navigation framework. Root permissions, publication restrictions and historical P3/G3/broad-P4 boundaries remain unchanged.

Run affected source/package/full-checkout documentation checks and report actual pushed-head CI, including no-run. Reconcile the shared board/checkpoint and Unity handoff. The [preceding handoff](https://github.com/gindemit/game-platform-dotnet/blob/04d959c11f23789e889e15f4c98c96758662ac94/docs/implementation/CAMPAIGN_UPM_HANDOFF.md) preserves dated pins/results. This update is documentation only: no runtime repair, package change, physical rerun or gate closure is claimed.
