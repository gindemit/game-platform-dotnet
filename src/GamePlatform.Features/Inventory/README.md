# Inventory

CL-104 adds portable, server-confirmed stack/instance projections plus a durable,
outbox-backed use/consume intent seam. It validates each active holding against an
exact app-visible catalog snapshot, keeps pending intent separate from holdings,
and supplies no local grant/consume engine, provider route, migration, or Unity
composition. `InventoryFeature` remains the legacy unavailable compatibility seam.

The private-sync storage owner remains responsible for applying raw feed groups
and cursors atomically. `ApplyConfirmedProjection` borrows that caller-owned
transaction for a strictly advancing, already-decoded confirmed projection; it
does not advance a cursor or interpret an uncommitted page. Receipt acceptance
only marks an intent `AcceptedAwaitingPull` with its expected resulting revision;
only a later matching confirmed projection clears it. See
`docs/implementation/evidence/CL-104`.
