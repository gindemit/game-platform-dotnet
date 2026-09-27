# CL-101 bounded account-lifecycle seam — 2026-09-20

Implementation commit is recorded after the focused validation below. The portable
service introduces a non-secret backend/issuer/subject descriptor, explicit auth
lifecycle outcomes, a durable reservation/binding directory port, and a narrow
scope lease factory. A session key is never reinterpreted as identity or persisted.
Reservation is written before the provisioning request so an uncertain/lost result
retries the same installation and stream. Only a lease backed by complete private
sync may report Ready.

Focused test command passed 9/9:

`dotnet test tests/GamePlatform.Tests/GamePlatform.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~Cl101AccountsLifecycleTests`

It covers lost-response reservation retry, partial bootstrap, initialized offline
reopen, recovery-required credentials, A-to-B late callback fencing, retirement-failure
failure quarantine, superseded-caller cancellation fencing, cancelled
late-lease drain before newer activation, caller cancellation during A-to-B
retirement, and real
Windows x64 SQLite reservation reopen/binding using the proposed additive migration.
Retirement first removes the lease from active use and synchronously quarantines
the lifecycle. Stop/drain receive only a lifecycle-owned bounded token; a failure
retains the non-active lease for recovery/diagnostic and blocks replacement scopes.
Full SDK tests passed 423/423, Release build passed 18 projects with zero warnings
or errors, and `python scripts/validate.py` passed.

Architecture review allocates the Accounts directory as SDK platform migration
**v4**. It is deliberately **not registered** in the shared
`SqlitePlatformMigrationRegistry`: coordinator ownership must add that approved
immutable entry with concurrent migrations. A host must also
provide the dedicated principal-directory database scope plus a scope-lease factory
that reads `SqlitePrivateSyncStore` readiness. These missing composition steps mean
## 2026-09-21 nonproduction Supabase auth boundary increment

### Security repair — 2026-09-21

The original raw read/write/delete store contract is superseded. The adapter now
requires defensive versioned public-marker/secret compare-exchange records and
never deletes/recreates a marker. Marker absence is recovery. A host must make
the explicit fresh-account decision through `AuthorizeFreshAsync`; the adapter
does not scan account ownership. Signup and refresh claim CAS pending fences,
and a successful response publishes a session only after winning the durable
Known CAS. The public record carries no credential.

`AccountsLifecycleService.StopAsync` now terminally fences and retires account
scope writers, including already-cancelled callers; failed retirement remains
recovery-required. Focused command:

`dotnet test tests/GamePlatform.Tests/GamePlatform.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~SupabaseAnonymousAuthLifecycleTests|FullyQualifiedName~Cl101AccountsLifecycleTests`

passed **20/20**. It covers explicit initialization/absence, public-secret
separation, pending restart, missing/torn secret, NotSent cleanup, uncertain
failure, refresh CAS race/rotation, post-success conflict, cancellation,
unavailable/overflow state, executor bounds, active stop, concurrent start/stop,
repeat stop and failed retirement. The redirect case uses the executor's
internal handler seam; a real local HTTPS redirect listener was not added and
is not claimed.

Source paths:

- `src/GamePlatform.Transport.Http/Supabase/SupabaseAuthConfiguration.cs`
- `src/GamePlatform.Transport.Http/Supabase/BoundedSupabaseAuthHttpExecutor.cs`
- `src/GamePlatform.Transport.Http/Supabase/SupabaseAnonymousAuthLifecycle.cs`
- `tests/GamePlatform.Tests/Transport/SupabaseAnonymousAuthLifecycleTests.cs`

The outer transport adapter implements the Accounts-owned lifecycle port under
the reviewed one-way dependency edge. It requires a separate exact HTTPS
`.../auth/v1/` root, an injected secure-session store and an injected bounded
executor. It sends only GoTrue anonymous signup and refresh JSON. It does not
parse JWT claims, validate issuer/audience, log secrets, retain access tokens
outside memory, or silently create a replacement anonymous user after a sent
or uncertain signup.

`dotnet test tests/GamePlatform.Tests/GamePlatform.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~SupabaseAnonymousAuthLifecycleTests`
passed **11/11** deterministic tests: signup persistence, malformed response,
expiry/refresh rotation, restart, subject mismatch, uncertain and not-sent
signup, offline refresh, secure-store failure, cancellation, concurrency
fencing and executor authority/route/redirect/body limits.

Unity remains unedited. Its actual integration must adapt the existing Android
keystore secret port and create a second auth-root executor; the existing
functions-root executor cannot admit `/auth/v1/` calls. No live endpoint,
device, A05/A07/A12, INT-006 or G3 result is claimed.

CL-101 remains in progress and neither A05/A07/A12 nor Unity/G3 is claimed.

### Independent-review repair â€” 2026-09-21

The adapter now treats a NotSent cleanup CAS/store failure as recovery-required,
while a cleanup cancellation remains cancelled and only an exact durable restore
is offline/temporarily unavailable. This applies to both initial signup and an
already-published session refresh. It rejects zero or non-advancable maximum
marker versions and refuses a malformed successful store response before
publishing a session.

`StopAsync` now bounds acquisition of a concurrently held activation section.
It first fences the generation, then publishes recovery-required if a
noncooperative open/bootstrap does not release the lifecycle-owned timeout. A
late lease retirement failure is retained in quarantine and is observed by Stop;
repeated Stop returns the same terminal recovery truth rather than hiding it.

Focused command passed **25/25**:

`dotnet test tests/GamePlatform.Tests/GamePlatform.Tests.csproj --no-restore --filter "FullyQualifiedName~SupabaseAnonymousAuthLifecycleTests|FullyQualifiedName~Cl101AccountsLifecycleTests"`

This includes deterministic signup/session cleanup CAS-conflict, store-error,
and cancellation cases; malformed/overflow version snapshots; late-retirement
failure; and a bounded noncooperative-open shutdown case. Full SDK validation is
recorded separately for this commit. `python scripts/validate.py`,
`python scripts/test_validation.py` (19 tests), and
`python scripts/test_packaging.py` (29 tests) passed. The full
`dotnet test GamePlatform.sln --no-restore` run had 502 passing tests and one
pre-existing/out-of-scope failure: the CL-008 abrupt-process crash probe did not
reach its requested write boundary; rerunning that test alone reproduced the
same failure. No TLS listener, device, hosted Supabase, or G3 result is claimed.
