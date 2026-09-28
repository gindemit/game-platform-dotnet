# SDK handoff — local campaign and Git UPM

2026-09-28. Owner-authorized plan; new packaging/campaign changes not implemented by this document. Single execution authority: [backend campaign packet](https://github.com/gindemit/game-platform-backend/blob/main/docs/work/campaign-2026-09-28/README.md), goal `CAMPAIGN_LOCAL`. Do not launch an independent SDK coordinator or the historical P3 dispatcher.

## Current implementation

At SDK main `25d9045bb6d7d30c6296a832fd4fdce4416a7e78`, `scripts/package-sdk.py` builds a deterministic commit-pinned managed/native bundle. It validates dependency closure, source-link identity, licenses, native importer policies and AOT/lifecycle metadata. `integration/unity/package` is support input, not yet a complete Git UPM delivery folder. Unity `Assets/Plugins/GamePlatform/consumer-install.json` records runtime `4b1ba063877d44ab12ad77c8d8470cf64a55b1e6` and manifest SHA-256 `2f9061f3cceaadf7e72b07b34d3723c1130bc5c614d4fa4713a2028ce9ac59ba`, plus preserved importer GUID overrides.

`src/GamePlatform.Features/Progression/ProgressionService.cs` is real generic pending-completion coordination, not a puzzle validator or campaign selector. Preserve atomic local admission, business-source/operation idempotency, ordered immutable outbox and accepted-versus-pull-confirmed evidence. Current broader feature stubs must not all be marked implemented.

## First: package delivery, not an SDK rewrite

Follow [Git prerequisites](https://github.com/gindemit/MrSquareUnity/blob/main/Docs/Implementation/DualEdition/GIT-PACKAGE-PREREQUISITES.md), SDK lane CMP-02. Default one complete Git-installable `com.gindemit.game-platform` package in this repository, generated deterministically from the existing canonical .NET source/bundle process. Preserve dependency/native/license/AOT/link/assembly/GUID/platform provenance. UPM will not build .NET DLLs automatically; the pinned package must contain a usable verified payload. Distinguish runtime source commit from package-delivery commit. Avoid duplicate source/DLL compilation and copied maintenance forks.

The Unity integrator alone changes consumer manifests/locks, removes redundant Assets payload and requalifies clean-cache import and actual Android/WebGL boundaries. SDK workers do not edit those files concurrently. Keep the core portable and game-neutral. No registry/marketplace publishing is authorized; reviewed commits/pushes required for the requested Git package are authorized.

## Then: minimal progression/evidence delta

The owner approved generated-only hybrid routing, progress/unlocks without rewards, shared completed milestones across Android devices and device-local active boards. Route/difficulty/content rules stay in the game. Backend owns changed wire/SQL contracts. After CMP-05 contract freeze, CMP-08 tests whether existing interfaces already suffice and changes only the required generic SDK boundary.

Inspect command-stream sequencing across two local account instances before promising concurrent-device support. Test different-operation/same-source and different-attempt/same-milestone outcomes, no-op duplicates with no new progress event, queue drainage, pending/confirmed UI evidence, response loss, rejection, pull/snapshot reconciliation and late-scope callbacks. No max-counter or whole-save overwrite merge. A duplicate milestone must not strand a pending completion forever or manufacture a revision/second effect.

Preserve issued identities, new SQLite/checkpoints/outbox and pending command bytes during package updates. Do not import old gameplay PlayerPrefs or Review/browser progress. No exact cross-device in-progress board transfer or social-linking UI in this packet. Rebuild a new Git package only for actual runtime/payload changes; documentation-only descendants do not require fake DLL churn.

## Validation and ownership

Use existing SDK build/test/package commands, focused Cl108 progression tests and relevant durable storage/delivery tests, deterministic package reproduction, dependency/native/hash negative checks and fresh Unity consumer evidence. Independent review is required before downstream integration. Local .NET success is not Android IL2CPP/native or WebLite stripping evidence.

Parallel SDK packaging may overlap rlottie work in its separate repository. Later SDK/backend/shared-game implementation may overlap only after contract freeze with disjoint file/resource leases. One coordinator integrates/commits/pushes and verifies remote reachability. Keep the shared checkpoint compact; do not duplicate the entire plan here. P3 historical acceptance and broad P4/public release hold remain unchanged.
