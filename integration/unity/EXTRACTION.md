# MrSquare.Platform extraction inventory

Source authority: `gindemit/MrSquareUnity` `origin/main` at `5c5233edefc8433d61b6c757fd2022bac98f76ba`.

Current public contracts include `PlatformId`; `ProviderFailure`; `OperationResult<T>`; `DataOwnership`; `StateRecord`; state/cloud/profile providers; team summary/membership/intent/result/contribution and provider; leaderboard query/entry/submission/provider; product/purchase/entitlement and purchase-validation/inventory providers; generic remote configuration/provider; `GameplayOutcome`/sink; and progress event/observer. Runtime also exposes in-memory state, schema migration, feature gating, quest counter/checkpoint, `PlatformServices`, feature modules and `PlatformHost`. Fake team/leaderboard providers and a collecting outcome sink are test-oriented implementations.

`MrSquare.Platform` is a pure `noEngineReferences` asmdef with no direct assembly references. The Unity package manifest does not contain MessagePack-CSharp, SQLite, DI or reactive packages.

Current compile-time consumers are `MrSquare.Unity.Runtime` and `MrSquare.Unity.EditModeTests`; production source usage is `Assets/Scripts/Garden/PocketBloomOutcomeAdapter.cs`, with architecture integration coverage in `Assets/Tests/EditMode/ArchitectureIntegrationTests.cs`. A path-scoped diff from historical starter baseline `eab8d4f106c7506e7eb2a2c841a557fe4de9e48e` to inspected `origin/main` found no changes in the three platform source files or asmdef.

M0 mapping decisions:

| Existing contract | M0 target/adaptation | Authority decision |
| --- | --- | --- |
| `PlatformId` string wrapper | `GamePlatform.Core.PlatformId` compatibility-shaped placeholder | Do not install beside Unity; decide UUID transition/shim in coordinated extraction. |
| `ProviderFailure` | `PlatformFailure` adds explicit NotImplemented | Preserve existing values during migration; review serialized enum use. |
| `OperationResult<T>` | `PlatformResult<T>` | Preserve fail-closed result semantics; no provider success stubs. |
| Provider interfaces | backend/feature/storage inward ports | Map per use case; do not carry provider SDK or universal CRUD inward. |
| `GameplayOutcome` and progress types | future Progression contracts | Preserve generic semantics; no puzzle/grid/plant dependencies. |
| host/gates/quest/runtime fakes | future feature/lifecycle implementations or tests | Do not extract as production evidence; accepted lifecycle supersedes global-host assumptions. |

Before migration, inspect all consuming assembly references and serialized formats, choose one move/shim per public type, add public API/consumer compile tests, and change Unity/package references atomically. No such migration occurred in M0.
