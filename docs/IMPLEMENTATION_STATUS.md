# SDK current status

Current, 2026-09-25: SDK code head `45a5ce6` (bundle manifest `9dd2f1b3d105d2a3fe0e901d8b941548c11b47458806bf3e84c6d33b4df8b612`, not yet imported into Unity). Historical bounded G3 epoch-3 pass is unchanged; overall P3 is PARTIAL (review remediation open, R05/R10 pending); P4 is NOT released. Start at [CODEX_SINGLE_AGENT.md](../CODEX_SINGLE_AGENT.md). The backend `docs/work/p3-review/queue.json` owns cross-repository next actions; old handoff chronology is not a current work order.

Implemented bounded foundations include production command fingerprint/push, private-sync revision-zero/boundary/observation handling, SQLite v7 invalidation, atomic durable receipt/feed correlation, CL-108 game transaction callback, Supabase anonymous-auth CAS and terminal StopAsync. Recorded Release tests: 530/530. These components still need complete real-game composition/acceptance.

SDK bundle currently imported in Unity: source `64eb2c8`; manifest `3ab92df7c34b450275d0b49ddd79f50895794646814f729be2476d0dd0d22fab`. Later SDK commits through the current code head are not yet packaged into that bundle. See [exact artifact evidence](implementation/evidence/CL-015/g3-final-bundle-2026-09-21.json) only when working on packaging/runtime history.

Missing: final Unity auth/storage/bootstrap/sinks/completion/UI composition, current-bundle device qualification and joint BE-025/INT-010. Do not reimplement existing foundations or claim hosted/device proof from desktop tests. No old gameplay-save migration is supported; remove obsolete Unity staging instead. P4 features/reuse remain in the original ledgers, deferred not deleted.

The old chronological log is archived byte-for-byte. These are previously committed results, not tests rerun by documentation cleanup. Replace current facts as work changes; never append session transcripts.
