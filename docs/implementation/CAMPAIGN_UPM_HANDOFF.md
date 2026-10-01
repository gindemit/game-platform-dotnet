# SDK handoff — current campaign follow-ups

2026-10-01, after the owner-approved recovery session. Current coordination lives in the backend [entry](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/README.md), [checkpoint](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/checkpoint.md) and [board](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/overnight-queue.json). GOAL=CAMPAIGN_LOCAL; PLAYER_SHELL_FOUNDATION only when selected by the owner. Root and scoped AGENTS continue to apply.

## Verified baseline and delivery distinction

| Layer | Reviewed baseline | State |
|---|---|---|
| SDK source | `ac46721780347210ec8b1786553cf68116d1b7b0` | ON-20/RV-03 restored (`4e9f925`); Cl008 first-failure evidence, `-shm` release handshake and failure-artifact upload (`93f3302`, `a43cded`); explicit same-account recovery (`f4538d3`, `ac46721`) |
| SDK CI | Runs 36862779541, 36863237650, 36863824293 | success on `a43cded`, `ac46721` and `0346a5e`; local full suite 720/720 with the qualification macOS SQLite library |
| Git UPM payload | `0346a5eebfd5312e8e4ae6966df3e28cd7f3b3a5` | Regenerated from `ac46721` with .NET SDK 9.0.205 (pin 9.0.203, latestPatch); two independent clean clones byte-equal: bundle manifest sha256 `0f6098921e5ef784916ddecfc8f32b927be0cf995d13760632f8f2f514c79eb2`, package-content-manifest sha256 `596800e7a453a3a33af96fd5b475f27292505a880df0882250505da5aabeef83`; only managed DLLs/PDBs/manifests changed; macOS native withheld; nonauthor payload review approved |
| Unity consumer | `2b7fe5a` repin, `7535634` recovery wiring | `verify_sdk_import.py` pass; focused EditMode 222/222, full 539/539 at the repin and 554/554 with the Unity recovery wiring |
| Backend | `9fc8dd9` | retained-fixture admission test; CI success |

These are evidence pins, not reset targets. Documentation-only descendants need no regenerated DLLs.

## Explicit same-account recovery (owner approved 2026-10-01)

`IAccountsAuthRecovery` (Features.Accounts) is implemented by `SupabaseAnonymousAuthLifecycle.RecoverAsync`: single-flight, bounded by `SupabaseAuthConfiguration.RecoveryTimeout` (default 30 s), same stored subject only. RecoveryRequired, orphaned RefreshPending and Known markers with a decodable retained secret refresh; FreshAuthorized, SignupPending, missing/corrupt secret or subject return RecoveryRequired without HTTP. Uncertain/4xx/5xx/subject mismatch keep the marker and secret; NotSent restores the prior marker and returns UnavailableOffline. The Known write of the rotated secret ignores the timeout and caller token once a valid response arrived, so a replaced refresh token is never retained. `AccountsLifecycleService.RecoverAsync(AppId, …)` is admitted only from RecoveryRequired/Unavailable (not stopped, not with a failed scope retirement), bumps the generation and reaches Ready only through real provision and complete bootstrap; UnavailableOffline never reopens offline. `AuthenticateAsync`/`StartAsync` are unchanged.

Retained-device result: the diagnostic test-auth Player at Unity `7535634` recovered both retained phones with one refresh each, provision and bootstrap on their pre-existing client streams, zero new streams or platform users.

Follow-ups: refuse recovery structurally when the directory has no entry for the principal (`FindAsync` null) instead of relying on the provider returning the stored subject; joined callers currently inherit the owner's cancellation; after UnavailableOffline/Cancelled the host must restart before another recovery.

## Durability follow-up: FU-DURABILITY / RV-02

The harness now copies the synthetic database files before any diagnostic open, waits (≤5 s, 25 ms polls, recorded as `shmReleaseWaitMs`) until the killed child's `-shm` can be shrunk, records an immediate raw/retry open, installs a Windows-x64-only SQLite log hook once per process, and CI uploads `artifacts/test-results` and `artifacts/diagnostics` with `if: always()`. Hard-kill precommit/postcommit assertions are unchanged. On macOS every run shows the `-shm` held for ~26 ms after the child PID is gone; Windows remains unproven. The predeclared comparison is in [CL008_WINDOWS_AB_PLAN.md](CL008_WINDOWS_AB_PLAN.md); refs `ab/cl008-a` (`1860b88`), `ab/cl008-b` (`3105a40`) and `ab/cl008-c` (`a43cded`) exist, and the 15 dispatches need the owner (the agent's dispatch was denied).

## Build environment and package

The exact SDK comes from the user-level official install in `~/.dotnet` (`DOTNET_ROOT`), not a framework downgrade. Canonical runtime source stays in `src`; `upm/` is generated delivery. The main checkout carried an unrelated dirty csproj line, so packaging ran in a clean worktree at `ac46721` and the output was proven equal to the two clean clones before commit.

## Preserved campaign semantics and acceptance

Accepted-receipt repair `5c7a02a`, Unity completion-lock `836fee6`, R03/R04/R05 and CMP-R06 remain as recorded. Push acknowledgement never advances pull checkpoints; immutable operation/business-source identity, generation fencing and zero fabricated rewards are unchanged by this session. Browser, 40/40 Android, loss/crash and coinstallation acceptance remain open at the backend checkpoint's scope. The [preceding handoff](https://github.com/gindemit/game-platform-dotnet/blob/3105a40/docs/implementation/CAMPAIGN_UPM_HANDOFF.md) preserves dated pins.
