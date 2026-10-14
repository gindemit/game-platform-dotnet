# Inventory

CL-104 adds portable, server-confirmed stack/instance projections plus a durable,
outbox-backed use/consume intent seam. It validates each active holding against an
exact app-visible catalog snapshot, keeps pending intent separate from holdings,
and supplies no local grant/consume engine, provider route, migration, or Unity
composition. `InventoryFeature` remains the legacy unavailable compatibility seam.

The private-sync storage owner remains responsible for applying raw feed groups
and cursors atomically. `ApplyConfirmedProjection` borrows that caller-owned
transaction for a strictly advancing, already-decoded confirmed projection;
`null` means absent and revision `0` is confirmed state. It does not advance a
cursor or interpret an uncommitted page. Receipt acceptance
only marks an intent `AcceptedAwaitingPull` with its expected resulting revision;
the same or a later authoritative projection removes it from every durable read,
including after a borrowed-transaction restart. See
`docs/implementation/evidence/CL-104`.

Platform migration v6 permits zero directly, so confirmed inventory rows store
the exact semantic revision, including zero and the signed-64 maximum.
