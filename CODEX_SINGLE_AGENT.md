# SDK entry — shared autonomous P3

The canonical workflow is [backend CODEX_ORCHESTRATOR.md](https://github.com/gindemit/game-platform-backend/blob/impl/platform-coordinated-2026-09-19/CODEX_ORCHESTRATOR.md). Open the backend as coordinator; use the latest `impl/platform-coordinated-2026-09-19` checkout of all three repositories.

A coordinator reads the short checkpoint and runs `python scripts/orchestrator_context.py --ready` from backend. A delegated worker reads only its supplied card/ticket and applicable scoped AGENTS.md; it must not launch another coordinator. One implementation writer across the workspace, bounded subagents, automatic independent reviews and automatic next-card continuation replace manual one-step sessions.

The coordinator alone commits/pushes task-owned changes and updates the shared queue/gates. Preserve original acceptance, existing source/artifact pins and unrelated work. No legacy gameplay import, fake Ready, main merge, deployment, publication or distribution. Finish P3/G3 and prepare the P4 handoff; do not implement P4. Missing peers/runtime or actual review remains a blocker, not an inferred pass.
