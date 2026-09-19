# GamePlatform.Storage.Sqlite

Implements the CL-007 file-backed adapter with one scope-bound connection,
serialized outer transactions, borrowed sessions, immutable migration
checksums, owner validation, foreign keys, WAL and bounded busy handling. It
depends only on Core/storage abstractions plus the reviewed pinned sqlite-net
source; there is no in-memory production fallback or nested commit API.

Windows x64 native execution is covered by the CL-007 tests. The retained
`UnavailableSqliteStore` remains the public M0 compatibility stub and still
throws. Production composition, the coordinated native bundle, Unity
Editor/IL2CPP/AOT and device execution remain unavailable until CL-015/INT-009.
Gates: A03, A05, A09, A13.
