# CL-108 — Progression portable-service evidence

Task state: dependency-ready SDK implementation, M3/C-D. This is local Windows
x64 SQLite evidence only; A03/A05/A07, PEER-CORE, Unity/device integration and
G3 are not passed.

`ProgressionService` accepts a game-owned generic `GameplayOutcome` with stable
operation and business-source identity, semantic content version, captured
owner/view/generation, and the explicit bounded
`client_trusted_unvalidated` outcome authority. It does not validate puzzle,
session, replay, score, content, or checkpoint rules; it does not allocate
progress, wallet value, inventory, entitlement, or reward values.

The service writes its pending completion projection and immutable
`gameplay.session.completed` outbox command through `IAtomicCommandStore` in one
transaction. A receipt moves only its matching item to
accepted-awaiting-pull. Only an explicit pull-derived confirmation may remove
that already-accepted item and install a monotonic server-owned semantic-state
projection; unrelated pending work survives. Terminal rejection removes only the
matching pending completion. The codec is a narrow future adapter boundary for
the frozen gameplay/projection contracts, not a new backend wire contract.

Focused command:

`dotnet test tests/GamePlatform.Tests/GamePlatform.Tests.csproj -c Release --filter FullyQualifiedName~Cl108ProgressionServiceTests`

Result: 6/6 tests passed. The native cases cover atomic completion/outbox plus
reopen, duplicate callback idempotency and immutable identity conflict, lost ACK
then explicit acceptance/pull confirmation, terminal rejection followed by next
sequence while retaining another pending item, owner/generation fencing, reset
projection retention, and signed-64 boundary preservation. They do not prove a
real backend outcome/reward receipt, production codec, live command sender,
Unity completed-run writer, or any G3 journey.
