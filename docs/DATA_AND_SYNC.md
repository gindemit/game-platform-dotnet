# Data and synchronization contract

An account-scoped serialized SQLite executor will eventually allocate sequence, update provisional projection and enqueue immutable operation bytes in one transaction. Sequences begin at one; stream IDs survive restart and new databases receive new streams. Assigned commands are never rewritten. Terminal rejection finalizes ordering without becoming success; transient errors do not finalize.

Initial provisioning stages every page at one boundary and marks Ready only after atomic install. Pull reads complete committed groups to a fixed high watermark. Visibility-generation changes and retention expiry require reset/snapshot handling that preserves pending work. Durable state, outbox, receipts and checkpoints are not TTL caches. None of this is implemented at M0.
