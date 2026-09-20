# GamePlatform.Storage.Sqlite

CL-011 adds immutable migration version 3 and a narrow durable feature-state
adapter. An internal typed SQL row is converted to copied storage values, keeping
SQL, feature and wire models distinct. Rows are isolated by the scope-owned
database and authorized view key, reject revision regression, and preserve
bounded opaque extension bytes. Disposable TTL query caches remain outside
SQLite and cannot evict durable records.

Implements the CL-007 file-backed adapter with one scope-bound connection,
serialized outer transactions, borrowed sessions, immutable migration
checksums, owner validation, foreign keys, WAL and bounded busy handling. It
depends only on Core/storage abstractions plus the reviewed pinned sqlite-net
source; there is no in-memory production fallback or nested commit API.

The CL-008 outbox layer persists the owner/stream tuple, checked sequence and
local revision, projection mutation and immutable semantic command in that same
outer transaction. It rejects non-Ready or mismatched streams, makes exact
operation/business-run retries idempotent, rejects changed immutable semantics,
retains terminal rows, recovers expired lease scheduling separately, and reports
clone/gap states without rotating or resequencing attempted work.

Windows x64 native execution is covered by the CL-007 tests. The retained
`UnavailableSqliteStore` remains the public M0 compatibility stub and still
throws. Production composition and Unity Editor/IL2CPP/AOT/device execution remain
unavailable. CL-015 now supplies the coordinated reviewed native inputs and
metadata; their actual consumer import and execution remain INT-009.
Gates: A03, A05, A09, A13.

CL-010 adds durable snapshot staging, a final atomic confirmed-view/cursor/Ready
install, fixed-watermark private pull checkpoints, revision-aware view removal
and tombstones, and transaction-borrowed overlay rebuild. Reset replacement
does not rewrite or delete the immutable outbox. Bootstrap Ready additionally
requires exact authoritative active/retired stream reconciliation with local
terminal, uncertain and pending command continuity. The concrete migration still
belongs to the fixed `SqlitePlatformMigrationRegistry`: the unchanged outbox is
version 1 and the additive private-sync schema is version 2. Hosts open both new
and existing scope-owned databases with the complete registry; applied identities
and checksums are never rewritten. Live HTTP/backend and non-Windows evidence
remain unrun.
