# CL-104 — Inventory portable service evidence

Task state: dependency-ready SDK implementation; M4/C-D. A06/A07 and G3 remain
unpassed.

`InventoryService` stores only server-confirmed stack and instance projections.
It validates active entries against an exact captured catalog app binding and
keeps use/consume commands as separate, durable pending intentions. An injected
authority must authorize an operation kind before it reaches the existing atomic
outbox seam; pending state never changes quantities or creates an instance.

The service has no grant path, spend engine, wire DTO, HTTP provider, SQLite
migration, private-sync cursor writer, Unity composition, or production codec.
CL-010 remains owner of atomic raw feed group/cursor installation. A later
adapter must only decode an already-committed projection, with its exact catalog
visibility generation, into this feature service.

Focused native SQLite cases prove confirmed stack/instance plus pending outbox
intent reopen together, revision regression/same-revision conflict rejection,
app/view isolation, unavailable-catalog rejection, offline intent denial,
duplicate intent replay deduplication, rejection removal without holding change,
quantity bounds, duplicate identities and tombstone shape. They are local
Windows x64 evidence only; they are not peer/backend, Unity, device, A06/A07 or
G3 acceptance.
