# SDK current status

Verified boundary: 2026-09-21, SDK `e57a8e5`; P3 active, G1/G2 passed, G3/G4 pending. Start at [CODEX_SINGLE_AGENT.md](../CODEX_SINGLE_AGENT.md). The backend P3_SESSION_CHECKPOINT.md owns cross-repository next actions; old handoff chronology is not a current work order.

Implemented bounded foundations include production command fingerprint/push, private-sync revision-zero/boundary/observation handling, SQLite v7 invalidation, atomic durable receipt/feed correlation, CL-108 game transaction callback, Supabase anonymous-auth CAS and terminal StopAsync. Recorded Release tests: 503/503. These components still need complete real-game composition/acceptance.

Final SDK artifact source: `73cf2feb84734d453a138573c4ffbc6ffc9a2e1b`; manifest `14562ae0b16985d8a5de5c04a3374fd3f06e83289496810ba516afa7b3e6201e`. It is imported in Unity. Later SDK commits through this boundary are documentation-only. Recorded packaging: 29/29 tests and two byte-identical clean reproductions. See [exact artifact evidence](implementation/evidence/CL-015/g3-final-bundle-2026-09-21.json) only when working on packaging/runtime.

Missing: final Unity auth/storage/bootstrap/sinks/completion/UI composition, current-bundle device qualification and joint BE-025/INT-010. Do not reimplement existing foundations or claim hosted/device proof from desktop tests. No old gameplay-save migration is supported; remove obsolete Unity staging instead. P4 features/reuse remain in the original ledgers, deferred not deleted.

The old chronological log is archived byte-for-byte. These are previously committed results, not tests rerun by documentation cleanup. Replace current facts as work changes; never append session transcripts.
