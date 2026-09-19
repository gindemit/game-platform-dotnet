# .NET SDK + real MrSquare agent — run one authorized phase

Updated 2026-09-19. This is the single-file entry point for BOTH the reusable SDK implementation and its real Unity adoption. Do not start a third independent Unity implementation session: this coordinator owns SDK and consumer integration together.

## Operator instruction

> Follow CODEX_CLIENT.md. Implement the currently authorized parallel phase in the SDK and MrSquare repositories, commit the results and handoff, and stop at its synchronization gate.

No previous chat or downloaded attachment is required. A separate parallel session owns the backend. The operator runs one synchronization session after both tracks stop, then relaunches these same entry files.

## Read and find the authorized phase

Read root/scoped AGENTS.md, [the binding coordination amendments](docs/implementation/COORDINATION.md), docs/implementation/README.md, AUDIT.md, RUNBOOK.md, FOUNDATION_TASKS.md, FEATURE_TASKS.md, CONTRACT_HANDOFF.json and execution-manifest.json. In Unity read Docs/Architecture/ClientImplementation/{README.md,HANDOFF.md,MIGRATION.md,LIVE_SLICE.md,TASKS.md}, plus accepted lifecycle/DI/contracts relevant to your scope.

Fetch the backend [coordination protocol](https://github.com/gindemit/game-platform-backend/blob/impl/platform-coordinated-2026-09-19/docs/implementation/coordination/PROTOCOL.md) and [state](https://github.com/gindemit/game-platform-backend/blob/impl/platform-coordinated-2026-09-19/docs/implementation/coordination/state.json). The persistent execution branch is `impl/platform-coordinated-2026-09-19` in all three repositories. Once that branch exists in backend, its `docs/implementation/coordination/state.json` is authoritative; never fall back to the planning seed because a fetch failed or a phase is inconvenient. Before its first creation, the initial planning seed authorizes P1 only. Resolve older planning links to the corresponding execution-branch documents for the current run; dated reports and immutable evidence references retain their original meaning.

Resolve current HEADs, planning PR branches, dirty files and active worktrees. The SDK planning branch is `docs/codex-client-execution-plan-2026-09-18`; Unity's is `docs/client-sdk-integration-plan-2026-09-18`. On first creation preserve current reviewed planning changes AND newer default-branch work in the separate execution branches. In particular, preserve newer animation-pack/visual work; the old Unity audit commit is not a reset target. Do not merge/push main or dispatch Android distribution, even if a general repository workflow normally uses main.

Use the current phase/epoch and approved immutable artifacts. Synchronizing/complete state prohibits parallel work; blocked state permits only its recorded remediation. Never unilaterally release a gate or use an old chat as authorization.

Before assigning workers, read your committed CLIENT_HANDOFF.json. If it already matches the current run/phase/epoch and is `ready_for_sync` with `quiescent: true`, this track has finished: STOP and direct the operator to backend CODEX_SYNC.md after the backend also stops. An unchanged parallel state is the previous phase release, not authorization to repeat P2 or move to P3. Resume only after a later committed release or explicit central same-phase remediation; the latter permits only its listed scope. An interrupted in-progress track can still resume authorized unfinished work.

At the P2/epoch2 boundary, read the backend [G2 source review and repair checklist](https://github.com/gindemit/game-platform-backend/blob/impl/platform-coordinated-2026-09-19/docs/implementation/coordination/G2_REVIEW_2026-09-19.md). Both matching handoffs now exist; the old backend-unavailable observation in the dated client handoff is historical. The C# opaque-token encode/decode defect and real two-runtime gate remain unresolved. The checklist is review input, not a P3 release or permission for a parallel client session to edit backend/state/pins. Preserve failed corpora and distinguish source, artifact, handoff and Unity bundle revisions.

## Your scope in each phase

| Phase | SDK + real consumer work | Mandatory stop |
| --- | --- | --- |
| P1 | CL-001 interfaces, CL-006 SQLite qualification, CL-014 diagnostics and INT-001 complete caller/save/import inventory. Prepare CL-003 schema review requirements and safe dependency-ready extraction work; actual core wire implementation waits for G1. | G1: foundation evidence, client/Unity contract requirements and candidate-review findings are committed. Backend codecs are not a prerequisite to requesting schema review. |
| P2 | Finish schema-only CL-003 against G1, implement CL-004 and the codec producer/consumer modes of CL-016. Continue dependency-ready storage, transport, lifecycle and early real SDK import (CL-013/INT-002) against approved interfaces. Review proposed minimal live-slice feature schemas. | G2: executable C# codec and real fixture commands/artifacts exist. Do not wait for a finished live backend merely to provide a codec harness. |
| P3 | Implement the minimal durable account/bootstrap/sync/value/progression path, real native bundle and actual Unity entry/completion/UI integration; prepare INT-010 with all actual prerequisites. Keep live reward activation blocked without approved policy. | G3: the real consumer and isolated device/service test prerequisites are ready for joint BE-025/INT-010 acceptance. |
| P4 | Complete remaining approved feature/adoption scope, independent second-game proof and INT-016–020 acceptance preparation. Request review before inventing any new wire schema. | G4: final agreed-scope verification, or an earlier requested schema-delta checkpoint. |

These are outer boundaries, not a substitute for the 52-task DAG. Only execute dependency-ready owned work within the released phase. Preparation of a subpart does not mark its entire parent task complete. An interrupted phase resumes from its committed work. Use COORDINATION.md's explicit schema/runtime and early-device-builder amendments rather than reproducing the old acceptance cycles.

## Implementation and ownership

Write SDK and authorized Unity integration files only during parallel phases. Backend canonical contracts, migrations and central release state are read-only. Send proposed deltas through the handoff. Assign one Unity integration owner for asmdefs, imports/manifests, scenes, composition, save migration and isolated builds; one storage owner and one packaging owner. Use isolated worktrees and active shared-file locks. Use actual subagent tools only when available, otherwise execute serially without claiming agents were spawned.

Implement reusable behavior, not another scaffold or fake-only demo. Keep game/Unity/provider/serializer dependencies out of forbidden portable layers. Preserve one canonical type per responsibility, semantic/provider identifiers, old save/checkpoint formats, complete first-online bootstrap, later same-account offline play, atomic progress/sequence/immutable outbox, pending-safe reset, manual constructor injection and quiescent account replacement. Server-confirmed receipts/projected state determine value; the client does not mint rewards.

Prioritize the real early import and first live slice rather than postponing MrSquare adoption until all sixteen features are finished. Do not rewrite unrelated visuals, animation, difficulty or deterministic mechanics. Missing native/device/editor/provider access is blocked/unrun evidence, never fake success. Product trust decisions stay explicit; no historical reward replay or automatic account merge.

Run the existing SDK planning checks and baseline/task tests; run Unity documentation/runtime/device checks appropriate to actual changes and available environments. Record exact commands, exit codes, nonzero selected test counts and raw reports. Desktop SDK tests cannot mark Unity/device integration complete. No publication, deployment, production migration, secret changes, real-money purchase, main merge or force push.

## Commit, hand off, stop

Commit implementation and evidence in both repositories first. Update [CLIENT_HANDOFF.json](docs/implementation/coordination/CLIENT_HANDOFF.json) in the SDK with the observed phase/epoch, actual SDK AND Unity implementation commits, completed versus partial task IDs, tests, artifact/schema hashes, blockers, required backend actions and next gate. Commit the handoff afterward so all referenced commits already exist. Push only the execution/task branches.

Set `ready_for_sync` only when phase review inputs are ready; it does not mean the gate passed. Set `blocked` when deliverables cannot be produced. Set `quiescent: true` only when all workers stopped and branch writes finished. Do not edit central state, continue into the next phase, wait/poll for the other chat, or declare that the backend implemented something without evidence.

End with the handoff commit, actual results and `STOPPED AT G<n>` (or the named delta/blocker). Tell the operator to run backend CODEX_SYNC.md after BOTH tracks have stopped. On the next launch of this same file, fetch the committed release and apply the finished-handoff guard before resuming only what it authorizes.
