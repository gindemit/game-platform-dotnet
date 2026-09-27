# Accounts

Owns canonical provisioning, app membership and identity linking; excludes automatic
merge. `AccountsLifecycleService` is the bounded CL-101 portable seam: it uses a
host-provided non-secret principal descriptor and principal-specific auth session,
durably reserves installation/stream identity before provisioning, validates the
issued account mapping, and exposes explicit readiness/recovery outcomes. It never
persists a session key or token and never declares Ready itself: the leased scope
must report an actual complete private-sync bootstrap.

The legacy `AccountsFeature` remains the M0 unavailable facade until coordinator
status/composition work is complete. The nonproduction Supabase HTTP adapter is
outer transport composition: it implements this module's consumer-owned
`IAccountsAuthLifecycle` port without moving Unity or credentials into Accounts.
Its secure-store bridge, account scope lease factory, shared migration
registration, Unity integration, and live backend proof remain host/coordinator
work.

`AccountsLifecycleService.StopAsync` is the explicit terminal scope-quiescence
boundary for a host composition. It fences the generation before waiting for
the lifecycle-owned bounded stop/drain timeout; even an already-cancelled
caller cannot leave an admitted writer active. Repeated/concurrent calls share
one terminal outcome, and later starts remain unavailable. Failed retirement is
quarantined as recovery-required.
