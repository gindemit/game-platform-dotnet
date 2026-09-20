# P3 frozen private-sync invalidation mapping — 2026-09-20

The already-approved frozen `invalidation` literal has a bounded SDK mapping:

1. `PrivateSyncHttpProvider` accepts exactly the frozen removal literal and
   creates the distinct internal `ProjectionMutationKind.Invalidation` value.
   Unknown literals remain protocol failures.
2. `PrivateSyncCoordinator` preserves that enum identity in
   `StoredProjectionKind.Invalidation`.
3. `SqlitePrivateSyncStore` persists it as the distinct `invalidation` raw
   confirmed-projection state admitted by platform migration v7. It is neither
   `removed` nor `tombstone`, and pull application does not touch pending
   commands.
4. `G3PrivateSyncProjector` delivers the distinct typed `"invalidation"`
   signal to its consumer-owned sink within the same SQLite transaction. The
   sink owns discarding the named feature cache and scheduling its normal
   authorized read/refetch; this projector does not invent a direct read route.
   If that sink rejects the signal, the raw state and pull cursor roll back
   together.

This implements the approved contract semantics: invalidation is not evidence
of canonical deletion. It does not create a new schema, migration, endpoint,
feature read route, Unity integration, bundle, or live-backend acceptance.

## Focused evidence

Executed from the clean isolated worktree based on SDK commit `f961bf5`:

```text
rtk dotnet test tests\GamePlatform.Tests\GamePlatform.Tests.csproj -c Release --filter "FullyQualifiedName~Cl010PrivateSyncTests|FullyQualifiedName~Cl010PrivateSyncHttpProviderTests"
```

Exit 0: 57 passed, 0 warnings across 16 projects.

The focused cases cover frozen HTTP literal mapping, unknown-literal rejection,
v7 distinct durable state, pending-command preservation, G3 typed delivery, and
rollback of raw state/cursor when the consumer sink fails.

## Remaining boundary

The consumer-owned live sink must connect an invalidation signal to each
feature's already-authorized query/read path. That composition and its
Unity/live-runtime evidence are outside this SDK mapping increment and remain
unclaimed here.
