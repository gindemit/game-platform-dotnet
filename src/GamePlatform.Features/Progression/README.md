# Progression

Owns generic gameplay-completion persistence, never puzzle/grid/plant types,
game checkpoint serialization, or rewards. `ProgressionService` commits a
game-supplied outcome and immutable outbox command through the existing atomic
seam, then presents awaiting-receipt, accepted-awaiting-pull, and confirmed
server projection states. It accepts only the explicitly bounded
`client_trusted_unvalidated` authority label: that is an honest casual-tier
admission policy, not gameplay/session/replay validation. A codec/sync adapter
must map the frozen wire command and pull projection; this module adds neither.

`ProgressionFeature` remains an old unavailable compatibility marker. Evidence:
immutable offline commands, provisional state and accepted overlay retention
(A03/A04/A07); peer, Unity, device and G3 evidence remain separate.
