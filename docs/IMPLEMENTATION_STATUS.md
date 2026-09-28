# SDK current status

2026-09-28: **P3 complete at the owner-approved local nonproduction scope; broad P4 not released.** Backend [R10 closure](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/p3-review/R10-closure.md) and [qualification](https://github.com/gindemit/game-platform-backend/blob/main/docs/reviews/2026-09-27-p3-review-qualification.md) hold the current acceptance matrix and limits. Historical bounded G3 is unchanged.

## Current installed runtime versus next packaging

Unity imports SDK runtime source `4b1ba063877d44ab12ad77c8d8470cf64a55b1e6` from bundle manifest SHA-256 `2f9061f3cceaadf7e72b07b34d3723c1130bc5c614d4fa4713a2028ce9ac59ba`, currently under `Assets/Plugins/GamePlatform`. Test-only CL-016 live peer CLI `e66edd12a37c4e67e0dfa94de7e2ab512c2b72dd` did not change that imported runtime. Inspected SDK main was `25d9045bb6d7d30c6296a832fd4fdce4416a7e78`; later planning commits are documentation only.

The owner now authorizes Git UPM delivery from this SDK repository, replacing the copied Assets payload after clean-import and platform qualification, followed by only necessary generic campaign progression/evidence changes. Start at [SDK handoff](implementation/CAMPAIGN_UPM_HANDOFF.md) and the [single campaign coordinator](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/README.md), goal `CAMPAIGN_LOCAL`. Neither the package migration nor the new campaign is implemented by this documentation update. No new runtime test/build pass is claimed here.

## Retained P3 acceptance

SDK Release passed 659/659, validation and package verification at the R10 qualification inputs. CL-016 ran real local C#/TS production-provider normal and post-commit loss paths, with reward-receipt seed, complete pull, owner isolation, idempotency and SQL uniqueness hook. Empty selection, readiness 503 and provision 501 failed closed. Fresh nonauthor review passed I10-11/CL-016 at P3 scope; reviewer personally built the CLI and checked empty selection and inspected the remaining evidence.

The imported SDK passed exact Unity gate, IL2CPP build and both-phone R10 qualification. LC09 Home and LC11 level editor are N/A in P3 and carried forward; LC06 account switch remains component-level. A13 is local nonproduction only. Managed hosting, provider/social identity, production backup/rollout, full multi-host isolation and later feature breadth remain P4 or release work. No old gameplay-save migration is supported; preserve issued identity and new SQLite/outbox.

Original ledgers remain [execution-manifest.json](implementation/execution-manifest.json) and [CLIENT_HANDOFF.json](implementation/coordination/CLIENT_HANDOFF.json). Do not dispatch closed P3 rows or treat a bounded packaging/campaign change as full original feature or P4 acceptance. Update current import pins only after actual reviewed package integration; retain historical hashes/evidence unchanged.
