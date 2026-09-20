# Inventory

CL-104 adds portable, server-confirmed stack/instance projections plus a durable,
outbox-backed use/consume intent seam. It validates each active holding against an
exact app-visible catalog snapshot, keeps pending intent separate from holdings,
and supplies no local grant/consume engine, provider route, migration, or Unity
composition. `InventoryFeature` remains the legacy unavailable compatibility seam.

The private-sync storage owner remains responsible for applying raw feed groups
and cursors atomically. A production adapter must decode only its committed
inventory projection into `InventoryService`; this module neither advances a
cursor nor interprets an uncommitted page. See `docs/implementation/evidence/CL-104`.
