# SDK handoff — local campaign and Git UPM

2026-09-29. Single execution authority: [backend campaign packet](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/README.md), goal `CAMPAIGN_LOCAL`. Do not launch an independent SDK coordinator or the historical P3 dispatcher.

## Current implementation

SDK source commit `208aab70545cdc330166476b5aea500921d07424` generates the deterministic managed/native bundle and complete Git UPM payload at `upm/com.gindemit.game-platform`. Delivery commit `c33ef4543270e072eddcd40bdab2ee2744900180` contains the payload; its content manifest SHA-256 is `b6cc5a373e8c31eb77dd36fd6fb5ecf11840a70ca1978043e5e26f73f32129dd` (143 files). Both commits are on remote `main`. The payload preserves closure, source-link identity, licenses, native importer policy, AOT/lifecycle metadata, GUIDs and Unity-visible `.meta` files. The previous Unity `Assets/Plugins/GamePlatform/consumer-install.json` is still the authoritative project's old copied import until CMP-03 changes the Unity consumer.

Two clean clones produced byte-identical bundles and UPM packages. SDK unit tests passed 659/659; packaging Python tests passed 66/66. A clean disposable Unity consumer pinned to `c33ef45…` passed 47/47 focused EditMode tests with no missing-metadata or compile errors. A pinned macOS SQLite dylib was staged solely in that disposable test project; it is excluded from the committed UPM payload. Independent nonauthor review passed. Android IL2CPP/native, WebGL exclusion and the final combined Unity consumer remain CMP-03 acceptance work.

`src/GamePlatform.Features/Progression/ProgressionService.cs` is real generic pending-completion coordination, not a puzzle validator or campaign selector. Preserve atomic local admission, business-source/operation idempotency, ordered immutable outbox and accepted-versus-pull-confirmed evidence. Current broader feature stubs must not all be marked implemented.

## Package delivery and next gate

CMP-02 is complete; follow [Git prerequisites](https://github.com/gindemit/MrSquareUnity/blob/main/Docs/Implementation/DualEdition/GIT-PACKAGE-PREREQUISITES.md) for CMP-03. The consumer must pin the full Git package delivery commit, not the separate runtime source commit. Future runtime/payload changes require a newly built and verified delivery commit. Avoid duplicate source/DLL compilation and copied maintenance forks.

The Unity integrator alone changes consumer manifests/locks, removes redundant Assets payload and requalifies clean-cache import and actual Android/WebGL boundaries. SDK workers do not edit those files concurrently. Keep the core portable and game-neutral. No registry/marketplace publishing is authorized; reviewed commits/pushes required for the requested Git package are authorized.

## Then: minimal progression/evidence delta

The owner approved generated-only hybrid routing, progress/unlocks without rewards, shared completed milestones across Android devices and device-local active boards. Route/difficulty/content rules stay in the game. Backend owns changed wire/SQL contracts. After CMP-05 contract freeze, CMP-08 tests whether existing interfaces already suffice and changes only the required generic SDK boundary.

Inspect command-stream sequencing across two local account instances before promising concurrent-device support. Test different-operation/same-source and different-attempt/same-milestone outcomes, no-op duplicates with no new progress event, queue drainage, pending/confirmed UI evidence, response loss, rejection, pull/snapshot reconciliation and late-scope callbacks. No max-counter or whole-save overwrite merge. A duplicate milestone must not strand a pending completion forever or manufacture a revision/second effect.

Preserve issued identities, new SQLite/checkpoints/outbox and pending command bytes during package updates. Do not import old gameplay PlayerPrefs or Review/browser progress. No exact cross-device in-progress board transfer or social-linking UI in this packet. Rebuild a new Git package only for actual runtime/payload changes; documentation-only descendants do not require fake DLL churn.

## Validation and ownership

Use existing SDK build/test/package commands, focused Cl108 progression tests and relevant durable storage/delivery tests, deterministic package reproduction, dependency/native/hash negative checks and fresh Unity consumer evidence. Independent review is required before downstream integration. Local .NET success is not Android IL2CPP/native or WebLite stripping evidence.

Parallel SDK packaging may overlap rlottie work in its separate repository. Later SDK/backend/shared-game implementation may overlap only after contract freeze with disjoint file/resource leases. One coordinator integrates/commits/pushes and verifies remote reachability. Keep the shared checkpoint compact; do not duplicate the entire plan here. P3 historical acceptance and broad P4/public release hold remain unchanged.
