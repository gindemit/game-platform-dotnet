# Client entry point — P3 now uses one cross-repository agent

Updated 2026-09-19 following the operator's explicit request for one sequential implementation session.

Read [CODEX_SINGLE_AGENT.md](CODEX_SINGLE_AGENT.md), then follow the canonical backend entry point it identifies. Do not start a separate SDK, backend, Unity or synchronization worker for P3. The SAME agent implements dependency-ready backend, portable SDK and actual game integration, then performs the explicit G3 verification boundary when its prerequisites exist.

The backend execution-branch state remains the phase/epoch/gate authority. Its legacy `mode: parallel` P3 release does not require or authorize multiple sessions under the current scheduling amendment. Read the current backend PROTOCOL.md and P3_SESSION_POLICY.json. A synchronizing, blocked, complete, different-run or later-phase state must not be ignored.

[CODEX_CLIENT_PARALLEL.md](CODEX_CLIENT_PARALLEL.md) preserves the previous entry point verbatim for task-scope and historical reference. Its instructions to start/wait for other sessions, write only two repositories, or restart parallel tracks are superseded for the current P3 profile. Its architectural constraints and acceptance requirements remain binding. SDK COORDINATION.md and existing task cards remain technical references, not permission to restore the old session topology.

No task, runtime or gate is marked complete by this routing change. Do not repeat P2 because an older header/handoff still describes it. Preserve the existing task ledger, real evidence, unrelated changes and all game save identities. Use only the execution/task branches; no main merge, package publication or deployment.
