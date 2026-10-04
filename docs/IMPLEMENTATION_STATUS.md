# SDK current status

## 2026-10-04 hosted-dev continuation

SDK main `5d3819d` contains the latest generated Git UPM, produced from source `6915723`; Unity still consumes `cae1a21`. Backend deployment evidence records hosted function source `f317ced`, not a newer SDK runtime. The retained project has migrations 001–018 recorded, but a JWT reaching bootstrap request validation is not a successful provision/bootstrap journey. A separate [retained-dev preflight](../integration/HostedDevSmoke/README.md) now checks the exact project/issuer, locally supplied namespace and run-owned app fixture identity, and a redacted JWT claim set. It makes no hosted request and does not establish hosted SDK acceptance. Exact fixture, cleanup and independently reviewed execution remain prerequisites.

2026-09-28: **P3 complete at the owner-approved local nonproduction scope; broad P4 not released.** Backend [R10 closure](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/p3-review/R10-closure.md) and [qualification](https://github.com/gindemit/game-platform-backend/blob/main/docs/reviews/2026-09-27-p3-review-qualification.md) hold the current acceptance matrix and limits. Historical bounded G3 is unchanged.

## Qualified Unity consumption

The Unity-qualified Git UPM `cae1a219c0c2c36fb0589510f6252989f9257a97` was deterministically generated from reviewed source `9fddbe6bb6bec4ad9460a0cb3041b5db9a2df8b2`; two clean copies were byte-identical and independent payload review passed. Unity's manifest and import verifier pin that package. An isolated Unity 6000.5.3f1 worktree resolved and import-verified it, then passed 74/74 focused account-scope EditMode tests. The newer generated package named above has not replaced this consumer pin. This is a bounded Editor result, not new-pin IL2CPP or device acceptance. See the [campaign UPM handoff](implementation/CAMPAIGN_UPM_HANDOFF.md), backend [reading guide](https://github.com/gindemit/game-platform-backend/blob/main/docs/engineering/CODEBASE-READING-GUIDE.md) and [maintenance evidence](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/evidence/maintainability-2026-10-03.md).

The package migration and bounded generic campaign integration are implemented at their recorded source and package scope. Configured physical Android acceptance, ordinary shell activation, broad P4 and publication remain held. The [current campaign checkpoint](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/checkpoint.md) identifies exact source, tests and open gates.

## Retained P3 acceptance

SDK Release passed 659/659, validation and package verification at the R10 qualification inputs. CL-016 ran real local C#/TS production-provider normal and post-commit loss paths, with reward-receipt seed, complete pull, owner isolation, idempotency and SQL uniqueness hook. Empty selection, readiness 503 and provision 501 failed closed. Fresh nonauthor review passed I10-11/CL-016 at P3 scope; reviewer personally built the CLI and checked empty selection and inspected the remaining evidence.

The imported SDK passed exact Unity gate, IL2CPP build and both-phone R10 qualification. LC09 Home and LC11 level editor are N/A in P3 and carried forward; LC06 account switch remains component-level. A13 is local nonproduction only. Managed hosting, provider/social identity, production backup/rollout, full multi-host isolation and later feature breadth remain P4 or release work. No old gameplay-save migration is supported; preserve issued identity and new SQLite/outbox.

Original ledgers remain [execution-manifest.json](implementation/execution-manifest.json) and [CLIENT_HANDOFF.json](implementation/coordination/CLIENT_HANDOFF.json). Do not dispatch closed P3 rows or treat a bounded packaging/campaign change as full original feature or P4 acceptance. Update current import pins only after actual reviewed package integration; retain historical hashes/evidence unchanged.
