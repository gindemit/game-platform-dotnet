# Data and synchronization contract

An account-scoped serialized SQLite executor will eventually allocate sequence, update provisional projection and enqueue immutable operation bytes in one transaction. Sequences begin at one; stream IDs survive restart and new databases receive new streams. Assigned commands are never rewritten. Terminal rejection finalizes ordering without becoming success; transient errors do not finalize.

Initial provisioning stages every page at one boundary and marks Ready only after atomic install. Pull reads complete committed groups to a fixed high watermark. Visibility-generation changes and retention expiry require reset/snapshot handling that preserves pending work. Durable state, outbox, receipts and checkpoints are not TTL caches. None of this is implemented at M0.

## Restored-server stream recovery (deviation from BE-002 R2.7)

R2.7 asks the client to inspect the stream when it suspects a server restore. The SDK does not call the recovery inspection or rotation operations. When a bootstrap boundary's `finalizedThrough` is below local terminal state, `PrivateSyncCoordinator` looks up the receipt of every local terminal command above it (`getReceipt`):

- A found receipt means the boundary is stale, so the bootstrap restarts once. A second stale boundary, or a found receipt whose outcome disagrees with the local one, blocks with `StreamRecoveryRequired`.
- An absent receipt allows replay only when its authoritative `observedFinalizedThrough` equals the boundary. Otherwise the boundary is treated as stale.
- A `409` conflict from the lookup (`operation_identity_conflict`, `stream_sequence_conflict`) blocks with `StreamRecoveryRequired`. Other lookup failures return `RemoteFailure` and are retried.
- When every receipt is absent, the commands return to `pending` on the same stream with their original identity and last terminal result, and the local finalized sequence drops to the boundary. They are replayed in order, and the replayed outcome overwrites the stored result.

The stream is not rotated. Rotating before the replay would abandon the lost accepted commands, and R2.9 forbids losing accepted value. No wire change is involved.
