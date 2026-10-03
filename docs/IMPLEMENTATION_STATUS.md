# SDK current status

2026-09-28: **P3 complete at the owner-approved local nonproduction scope; broad P4 not released.** Backend [R10 closure](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/p3-review/R10-closure.md) and [qualification](https://github.com/gindemit/game-platform-backend/blob/main/docs/reviews/2026-09-27-p3-review-qualification.md) hold the current acceptance matrix and limits. Historical bounded G3 is unchanged.

## Current source, package and Unity consumption

Unity's manifest and import verifier pin canonical Git UPM `e7c03c5821cba85b921c23f94f743880871cd057`. The package contains reviewed SDK recovery source `1244a5b`; subsequent SDK documentation commits do not change its DLL payload. New SDK runtime source needs deterministic package regeneration, review and a Unity pin update before it is consumed. See the [campaign UPM handoff](implementation/CAMPAIGN_UPM_HANDOFF.md) and backend [reading guide](https://github.com/gindemit/game-platform-backend/blob/main/docs/engineering/CODEBASE-READING-GUIDE.md).

The package migration and bounded generic campaign integration are implemented at their recorded source and package scope. Configured physical Android acceptance, ordinary shell activation, broad P4 and publication remain held. The [current campaign checkpoint](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/checkpoint.md) identifies exact source, tests and open gates.

## Retained P3 acceptance

SDK Release passed 659/659, validation and package verification at the R10 qualification inputs. CL-016 ran real local C#/TS production-provider normal and post-commit loss paths, with reward-receipt seed, complete pull, owner isolation, idempotency and SQL uniqueness hook. Empty selection, readiness 503 and provision 501 failed closed. Fresh nonauthor review passed I10-11/CL-016 at P3 scope; reviewer personally built the CLI and checked empty selection and inspected the remaining evidence.

The imported SDK passed exact Unity gate, IL2CPP build and both-phone R10 qualification. LC09 Home and LC11 level editor are N/A in P3 and carried forward; LC06 account switch remains component-level. A13 is local nonproduction only. Managed hosting, provider/social identity, production backup/rollout, full multi-host isolation and later feature breadth remain P4 or release work. No old gameplay-save migration is supported; preserve issued identity and new SQLite/outbox.

Original ledgers remain [execution-manifest.json](implementation/execution-manifest.json) and [CLIENT_HANDOFF.json](implementation/coordination/CLIENT_HANDOFF.json). Do not dispatch closed P3 rows or treat a bounded packaging/campaign change as full original feature or P4 acceptance. Update current import pins only after actual reviewed package integration; retain historical hashes/evidence unchanged.
