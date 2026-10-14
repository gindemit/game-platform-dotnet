# Progression

Owns generic gameplay-completion persistence, never puzzle/grid/plant types,
game checkpoint serialization, or rewards. `ProgressionService` commits a
game-supplied outcome and immutable outbox command through the existing atomic
seam, then presents awaiting-receipt, accepted-awaiting-pull, and confirmed
server projection states. It accepts only the explicitly bounded
`client_trusted_unvalidated` authority label: that is an honest casual-tier
admission policy, not gameplay/session/replay validation. A codec/sync adapter
must map the frozen wire command and pull projection; this module adds neither.

The completion overload with an `applyGameState` callback lets a consumer write
its game-owned checkpoint, progress and client-side presentation state in that
same transaction as SDK pending state, sequence allocation and outbox admission.
It is a synchronous, transaction-only callback: consumers must not retain the
transaction, start nested transactions, publish UI, perform external side
effects or allocate authoritative platform value. A callback failure or
cancellation rolls the entire admission back, and exact replay skips it.

`ApplyConfirmedProjection` is the narrow private-feed integration seam: it
stages a strictly advancing projection plus immutable, bounded confirmation
evidence in a caller-owned transaction, leaving cursor ownership to CL-010.
Only evidence IDs verified against durable accepted-awaiting-pull records are
suppressed after restart; a projection revision alone never suppresses a local
completion.

Projection revision zero is a valid authoritative empty/bootstrap revision, not
an absent or remapped revision. The borrowed seam uses an explicit nullable
prior revision so absent-to-zero is distinct from zero-to-one; the durable
feature-state envelope stays positive only for its local record identity.

`ProgressionFeature` remains an old unavailable compatibility marker. Evidence:
immutable offline commands, provisional state and accepted overlay retention
(A03/A04/A07); peer, Unity, device and G3 evidence remain separate.
