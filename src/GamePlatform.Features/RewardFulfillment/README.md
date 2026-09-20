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

`IRewardProjectionEvidenceStore` similarly inserts immutable group evidence or
verifies the exact retained canonical bytes at the same feed revision. It never
uses generic equal-revision upsert replacement; a replay is idempotent while an
operation/source/line/revision change for an existing grant fails closed.

`DurableRewardFulfillmentComposition` supplies the production state, evidence
and claim implementations. `DurableRewardReceiptCorrelation` preserves both
receipt-before-pull and pull-before-receipt. The pull adapter calls
`StageInstalledFeed` only after installing the listed projections, in the same
projection/cursor transaction. The receipt adapter calls `ObserveReceiptAsync`;
each path stages its correlation fact, optional existing join, immutable proof
and operation presentation in its one caller-owned transaction. `ReconcileAsync`
is only idempotent repair/notification for already committed historical state,
never a required post-commit correctness step. Correlation requires the exact feed
revision and exact kind/resource membership; it stores component revisions but
never stores or applies receipt quantities, advances component revisions, or
creates value. Exact replay is idempotent and changed replay fails closed.

The Unity feed sink remains the composition owner for mapping its installed
typed changes into that transaction. G3 live peer, Unity wiring
and A06/A08 acceptance remain unverified.
