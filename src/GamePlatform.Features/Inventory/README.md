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

SQLite's current `gp_feature_state` constraint reserves positive row revisions,
so this module uses a private `semantic + 1` envelope. Public snapshots and
codecs retain semantic revision `0`; a v6 storage-contract migration must remove
this temporary envelope with compatibility coverage.
