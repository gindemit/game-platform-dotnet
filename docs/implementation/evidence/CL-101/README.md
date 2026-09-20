# CL-101 bounded account-lifecycle seam — 2026-09-20

Implementation commit is recorded after the focused validation below. The portable
service introduces a non-secret backend/issuer/subject descriptor, explicit auth
lifecycle outcomes, a durable reservation/binding directory port, and a narrow
scope lease factory. A session key is never reinterpreted as identity or persisted.
Reservation is written before the provisioning request so an uncertain/lost result
retries the same installation and stream. Only a lease backed by complete private
sync may report Ready.

Focused test command passed 5/5:

`dotnet test tests/GamePlatform.Tests/GamePlatform.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~Cl101AccountsLifecycleTests`

It covers lost-response reservation retry, partial bootstrap, initialized offline
reopen, recovery-required credentials, A-to-B late callback fencing, and real
Windows x64 SQLite reservation reopen/binding using the proposed additive migration.
Full SDK tests passed 375/375, Release build passed 18 projects with zero warnings
or errors, and `python scripts/validate.py` passed.

The SQLite migration is deliberately **not registered** in the shared
`SqlitePlatformMigrationRegistry`: coordinator ownership must allocate/confirm the
next immutable version and register it with concurrent migrations. A host must also
provide the dedicated principal-directory database scope plus a scope-lease factory
that reads `SqlitePrivateSyncStore` readiness. These missing composition steps mean
CL-101 remains in progress and neither A05/A07/A12 nor Unity/G3 is claimed.
