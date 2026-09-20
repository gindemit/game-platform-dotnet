# Entitlements

Owns durable server-confirmed rights, expiry and revocation. `EntitlementsService`
uses caller-owned SQLite transactions through the generic durable feature-state
store; it never verifies a receipt, grants a right, or uses device time as an
authority source. Active rights with a server expiry are explicitly
`ExpiryUncertain` while offline. Rights are scoped by captured backend/app/
account/view/generation and must be present in the exact app-visible catalog.
`ApplyConfirmedInTransaction` is a narrow borrowed-transaction mutation seam
for a future private-feed adapter to commit entitlement effects with its cursor
and other group effects; the adapter remains responsible for complete-group
revision admission.

The feature is portable only. There is no purchase/IAP provider, private-feed
adapter, production codec, Unity composition, or peer evidence yet. INT-011 is
the eventual consumer integration boundary. Evidence: CL-106 focused real
SQLite tests (A06/A07), with live private transaction-group and peer tests still
unrun.
