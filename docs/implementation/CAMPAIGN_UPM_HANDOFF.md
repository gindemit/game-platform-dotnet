# SDK handoff — storage/schema and recovery hardening

2026-10-01 owner-directed continuation. Current authority: backend [next session](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/NEXT-SESSION-2026-10-01.md), [storage/schema contract](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/STORAGE-SCHEMA-2026-10-01.md), [checkpoint](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/checkpoint.md) and [board](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/overnight-queue.json). Root/scoped AGENTS apply. GOAL=CAMPAIGN_LOCAL, selected NEXT=PLAYER_SHELL_FOUNDATION. No recovery approval needs repeating.

## Owner clarification

This game has no current players; breaking schema changes without backward compatibility are authorized. Do not retain permissive old-schema behavior or add upgrade/backfill/dual-writer machinery solely for development saves. The exact-slot change is game-owned SQLite identity, not a general SDK game-rule feature. Keep JNI/root resolution in the Unity/Android host. This policy does not permit automatic deletion/reset of retained phone/fixture state, copied credentials or edits to the other live game mentioned by the owner. Preserve old artifacts; isolate new-schema qualification and refuse unsupported old stores visibly.

## Delivered baseline — do not repeat

SDK main baseline 10da6f0 contains runtime source ac46721 and Git UPM payload 0346a5eebfd5312e8e4ae6966df3e28cd7f3b3a5. ON-20/RV-03 navigation, explicit IAccountsAuthRecovery, late-cancellation rotated-secret persistence, Cl008 first-failure diagnostics/SHM handshake/failure upload are delivered. Current baseline build/test/package CI passed. Local full suite 720/720 was coordinator-recorded, not rerun by this documentation pass.

The payload was generated with SDK 9.0.205 (global.json 9.0.203/latestPatch; CI uses 9.0.203), compared byte-for-byte across two clean clones and reviewed. Bundle manifest sha256 0f6098921e5ef784916ddecfc8f32b927be0cf995d13760632f8f2f514c79eb2; content manifest sha256 596800e7a453a3a33af96fd5b475f27292505a880df0882250505da5aabeef83. Unity repin 2b7fe5a and source 7535634 consume this package; import verification and full EditMode 557/557 at final source are recorded. A diagnostic APK recovered both retained phones on original streams without new users/streams. Final whole-artifact campaign acceptance is still open.

Documentation-only descendants need no package regeneration. Use the existing installed toolchain, not a repeat of missing-.NET setup. Complete provenance/test scopes remain in the [prior handoff](https://github.com/gindemit/game-platform-dotnet/blob/10da6f0dc391aae2b34f66f9298f3b0dba347dd8/docs/implementation/CAMPAIGN_UPM_HANDOFF.md).

## FU-SAME-ENTRY — next SDK correctness task

AccountsLifecycleService.RecoverAsync currently enters the shared ProvisionAndBootstrapAsync path. If directory.FindAsync(principal, appId) returns null, that method allocates and reserves a new installation/stream. Recovery must structurally prohibit this fresh path. Require the exact retained principal/app/backend entry, matching installation/stream and issued-account binding where present; refuse a missing/incompatible entry without ReserveAsync, replacement IDs, new-stream provisioning or Ready. Keep normal fresh StartAsync behavior. HasAnyEntry in Unity is not an exact-entry proof.

Reproduce missing-entry, incompatible returned entry, missing directory/read failure, retirement/stale generation and valid retained success; assert no prohibited side effects. Preserve provider subject checks, CAS fencing, real provision/bootstrap and late-cancellation rotated-secret durability. Do not add a broad recovery framework or rely on correct Android path choice as the SDK invariant.

## FU-RETRY-ENTRY — coordinate with the host

The current lifecycle admits recovery from RecoveryRequired/Unavailable. UnavailableOffline/Cancelled can require a host restart before another attempt; joined callers inherit the owner attempt's cancellation. Test actual Unity bridge/gate behavior and make each explicit retry or relaunch requirement truthful. No automatic loop, fake Ready or weakened stop/drain. Preserve mandatory persistence of a successfully rotated credential; network timeout, persistence and retirement bounds are distinct and must be characterized honestly.

## FU-ANDROID-STORAGE and FU-CAMPAIGN-SCHEMA

Unity baseline currently prefers external data/install storage. The host must supply one verified app-private internal root before any account/session/directory probe; never silently change save roots or authorize fresh state on unavailable storage. SDK storage remains injected and game-neutral. Namespace/entry/generation mismatches fail closed. Keep directory, account database, immutable outbox and secure-session identity coherent; no absolute Android paths embedded in durable identity.

The owner-authorized new game schema retains independent run-kind/slot identity for active/completed/replay/abandoned rows and compares business source exactly. Preserve generic SDK idempotency, operation bytes and transaction contracts. Coordinate schema/version interfaces only when genuinely crossing the SDK boundary. No backward-compatibility work is needed merely to load previous pre-release data.

## FU-DURABILITY remains open

Use [CL008_WINDOWS_AB_PLAN.md](CL008_WINDOWS_AB_PLAN.md). Verify refs ab/cl008-a=1860b88, b=3105a40 and c=a43cded and inventory existing runs before authorized dispatch. Preserve every result, native/toolchain/runner provenance, hard-kill pre/postcommit assertions and failure artifacts. Do not bypass denied actions.

Arm C's release handshake/probe changes the SHM precondition and touches the file. A green arm C, small zero-failure sample or macOS mapping delay does not prove Windows immediate product-relaunch safety. Root cause and required runtime acceptance remain unproven. No production I/O retries, deletion of WAL or arbitrary sleeps to manufacture green.

## Delivery and finish

After reviewed runtime hardening, FU-CURRENT-DELIVERY runs affected/full SDK suites and the required deterministic clean-copy/native/metadata checks, then fresh nonauthor payload review. One integrator nonforce-pushes source/generated UPM and serially repins/import-verifies Unity. Do not edit generated DLLs independently, downgrade dependencies or inherit old runtime acceptance onto changed payloads.

Preserve accepted-receipt repair 5c7a02a, completion-lock/R03-R05/CMP-R06 evidence and both SDK/game pending representations. Push acknowledgement never advances pull checkpoints or fabricates reward observations. Game-owned state and outbox remain truly transactional with immutable operation identity. Update existing shared plan/board/checkpoint and Unity status; run applicable full-checkout docs/manifest validators. Actual CI success/failure/no-run and exact physical acceptance are separate. Historical P3/G3, ordinary shell activation, broad P4 and distribution gates remain unchanged.
