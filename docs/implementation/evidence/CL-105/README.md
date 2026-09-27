# CL-105 — wallet portable service evidence

Task state: bounded SDK implementation; M4/C-D. This evidence does not pass
A01, A06, A07, G3 or any Unity/live-backend gate.

## Implemented boundary

`WalletService` is constructed for one captured backend/app/account/view/
generation. It persists server-confirmed `long` balances under a wallet-scoped
feature namespace, carrying a stable definition ID and semantic key. It has no
local credit, grant, balance arithmetic or offline purchase authority.

Every installed, refreshed and public-read balance requires the exact captured owner's
catalog snapshot to bind the same definition ID and semantic key as a visible
`Currency`; spend admission additionally requires that binding's `Spend`
permission. Missing/revoked, wrong-kind and foreign-owner catalog data fails
closed before a projection install, balance exposure or outbox admission. A
matching retained terminal receipt remains reconcilable after a binding is
revoked: the owner/operation-fenced mutation preserves terminal state but does
not debit, credit, expose a new catalog read or admit another command.

An immutable spend intent carries operation, stream, business-source, expected
confirmed revision, currency and positive exact amount. The service calls its
injected remote boundary for fresh online authorization of that exact intent
before atomically committing `wallet.spend` and the separately labelled pending
state. A matching receipt moves only the intent to `AcceptedAwaitingPull`; the
balance remains unchanged until a monotonic confirmed pull reaches the receipt's
resulting revision. A retained rejection remains an explicit error state.

The narrow borrowed `ApplyConfirmed` mutation now accepts a caller-owned
`ILocalStorageTransaction` plus the explicit durable prior revision observed in
that same private projection/cursor transaction. It verifies exact owner and
visible catalog binding, validates codec/extensions, and rejects equal/lower
revision replacement before writing. The convenience async path routes every
advancing write through that same mutation. Accepted pending cleanup is a read
overlay, so receipt state is suppressed after the stored confirming revision
survives a borrowed transaction and restart; awaiting/rejected states remain.

The remote, codec, SQLite composition and command fingerprint interfaces are
injected. Test fakes are not production providers. No wire DTO, HTTP route,
server-side funds algorithm, wallet ledger, reward grant, Unity composition or
migration registration was added.

### G3 frozen semantic-wallet compatibility path

`G3WalletProjectionService` is separately namespaced and semantic-key-only for
the frozen G3 wallet payload. It does not fabricate a catalog definition ID or
weaken `WalletService` catalog/spend admission. There is no convenience async
write: a bootstrap/pull owner must borrow its existing SQLite transaction and
provide the observed prior source revision. Revision zero is valid; absent,
removal, and reset reads are `Missing`, never a zero balance. The local envelope
is bounded and versioned, and durable extensions fail closed.

Real SQLite tests cover absence, revision zero and signed-64 balance extrema,
rollback/commit/reopen with a cursor sentinel, equal/lower changed payload
rejection, explicit reset, and exact account/view isolation. This remains only
a portable cached projection: it adds no wallet wire/API, codec composition,
backend transcript, spend, credit, grant, or Unity composition.

## Local Windows x64 evidence

At task worktree head (uncommitted while this evidence was written):

```text
rtk dotnet build GamePlatform.sln -c Release --no-restore
exit 0; 18 projects, zero warnings/errors

rtk dotnet test tests/GamePlatform.Tests/GamePlatform.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~Cl105WalletServiceTests
exit 0; 9 passed, 0 failed/skipped/warnings

rtk dotnet test GamePlatform.sln -c Release --no-build --no-restore
exit 0; 433 passed, 0 failed/skipped/warnings
```

The real pinned SQLite cases prove confirmed/pending/outbox atomicity and reopen,
`long.MinValue`/`long.MaxValue` preservation, invalid/overflow-sign spend amount
rejection, stale and offline spend denial without an outbox row, duplicate
receipt idempotency, receipt followed by an older pull then confirming group,
two-device divergent accepted/rejected outcomes, rejection visibility, and
same-currency view namespace/owner fencing. The additional catalog case proves
missing/unavailable and subsequently revoked bindings, wrong resource kind,
foreign owner, and spend-disabled binding rejection before outbox admission.
It also proves a post-admission revocation cannot suppress a matching accepted
or rejected terminal receipt, and neither receipt changes the confirmed balance.
The borrowed-transaction cases use a real SQLite cursor sentinel to prove rollback
and commit/reopen atomicity, logical accepted-overlay suppression after a borrowed
confirming revision, and rejection of equal/lower revision overwrite, foreign
owner and invisible catalog input.

## Remaining limits

The required `PEER-VALUE` server/provider contract remains unavailable at this
feature boundary. There is no real backend transcript, production codec/HTTP
adapter, server authorization/funds validation evidence, shared-row multi-app
exercise, physical kill/disk/corruption test, Unity IL2CPP/AOT/device test,
INT-010 journey or independent review. Cached balances remain display-only until
such an online backend authority is composed and verified.

## Inherited storage-runner observation

One earlier full-suite attempt in this isolated worktree failed only the
storage-owned `Cl007P3ExtensionMigrationTests.ConcurrentOpenUsesOneExtensionJournalAndChecksumsIncludeDescriptorIdentity`
case: 402 passed/1 failed, where a concurrent initializer observed
`table gp_stream_state already exists`. No CL-105 code owns or changes that
test/migration boundary. The immediate repeat passed 403/403, and five focused
repetitions of that exact CL-007 case passed 1/1 each (0/5 failures). The
observation was reported to the coordinator; it is not treated as CL-105
acceptance evidence or as a storage fix.
