# CL-107 — RewardFulfillment receipt presentation

Source task commit: recorded after task-branch verification. This bounded SDK increment
adds no client reward authority. `RewardFulfillmentService` stores trusted
receipt presentation state only, with captured account/view ownership and an
explicit state-codec seam. It never references Wallet, Inventory or Entitlements
services, never creates an outbox command, and never stores projected balances,
holdings, rights, or a locally selectable grant plan.

The feature requires a composition-owned feed adapter to submit a
`RewardProjectionGroup` only after the corresponding Wallet/Inventory/
Entitlements projections have installed the same committed private-feed group.
The carrier contains grant/source/feed-revision and ordered receipt-line/component
revision membership only. `StageProjectionGroup` accepts the caller's borrowed
`ILocalStorageTransaction`, so the adapter can persist that evidence atomically
with component projections and its pull cursor. The service confirms only an
exact receipt/group match; partial or conflicting group evidence fails closed.

Exactly-once UI display is not implemented with an in-process lock or a
feature-state read/upsert race. The feature requires a narrow
`IRewardPresentationClaimStore` conditional-insert port, with the record update
as its same-transaction callback. The focused test composes it with a real
SQLite `INSERT OR IGNORE` adapter and races two independent service instances.

Focused evidence on Windows x64 / .NET SDK 9.0.203:

```
dotnet test tests\GamePlatform.Tests\GamePlatform.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~Cl107RewardFulfillmentServiceTests
```

Result: 7 passed, 0 failed, 0 warnings. The tests use the pinned real SQLite
executor and durable feature-state migration, covering accepted-before-pull,
response-loss group-before-receipt, repeated receipts, a different idempotent
operation for the same grant/source, partial-group rejection, terminal rejection,
foreign owner fencing, reopen, and grant-keyed exactly-once presentation.
They also prove rollback versus commit of the borrowed projection/cursor
transaction before later receipt reconciliation, and two-instance/reopen claim
exclusivity.

The locked Release build and full SDK test suite were also run on the corrected task head:
422 passed, 0 failed, 0 skipped, 0 warnings. `python scripts/validate.py` passed
and `python -m unittest discover -s scripts -p test_*.py` passed 51/51.
The first full-suite attempt exposed the pre-existing CL-007 concurrent-open
race (`gp_sync_state already exists`); the immediate unchanged rerun passed
422/422. No CL-007 source was changed by this task.

This is local durability evidence, not backend transaction proof, MessagePack
codec/HTTP evidence, Unity integration, live configuration, A06/A08 or G3.
