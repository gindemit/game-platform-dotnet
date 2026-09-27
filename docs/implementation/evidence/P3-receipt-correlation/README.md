# P3 G3 receipt and durable CL-107 correlation evidence

Date: 2026-09-20. Source base: `b1375f0`.
The final source commit is the commit containing this evidence.

Implemented bounded scope:

- closed mapper for the approved G3 reward-receipt request/response schema;
- authenticated MessagePack GET provider and exact frozen-route admission;
- account production-composition exposure through neutral `IHttpExecutor`;
- production durable CL-107 state codec, immutable evidence and conditional
  presentation-claim composition;
- durable receipt/feed/operation join for both arrival orders, with exact feed
revision and exact installed kind/resource/component-revision evidence. Receipt
staging, its optional existing-feed join and operation presentation share one
serialized transaction; feed staging, its immutable proof and confirmation share
the caller's projection/cursor transaction.

The join never carries installed quantities, applies receipt quantities, bumps
component revisions or advances a cursor. Feed staging uses the caller's
projection/cursor transaction. `ReconcileAsync` is retained only as idempotent
repair/notification, not a post-commit correctness requirement.
Exact replay is accepted; changed same-revision evidence and partial/ambiguous
membership fail closed. Restart tests use the pinned real SQLite store.

Exact verification:

```text
rtk dotnet restore GamePlatform.sln --locked-mode
exit 0; 18 projects, 0 errors/warnings

rtk dotnet build GamePlatform.sln -c Release --no-restore
exit 0; 18 projects, 0 errors/warnings

rtk dotnet test tests/GamePlatform.Tests/GamePlatform.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~G3RewardReceiptHttpProviderTests|FullyQualifiedName~G3ReceiptCodecTests|FullyQualifiedName~Cl107RewardFulfillmentServiceTests"
exit 0; 17 passed, 0 failed/skipped/warnings

rtk dotnet test GamePlatform.sln -c Release --no-build --no-restore
exit 0; 487 passed, 0 failed/skipped/warnings

rtk python scripts/validate.py
exit 0

rtk python -m unittest discover -s scripts -p test_*.py
exit 0; 51 passed

rtk python docs/implementation/validate_manifest.py
exit 0; 52 tasks, 16 feature ledgers, 21 waves

rtk python -m unittest discover -s docs/implementation -p test_*.py
exit 0; 14 passed
```

No canonical
schema/generated carrier, migration registry, package/bundle manifest, client
handoff or coordination state was changed.

The focused receipt tests include an injected receipt-correlation staging fault
that rolls back both the receipt fact and operation presentation before retry,
a fault before feed/cursor commit that rolls back cursor, feed evidence and
confirmation, exact replay and changed-replay rejection, and both successful
arrival-order transactions reopened before their counterpart arrives. They do
not apply a receipt quantity or create a wallet row.

Remaining boundaries: the Unity sink must map its already installed typed
changes into `InstalledRewardProjection` and invoke that transaction; invalidation remains fail-closed pending its owner-assigned migration;
no Unity/editor/device/live-backend offline/restart/reconnect journey ran, and
INT-007, BE-025/INT-010 and G3 remain unpassed.
