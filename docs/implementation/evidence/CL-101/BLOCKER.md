# CL-101 account-lifecycle contract blocker — 2026-09-20 (superseded)

Source inspected: SDK branch `impl/cl101-accounts-20260920` at
`8941dfe` (clean before this evidence-only change).

## Required behavior that has no accepted contract

CL-101 requires a portable service to map a provider principal to the canonical
account and app membership, reopen the correct initialized account while offline,
surface missing credentials as recovery, fence a late A-to-B result, and retire
the old account scope without losing its pending work.  The current SDK contracts
cannot express the stable, non-secret identity or durable scope selection needed
for those behaviors.

## Existing bounded interfaces

- `src/GamePlatform.Backend.Contracts/Remote/RemoteContracts.cs` exposes
  `IProvisioningRemote.ProvisionAsync(AppId, Guid, ClientStreamId, CancellationToken)`.
  Its result contains server-issued `PlatformUserId`, app membership, and stream
  state only after a request is sent.  It does not accept or return a stable
  provider-principal identity, credential availability, or an account-scope key.
- The same file's `IAuthSession` exposes an opaque `SessionKey`, `GetAsync`, and
  `RefreshAsync`.  `SessionKey` is not specified as a provider principal, durable
  identity, or non-secret persistent value; treating it as one would invent trust
  and persistence semantics.
- `src/GamePlatform.Backend.Contracts/Remote/PrivateSyncContracts.cs` exposes
  `IPrivateSyncRemote`; `src/GamePlatform.Storage.Abstractions/Sync/PrivateSyncStorage.cs`
  exposes a scope-bound `IPrivateSyncStore`; and
  `src/GamePlatform.Sync/Pull/PrivateSyncCoordinator.cs` can install the complete
  bootstrap.  None selects an account-owned store/scope from a durable account
  record or reports whether an existing credential is recoverable.
- `src/GamePlatform.Storage.Abstractions/FeatureState/DurableFeatureState.cs`
  requires a pre-existing `ScopedOwnerContext`; it cannot bootstrap that context
  or map a provider principal to it.

The CL-101 card itself identifies the missing secure-credential adapter and
requires these states and recovery cases:
`docs/implementation/FEATURE_TASKS.md#cl-101`.

## Minimum decision needed

Approve one narrow, portable account-lifecycle boundary that specifies:

1. a stable, non-secret authenticated-principal handle and explicit credential
   availability/renewal/recovery outcomes (owned by the secure host adapter);
2. a durable account-directory/scope-factory port that maps that handle plus app
   to the issued account, membership, stream, and account-private sync store;
3. identity-switch generation/fencing and retirement semantics, including that
   missing credentials for a known account must return recovery and never cause
   provisioning of another account.

The decision must state which owner publishes each interface and how its durable
mapping is kept distinct from secret-token storage.  It must not infer identity
from `IAuthSession.SessionKey` or change the frozen provisioning/sync wire
contracts without canonical backend ownership.

## Historical result

This was the correct stop state before the architecture review supplied the
portable principal, directory, and scope-lease decision. See `README.md` in this
directory for the subsequently implemented bounded seam and its remaining shared
composition/migration-registration limits.
