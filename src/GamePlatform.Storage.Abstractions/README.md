# GamePlatform.Storage.Abstractions

Owns meaningful local atomic-use-case ports, depending only on Core. Excludes
SQL strings, native types and universal CRUD. CL-007 adds a scope-bound
serialized transaction port and neutral typed failures; SQL execution remains a
concrete SQLite adapter concern. CL-008 adds immutable command drafts, post-
sequence fingerprinting, admissions and one transaction-bound projection/outbox
callback. Admission success means local durability only, never backend acceptance
or pull-checkpoint progress. Gates: A03, A05, A09.
