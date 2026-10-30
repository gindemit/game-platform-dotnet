# SDK current status

## Canonical checkout and current consumer identity — 2026-10-21

The owner selected one working checkout on `main` at
`C:/Work/git/gindemit/game-platform-dotnet`, with frequent reviewed source pushes.
Historical branches/package builds are preserved as recovery history rather than
parallel working copies. This is source consolidation, not a new SDK release.

The canonical Unity manifest and lock currently select generated SDK
`0.1.0-draft`, Git UPM delivery
`0c68319288d7413db432290ae3ccdba89e4bdc1e`, built from source
`3edc51834388f95e7a6c4bcca45b232dc681dd7a`. Package and source bundle manifest
hashes begin `e8633e14` and `de1d8fa0`, respectively. Native consumer import and
Android integration validation remain in progress. The preceding virtual Store
package `048bfb0a228f015be93caa2897fb513aa5620e15` passed import verification
covering 145 files, 24 managed assemblies and 38 P/Invokes. Its assembled account
and Store EditMode suite passed 101/101; game delivery is
`f0f84ae0a2e158a8f726aaaf9cfc5e70141715a5`. Final extended native layout acceptance
is pending. Navigation remains pinned separately to
`f5be3035e43efa1e1ee517693e6219292d6803e5`. This records actual consumer identity;
it does not extend older runtime/device acceptance to the newly combined Unity
atlas/UI source.

## 2026-10-14 configuration scheduling repair and local target checkpoint

Task source `2aeef858` commits the reviewed caller-context repair on published
parent439272a. Owning validation uses immutable SDK495
(`495eb8bf4db36c1fb8a735a2baed36fa8a306afe`) plus the exact reviewed overlay.
Owning local gates pass 75 Python and 998 .NET tests; nine pre-existing
joined-recovery cases remain skipped without their separate harness. The ordinary
local package has 23 managed and three native payloads, with both Configuration
projects excluded. Historical CI154/dev.154 delivery provenance remains dated
history; this checkpoint does not repin the game or assert a new CI delivery.

Repaired target inputs are separately frozen SDK557
(`5573a2a03492f3fd6088926d6320484b33e942fe`) plus exactly three reviewed source
overlays. Independent reproduction matches core DLL SHA256
`26e9846a4401b3f851852062e1c8a06b578a757bf4c7efda49d757f633eb2c45` and client DLL
SHA256 `f060a37447b8fa0c2c2a449957ee60c19ee5b648342a98e843441d82f7123b46`.
All eight isolated matched targets passed. Measured Android growth is 482,256
bytes, exceeding the 262,144-byte objective; WebGL growth is 134,133 bytes and
meets that objective. These measurements do not qualify Android JNI or a device.

