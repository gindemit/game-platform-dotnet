# SDK current status

## 2026-10-05 consumer identity reconciliation

At Unity `54fb156c1600b825c010cce067976bb30337d03d`, the [package lock](https://github.com/gindemit/MrSquareUnity/blob/54fb156c1600b825c010cce067976bb30337d03d/Packages/packages-lock.json) requests `0.1.0-dev.119` and resolves Git UPM `1e8a6510a8db5421c9f012c1b0940e92e505600b`. The [localization evidence](https://github.com/gindemit/MrSquareUnity/blob/54fb156c1600b825c010cce067976bb30337d03d/Docs/Implementation/WebPlayer/2026-10-05-localization.md) records 142 import-verified package files and bounded localization/build/browser results. These are recorded results, not a new SDK-backed Android, hosted-service or portal qualification. In particular, WebLite is not evidence of the mobile SDK runtime path.

This correction changes documentation only: no SDK runtime, generated payload or Unity manifest is edited. A newer generated delivery tag does not automatically change the consumer. The older hashes and acceptance below retain their original scope; `cae1a21` is no longer described as the current Unity pin. Reconcile later consumer changes against their exact lock and runtime evidence.

## 2026-10-04 hosted-dev continuation

At the earlier hosted-dev checkpoint, SDK main `5d3819d` contained the generated Git UPM from source `6915723`, and Unity consumed `cae1a21`; this is a historical snapshot, superseded for current consumer identity by the section above. Backend deployment evidence records hosted function source `f317ced`, not a newer SDK runtime. The retained project has migrations 001–018 recorded, but a JWT reaching bootstrap request validation is not a successful provision/bootstrap journey. A separate [retained-dev preflight](../integration/HostedDevSmoke/README.md) now checks the exact project/issuer, locally supplied namespace and run-owned app fixture identity, and a redacted JWT claim set. It makes no hosted request and does not establish hosted SDK acceptance. Exact fixture, cleanup and independently reviewed execution remain prerequisites.

2026-09-28: **P3 complete at the owner-approved local nonproduction scope; broad P4 not released.** Backend [R10 closure](https://github.com/gindemit/game-platform-workspace/blob/main/docs/work/p3-review/R10-closure.md) and [qualification](https://github.com/gindemit/game-platform-backend/blob/main/docs/reviews/2026-09-27-p3-review-qualification.md) hold the current acceptance matrix and limits. Historical bounded G3 is unchanged.

## Qualified Unity consumption

The earlier Unity-qualified Git UPM `cae1a219c0c2c36fb0589510f6252989f9257a97` was deterministically generated from reviewed source `9fddbe6bb6bec4ad9460a0cb3041b5db9a2df8b2`; two clean copies were byte-identical and independent payload review passed. At that checkpoint, Unity's manifest and import verifier pinned that package. An isolated Unity 6000.5.3f1 worktree resolved and import-verified it, then passed 74/74 focused account-scope EditMode tests. Those tests qualify that historical package at their bounded Editor scope, not the later consumer pin, IL2CPP or physical devices. See the [campaign UPM handoff](implementation/CAMPAIGN_UPM_HANDOFF.md), backend [reading guide](https://github.com/gindemit/game-platform-workspace/blob/main/docs/engineering/CODEBASE-READING-GUIDE.md) and [maintenance evidence](https://github.com/gindemit/game-platform-workspace/blob/main/docs/work/campaign-2026-09-28/evidence/maintainability-2026-10-03.md).

The package migration and bounded generic campaign integration are implemented at their recorded source and package scope. Configured physical Android acceptance, ordinary shell activation, broad P4 and publication remain held. The [current campaign checkpoint](https://github.com/gindemit/game-platform-workspace/blob/main/docs/work/campaign-2026-09-28/checkpoint.md) identifies exact source, tests and open gates.

## Retained P3 acceptance

SDK Release passed 659/659, validation and package verification at the R10 qualification inputs. CL-016 ran real local C#/TS production-provider normal and post-commit loss paths, with reward-receipt seed, complete pull, owner isolation, idempotency and SQL uniqueness hook. Empty selection, readiness 503 and provision 501 failed closed. Fresh nonauthor review passed I10-11/CL-016 at P3 scope; reviewer personally built the CLI and checked empty selection and inspected the remaining evidence.

The imported SDK passed exact Unity gate, IL2CPP build and both-phone R10 qualification. LC09 Home and LC11 level editor are N/A in P3 and carried forward; LC06 account switch remains component-level. A13 is local nonproduction only. Managed hosting, provider/social identity, production backup/rollout, full multi-host isolation and later feature breadth remain P4 or release work. No old gameplay-save migration is supported; preserve issued identity and new SQLite/outbox.

Original ledgers remain [execution-manifest.json](implementation/execution-manifest.json) and [CLIENT_HANDOFF.json](implementation/coordination/CLIENT_HANDOFF.json). Do not dispatch closed P3 rows or treat a bounded packaging/campaign change as full original feature or P4 acceptance. Update current import pins only after actual reviewed package integration; retain historical hashes/evidence unchanged.
