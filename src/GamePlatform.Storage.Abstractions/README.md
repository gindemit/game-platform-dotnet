# GamePlatform.Storage.Abstractions

CL-011 adds a narrow transaction-borrowing durable feature-state port. Its
defensively copied payload and bounded extension bytes are storage values, not
feature read models or wire DTOs. A null extension mutation preserves existing
unknown extension bytes during read-modify-write; it never means cache expiry.

Owns meaningful local atomic-use-case ports, depending only on Core. Excludes
SQL strings, native types and universal CRUD. CL-007 adds a scope-bound
serialized transaction port and neutral typed failures; SQL execution remains a
concrete SQLite adapter concern. CL-008 adds immutable command drafts, post-
sequence fingerprinting, admissions and one transaction-bound projection/outbox
callback. Admission success means local durability only, never backend acceptance
or pull-checkpoint progress. CL-009 adds a narrow contiguous lease/finalization
port; terminal delivery state never advances a pull checkpoint. CL-010 adds
distinct bootstrap, projection-mutation and fixed-boundary pull storage values;
these remain separate from remote DTOs and SQL rows. Gates: A03, A05, A09.
