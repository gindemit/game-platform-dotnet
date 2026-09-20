# Accounts

Owns canonical provisioning, app membership and identity linking; excludes automatic
merge. `AccountsLifecycleService` is the bounded CL-101 portable seam: it uses a
host-provided non-secret principal descriptor and principal-specific auth session,
durably reserves installation/stream identity before provisioning, validates the
issued account mapping, and exposes explicit readiness/recovery outcomes. It never
persists a session key or token and never declares Ready itself: the leased scope
must report an actual complete private-sync bootstrap.

The legacy `AccountsFeature` remains the M0 unavailable facade until coordinator
status/composition work is complete. No host factory, secure credential adapter,
shared migration registration, Unity integration, or live backend proof is supplied
by this module.
