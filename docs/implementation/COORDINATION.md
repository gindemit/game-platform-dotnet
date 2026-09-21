# Binding coordination amendments — 2026-09-19

This scheduling addendum is part of the implementation instructions, not a future suggestion. Read it before applying the 2026-09-18 RUNBOOK.md, FOUNDATION_TASKS.md, external-gate descriptions and Unity task launch text. It explicitly replaces only the conflicting gate/launch wording described below; accepted architecture and all other detailed acceptance requirements remain in force.

Start with [CODEX_CLIENT.md](../../CODEX_CLIENT.md). The canonical cross-repository [protocol](https://github.com/gindemit/game-platform-backend/blob/docs/backend-codex-execution-2026-09-18/docs/implementation/coordination/PROTOCOL.md), central phase state and [CODEX_SYNC.md](https://github.com/gindemit/game-platform-backend/blob/docs/backend-codex-execution-2026-09-18/CODEX_SYNC.md) live in backend. Planning links are discovery links; once `impl/platform-coordinated-2026-09-19` exists, read its current protocol/state and exact approved artifact references.

## Launch boundaries

Two parallel tracks run P1 foundations, stop for G1 schema approval, run P2 core codecs, stop for G2 real peer exchange, run P3 first live implementation, stop for G3 joint game acceptance, then run P4 remaining scope and stop for G4 final verification. Reuse the same entry files for fresh sessions. The backend state, not an old conversation, tells each new session where to resume. Neither track changes central state. A synchronization session writes across repositories only while BOTH tracks are paused with committed quiescent handoffs.

An old prompt's instruction to continue independent work is bounded by the current phase. It does not permit silently crossing a synchronization gate. Feature schema deltas require an earlier reviewed checkpoint when needed; only affected approved work is released afterward, in the same phase.

## CL-003 / PEER-CORE: schema acceptance replaces the runtime prerequisite

Replace the old CL-003 acceptance requirement for compatible implementation revisions with: approved canonical operation/schema/semantic-vector artifacts, versions, field mappings, hashes, reviewers, reviewed mirror commit and compatibility policy. Real runtime implementation revisions may remain null/unverified. CL-003's schema-only deliverables and tests can complete without codecs; PEER-CORE does not require peer-produced binary fixtures.

This also replaces RUNBOOK.md section 3's combined schema/peer-binary contract gate. The existing dependency CL-003 -> CL-004 / CL-016 is retained and is now implementable without a semantic cycle. Review a BE-002 candidate before the entire backend task is complete; do not require mutually completed peer tasks just to exchange a candidate.

The change does NOT approve current draft contracts or mark CL-003 complete. Complete operation schemas are still missing until actual G1 work. It does NOT waive A02: C# -> TS and TS -> C# execution, hostile-input cases and normalized fingerprint equality remain G2 / BE-015 / CL-004 evidence. Null runtime references are never wildcard runtime compatibility.

## CL-004 / CL-016: executable codec first, live harness later

Implement codec producer/consumer CLI modes against G1 without requiring a live backend. Publish actual bytes, decoded semantic expectations, versions and commands for G2. The service/fault-injection portion of CL-016 remains incomplete until its actual backend prerequisites exist; G2 must not wait for that later service evidence. Keep full task status partial until all applicable delivery requirements are demonstrated. Desktop exchange does not certify Android AOT or feature families not included in the exchanged corpus.

## PEER-LIVE-SLICE / INT-010: readiness is not completed joint acceptance

Use BE-016/024 and their real dependencies, approved policy and seed/start/state-inspection commands as backend readiness. Never require completed BE-025 as a predecessor to INT-010. Prepare both tracks, then G3 performs BE-025 and INT-010 as one joint acceptance run, with evidence attached to both authoritative task ledgers. SDK compilation, fake service success and healthz are not the live journey.

## INT-004 / INT-009: obtain the isolated builder before device probes

The single consumer integration owner prepares the isolated device builder/runner early as prerequisite-free subwork of INT-009 while preparing INT-004. Allocate any shared editor/build files through the existing owner and update the authoritative ownership manifest/card before changing that formal scope. Do not require INT-009 to be wholly complete just to make its builder available: INT-009 -> INT-006 -> INT-004 otherwise creates an acceptance-ordering loop.

INT-004 still requires its real applicable HTTPS/credential/pause-resume device evidence; INT-009 still requires its final bundle/native/AOT/device evidence when its full predecessors exist. INT-010 cannot pass without those device prerequisites. Early builder preparation does not mark either task complete. Never install over a normal game's identifier/data or substitute desktop success for a missing device.

## Task mapping and evidence

The updated [CONTRACT_HANDOFF.json](CONTRACT_HANDOFF.json) maps PEER-* requests to actual backend plan IDs; the canonical protocol supplies the EXT-* reciprocal mapping. These IDs prove only task existence, not completion. BE-002 <-> CL-003 is schema review; BE-005/015 <-> CL-004/016 is codec evidence; BE-025 <-> INT-010 is joint runtime acceptance.

Backend execution.json owns BE task status. This repository's execution-manifest.json owns CL/INT and feature status. The new [CLIENT_HANDOFF.json](coordination/CLIENT_HANDOFF.json) is a session envelope containing exact SDK/Unity commits and evidence references, not a competing task ledger. Central state tracks phase releases only. Formal task/DAG/ownership edits require corresponding existing-card/manifest/checker changes and validation; no duplicate DAG is introduced by this addendum.

## Non-negotiable boundaries

No automatic main merge, Android distribution, deployment, publication, production data migration, secret changes or force push. Preserve newer Unity work when incorporating the planning branch into the separate execution branch. `DEC-CONTENT-VALIDATION` is accepted only for the bounded unvalidated casual test tier. The former `DEC-LEGACY-TRUST` question is resolved: no legacy gameplay PlayerPrefs/checkpoint migration is supported because there are no production users to preserve; old gameplay saves are ignored and obsolete import/staging code is removed. A client checkpoint signature is not trusted outcome proof, and fixture success does not authorize real grants or provider activation.

At a phase boundary commit actual results, then a handoff referencing those already existing commits; mark workers quiescent and stop. On failure record blocked/unrun evidence and bounded requests. Only CODEX_SYNC can approve the next phase, and only after inspecting/testing the corresponding evidence.
