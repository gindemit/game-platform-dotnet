# Entitlements

Owns durable server-confirmed rights, expiry and revocation. `EntitlementsService`
uses caller-owned SQLite transactions through the generic durable feature-state
store; it never verifies a receipt, grants a right, or uses device time as an
authority source. Active rights with a server expiry are explicitly
`ExpiryUncertain` while offline. Rights are scoped by captured backend/app/
account/view/generation and must be present in the exact app-visible catalog.
`ApplyConfirmedInTransaction` is a narrow borrowed-transaction mutation seam
for a future private-feed adapter to commit entitlement effects with its cursor
and other group effects. The adapter must pass the durable prior revision read
inside that same transaction; `null` means absent while revision `0` is real,
and non-advancing mutations fail closed.

SQLite's current `gp_feature_state` constraint reserves positive row revisions,
so this module uses a private `semantic + 1` envelope. Public snapshots and
codecs retain semantic revision `0`; a v6 storage-contract migration must remove
this temporary envelope with compatibility coverage.

The feature is portable only. There is no purchase/IAP provider, private-feed
adapter, production codec, Unity composition, or peer evidence yet. INT-011 is
the eventual consumer integration boundary. Evidence: CL-106 focused real
SQLite tests (A06/A07), with live private transaction-group and peer tests still
unrun.