Actual native joined runtime5 is accepted at its scoped local boundary. Actual C# browser2 lifecycle, durable cached state after actual reload and two-host CAS retry are locally reviewed; the automatic PlayerPrefs reload-marker dispatch remains unproven.
The actual page reload was followed by visible owner replacement/restoration at
generation3, not automatic restore-only dispatch. Actual Conflict,Committed
confirms shipping retry; earlier timeout/noncollision attempts remain retained.
The legacy RemoteConfig facade remains unavailable. Live/deployment, physical
devices and game integration remain held. Exact evidence and limits belong to
the [service receipts](https://github.com/gindemit/game-platform-workspace/tree/main/docs/work/remote-config-service).

## 2026-10-14 optional configuration lifecycle: local source/package checkpoint

Reviewed source `5573a2a03492f3fd6088926d6320484b33e942fe` adds the independently usable
netstandard2.1 `GamePlatform.Configuration.Client` and explicit native/browser host
adapters around the existing pure evaluator. It has no account, SQLite, Unity or game
dependency in its portable assembly. Async selected-graph validation, durable cache,
pin restoration, public emergency polling and owner-generation fencing are scoped to
the optional lifecycle; the legacy RemoteConfig feature facade remains unavailable.

Independent client review passed 64 focused tests and seven additional lifecycle/race
probes, and reproduced the exact optional DLLs using recorded Git/build-path inputs.
Full local owning gates passed 75 Python and 995 .NET tests; nine existing joined-recovery
cases were skipped without their separate harness. Formatting and Release build passed.
Two clean clones reproduced byte-identical ordinary SDK/UPM packages: 23 managed
assemblies, with both Configuration projects excluded. This does not repin the game.

Actual two-origin shipping JS tests passed IndexedDB restore, stale-generation refusal,
quota/abort boundaries and two-tab conflict/merge. They are separate from C#/Unity
runtime qualification. The isolated Unity full-client measurements are pending at
this checkpoint; Android JNI/device, live edge/deployment and game integration remain
unrun. Exact source/staged artifact identities, earlier failures and limits are in the
[service receipts](https://github.com/gindemit/game-platform-workspace/tree/main/docs/work/remote-config-service).
Consumer dev.150 and all historical/public gates retain their recorded scopes.

## 2026-10-12 FIN-ANDROID configured consumer qualification

Current reviewed consumer Git delivery is `0.1.0-dev.150`,
`aa80f9dd48794b32e66391aba5cdfa53c347580c`, from source
`6ad5efeb0f700a6acc3c262be319a94fac161274`; final SDK CI passed.
The main checkout's generated dev.145 payload remains a separate identity.
Unity repinned manifest/lock/verifier to that exact tagged delivery and passed
400 Player plus 48 Review EditMode cases, 44 PlayMode cases, resolved inventory,
configured ARM64 IL2CPP build and an isolated High fixture. Physical Dev Android
retained-account gameplay/social/offline restart/reconnect and real isolated hosted
missing-storage recovery passed; successor pending command bytes survived process
death and acceptance. Exact artifacts, initial harness/import failures and limits
are in the [FIN-ANDROID receipt](https://github.com/gindemit/game-platform-workspace/blob/main/docs/FIN-ANDROID-RESULTS-2026-10-12.md).
P3 stays closed; broad P4/public release holds and earlier dated receipts remain.

## 2026-10-12 FIN-AUTH desktop response bound

The desktop Supabase Auth executor now accepts up to 64 combined response/content
headers, including the legitimate 20-header shape previously rejected at 16.
Name/value, duplicate, body, authority and route guards are retained. Focused
lifecycle tests pass 55/55 and the Release SDK suite passes 883/883, zero skips.
The [local validation receipt](FIN-AUTH-VALIDATION-2026-10-12.md) distinguishes
fake-store provider reconstruction from durable storage/process restart. The
coordinator additionally verified real DEV signup/refresh (20 headers), encrypted
host-store restart with the same subject (21 headers), and authenticated hosted
health/readiness. Package and consumer evidence remain separate; no broad gate changes.

Independently reviewed generated Git UPM dev.145 now carries source
`13c8ed92ecbf935795088555a2ea03ab2852d20b`; source CI passed and full package
verification plus a package-byte-matched DEV restart refresh passed. Native/import/
AOT policy bytes are unchanged. The Unity consumer remains on dev.144 until its
separate repin and qualification; no release tag or registry publication was made.

## 2026-10-12 updated consumer validation

Reviewed SDK source `020695b5ead664f829ed866867100faa87b1f444` is delivered as
`0.1.0-dev.144`, Git UPM commit `281ce9b6c45682c1caa32cd6ca52a756734cbcff`.
Delivery CI [38197914770](https://github.com/gindemit/game-platform-dotnet/actions/runs/38197914770)
passes. The selected Unity manifest, lock and import verifier pin this delivery.
Both WebGL candidates, configured Android Player shell tests and isolated physical
ARM64 native tests passed with the updated package. Exact checks and limits are in
the [consumer receipt](https://github.com/gindemit/MrSquareUnity/blob/main/Docs/Implementation/PlatformValidation/2026-10-12-sdk-dev144.md).
This reconciles the current consumer identity; earlier sections remain dated evidence.

## 2026-10-11 reviewed recovery corrections and consumer update

Reviewed source `d71f25cbd181560c5ce5aeea4fea8c2f952eb8af` fixes successor
resume after adoption and terminal inspection expiry, and supplies the atomic
principal-directory extension needed for a consumer admission witness.
CI `38169608199` generated SDK `0.1.0-dev.140`, delivery
`f86a5aee14ab8e81f0d0b9084c6d07a5fd6c2efe`. The selected Unity manifest, lock and
import verifier pin this exact delivery. Both source and actual delivered DLL
suites passed 876/876 with zero skips; complete package verification passed.
The consumer must explicitly supply the package's canonical `Runtime/link.xml`
to Unity's linker: package metadata alone does not activate preservation. The
selected game now supplies it through an Editor-only `IUnityLinkerProcessor`;
physical checks and their limits belong to the linked consumer receipt.
Unity execution and physical storage qualification have their own
[consumer receipt](https://github.com/gindemit/MrSquareUnity/blob/main/Docs/Implementation/StreamRecovery/VALIDATION-2026-10-11.md).
The earlier deliveries below remain dated evidence; no broad gates change.

## 2026-10-11 portable stream inspection and recovery

Production account HTTP composition now includes the backend's stream inspect and
recover endpoints. Portable recovery adds an immutable persistent principal-directory
journal, atomic successor adoption, retained-command receipt reconciliation and
account lifetime fencing. Complete bootstrap remains a consumer-host requirement;
the change does not activate Unity recovery or product gates. See the
[implementation and actual checks](work/stream-recovery-2026-10-11/IMPLEMENTATION.md).
Reviewed generated dev.138 delivery is `8429e119`, from source `83b07c6`;
the [delivery receipt](work/stream-recovery-2026-10-11/DELIVERY.md) records exact
hashes, successful CI and 867/867 tests against the actual delivered managed DLLs.

## Friends generated delivery and consumer pin

Source `9df6800c3109b8b122a4661bd18ae0512fa3a0be` passed CI `38151671867`
and generated `0.1.0-dev.135`, delivery
`e17d18b3bacb9fde4a992504f6e5f074167fbac7`. Pocket Bloom's committed package
manifest and lock now select this exact delivery. Root and independent review
verified the complete package inventory. Real generated-DLL local HTTP checks
pass. The same generated payload passed 72 hosted real-auth checks and the
consumer's scoped mounted Unity, local/DEV Android and sustained offline
restart/reconnect qualification. Exact identities and limits are tracked in the
[Friends validation receipt](https://github.com/gindemit/game-platform-workspace/blob/main/docs/FRIENDS-VALIDATION-2026-10-11.md).
The consumer pin alone does not establish runtime results; the linked receipts do.

## 2026-10-11 selected core Friends source

The selected Friends service, authenticated provider, closed MessagePack contract,
durable cache/journal and friends-ranking scope are implemented in source.
Real SQLite restart and invalidation races, retained historical receipts and
production composed routes have local regression coverage. See
[Friends source evidence](work/friends-2026-10-11/EVIDENCE.md) for exact checks.
Generated-package, Unity, Android and hosted qualification remain separate.

## 2026-10-11 selected period leaderboards

The account-bound `LeaderboardsService` implements authenticated immutable
snapshots, bounded first-page stale caches, explicit country assignment and
durable uncertain-command retry. The legacy `LeaderboardsFeature` facade remains
unavailable; the subsequently selected Friends source is recorded above. Historical CL-113 and broad
P4 acceptance are unchanged.

Historical SDK 132 passed source tests and import checks, but independent hosted
testing caught the production HTTP executor rejecting both leaderboard routes.
The SDK 133 repair and subsequent hosted/physical acceptance below supersede it.
The current cross-repository receipt is
[leaderboards validation](https://github.com/gindemit/game-platform-workspace/blob/main/docs/LEADERBOARDS-VALIDATION-2026-10-11.md).

Route repair `8e30aaa900a90b995f8656c25a0c2919e02eb71d` passed 804 Release
tests, 27 independent executor tests and exact CI 38140944293. Verified generated
delivery `0.1.0-dev.133` is `ea77967b089315c96dcbc23981dc2ca07f0c4a67`.
Independent generated-DLL DEV smoke passed all twelve kind/scope/schedule reads,
country assignment/replay/conflicting identity and leader-only team authorization.
Responses remain bound to the authenticated token principal regardless of a
different local provider account argument. Empty pre-completion boards supplied
no continuation cursor. Unity source `0200ae9d2c7c24423bb7653037ce0c186d938f8b`
then passed 49 EditMode/four PlayMode checks and Android IL2CPP build with this
exact package. The preserved phone account completed a real garden; 80 independent
post-phone cloud assertions confirmed player/team score 1 on all global schedules
and both monthly Germany boards. The owning consumer's
[device receipt](https://github.com/gindemit/MrSquareUnity/blob/main/Docs/Implementation/Leaderboards/DEVICE-2026-10-11.md)
records exact artifact, network and scope limits. These results supersede the
SDK 132 transport failure without broadening historical feature or P4 acceptance.

## 2026-10-11 Teams source implementation

Teams now has typed online queries/mutations, authenticated production HTTP
composition, durable stale caches and explicit uncertain-command retry, plus
additive private-feed invalidations. Finalization repaired canonical Teams route
admission in the bounded production HTTP executor and Unicode-scalar search bounds
in both public and wire queries. Reviewed Release regression passes 770/770;
focused Teams/executor checks pass 49/49, including composed-provider routes,
malformed/over-limit searches and the existing durable privacy/retry cases.
See [Teams evidence](../src/GamePlatform.Features/Teams/EVIDENCE-2026-10-11.md).
The generated SDK and UPM package reproduce byte-for-byte in two independent
clean clones of source `e8f94b8a4c7d679d47761d9bb8acf41b12e4d311`, including
clean-clone validation of the canonical LF schema pin.
Reviewed generated delivery is `c776f43765a108105c45258c80b38536ec5bd2cd`.
Unity verification input `8a1147b65624849d37787088981c126431660948` pins that full
revision with exact content/source manifest hashes. The current finalization
receipt is `plans/TEAMS-FINALIZATION-RESULTS-2026-10-11.md` in the coordination
repository; original package/import receipts remain historical. Final Unity
EditMode/PlayMode passed 74/4 with exact generated-package verification. Graphics
source `866a1a5` passed two graphics cases with 15 inspected captures and the guarded
Android ARM64 IL2CPP build. Retained APK inspection verifies Teams metadata and
native SQLite; the preserved Editor also resolves/compiles the final package.
The final generated-DLL local HTTPS/PostgreSQL journey passed 65 assertions across
all 24 Teams actions, including dropped/cancelled response retries after restart,
private cache erasure and zero private feed-head advancement by shared queries.
The owner-authorized Supabase Dev backend deployment passed its guarded workflow
at backend `f98756a2`. Physical Teams startup still needs an authorized app startup
configuration; the built APK has none. Signed-JWT desktop fixtures, deployment and
the owner's earlier Android tests do not establish new physical Teams acceptance.
Existing CL-114/INT-014 and P3/G3/P4 gates retain their
original scope; Dev deployment does not establish authenticated hosted Teams
behavior or physical acceptance of this payload.

## 2026-10-05 consumer identity reconciliation

Current Store consumer update, 2026-10-30: the canonical Unity manifest/lock now
select Git UPM `0c68319288d7413db432290ae3ccdba89e4bdc1e` (`0.1.0-draft`),
generated from native purchase source `3edc51834388f95e7a6c4bcca45b232dc681dd7a`.
The preceding virtual Store package `048bfb0a228f015be93caa2897fb513aa5620e15`
passed exact consumer import checks and the assembled 101-test account/Store
EditMode suite. Native package import and Android integration are in progress;
this pin update does not imply physical Billing/Keystore or live store acceptance.

Current identity reconciliation, 2026-10-21: the canonical Unity manifest/lock
select SDK `0.1.0-dev.172`, Git UPM
`cf8b14138ff6db6934db1573e91a98ffd9895387`. This supersedes the older consumer pins
below for current identity only. It does not extend historical runtime acceptance
to the combined atlas/UI source or regenerate the SDK package.

Current consumer update, 2026-10-15: the requested analytics qualification now
pins Git UPM dev.162, `a26d478b05f99e46ba7f36787dcb54d5edae5a53`, generated from
existing canonical remote source `8e4821a9ada89b8f1814e5423bf9b75a90e92f80`.
This supplies existing Profile types missing from the previous consumer package;
it introduces no native analytics SDK feature. SDK source validation passed
1,030 tests with nine skips, plus 76 tooling tests; all 142 package files and
stable importer metadata passed independent review and consumer import checks.
Current Editor compilation passes. The game-owned native uploader and its
compiled/device/cloud qualifications have separate [execution receipts](https://github.com/gindemit/game-platform-workspace/blob/main/docs/work/remote-config-service/ANALYTICS-DEV-DEPLOY-2026-10-15-RESULTS.md).
Earlier package/device results below retain their exact dated scope. The original
local SDK checkout's divergent unrelated commits are preserved; the generated
package was built and pushed from a clean exact remote-main disposable checkout.


Current FIN-ANDROID consumer on 2026-10-12: manifest, lock and verifier select
`aa80f9dd48794b32e66391aba5cdfa53c347580c` (`0.1.0-dev.150`), generated from
source `6ad5efeb0f700a6acc3c262be319a94fac161274`. Configured Dev physical
qualification is linked at the top; source main's generated payload remains separate.

Earlier consumer on 2026-10-12: manifest, lock and verifier selected
`281ce9b6c45682c1caa32cd6ca52a756734cbcff` (`0.1.0-dev.144`), generated from
source `020695b5ead664f829ed866867100faa87b1f444`. The updated validation receipt
is linked at the top of this document; the following entries describe earlier pins.

Recovery follow-up on 2026-10-11: the selected Unity manifest and lock now pin
`f86a5aee14ab8e81f0d0b9084c6d07a5fd6c2efe` (`0.1.0-dev.140`). The reviewed
correction and actual generated-DLL results are recorded at the top of this page;
the linked consumer receipt separates Unity/device execution from package checks.

Follow-up on 2026-10-11: the current Unity manifest and lock select Friends SDK
`e17d18b3bacb9fde4a992504f6e5f074167fbac7` (`0.1.0-dev.135`). Package inventory
and generated-DLL local transport evidence are recorded above; Android acceptance
remains separate. Leaderboards delivery `ea77967b089315c96dcbc23981dc2ca07f0c4a67`,
Teams delivery `c776f43765a108105c45258c80b38536ec5bd2cd` and the October 5 facts
below remain historical.

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

Final local browser review: `rc-s5-repaired-browser-results-review.json`,
SHA256 `12742f6d6955de28a2b3c999b4f201ea74a77bbe51486ebafdfd845ea3f1cc0e`, in the workspace service packet. Two-origin read success
and source credentials=omit are recorded; browser server receipts do not archive
request credentials/CORS header fields. Native HTTP header observations are separate.
