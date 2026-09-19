# GamePlatform.Storage.Abstractions

Owns meaningful local atomic-use-case ports, depending only on Core. Excludes
SQL strings, native types and universal CRUD. CL-007 adds a scope-bound
serialized transaction port and neutral typed failures; SQL execution remains a
concrete SQLite adapter concern. Durable outbox/projection semantics begin in
CL-008. Gates: A03, A05, A09.
