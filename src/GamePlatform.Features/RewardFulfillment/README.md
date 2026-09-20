# RewardFulfillment

`RewardFulfillmentService` is a portable, receipt-presentation coordinator. It
does not accept a local grant plan, mutate Wallet/Inventory/Entitlements, create
an outbox command, or turn an animation into value acknowledgement. A trusted
backend receipt is retained as `AcceptedAwaitingPull` until a separately mapped,
ordered private-feed `RewardProjectionGroup` proves that every receipt line was
installed by the component projection owners from the same committed group.

Presentation state is account/view-generation scoped and SQLite durable:
`Pending`, `AcceptedAwaitingPull`, `Confirmed`, and terminal `Rejected`. A
one-time presentation lease is keyed by immutable `grantId`, independently of
backend business-source uniqueness, so idempotent lookup by a second operation
cannot animate the same receipt twice after a restart. The feed-group carrier
contains only receipt/group membership and component revisions; it contains no
balance, holding, entitlement payload, or grant authority.

`StageProjectionGroup` is the narrow borrowed-transaction API for the private
feed adapter: it writes validated group evidence in the same transaction as
component projections and the pull cursor. The async observation path uses that
same staging mutation. `IRewardPresentationClaimStore` is a separate
consumer-owned conditional-insert seam; its successful callback writes the
operation presentation record in the same storage transaction. Composition must
provide a real atomic store implementation, never an in-memory lock.

The receipt endpoint/feed adapter remains a composition owner. It maps frozen
backend receipt and group semantics into these contracts after component
projections commit. G3 peer, production codec/HTTP, Unity wiring and A06/A08
acceptance remain unverified.
