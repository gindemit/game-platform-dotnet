# CL-104 — Inventory portable service evidence

Task state: dependency-ready SDK implementation; M4/C-D. A06/A07 and G3 remain
unpassed.

`InventoryService` stores only server-confirmed stack and instance projections.
It validates active entries against an exact captured catalog app binding and
keeps use/consume commands as separate, durable pending intentions. An injected
authority must authorize an operation kind before it reaches the existing atomic
outbox seam; pending state never changes quantities or creates an instance.
An accepted receipt retains its intent with an explicit resulting inventory
revision until an authoritative projection reaches that revision; older pulls
and unrelated pending intents are preserved. Rejection is idempotent and only
removes the matching pending intent. If CL-010 commits a borrowed projection and
cursor before inventory persists cleanup, durable reads still suppress only the
accepted intent whose resulting revision has been reached; awaiting-receipt and
unrelated intents remain visible after restart.

The service has no grant path, spend engine, wire DTO, HTTP provider, SQLite
migration, private-sync cursor writer, Unity composition, or production codec.
CL-010 remains owner of atomic raw feed group/cursor installation. A later
adapter must only decode an already-committed projection, with its exact catalog
visibility generation, into this feature service. `ApplyConfirmedProjection`
borrows that same transaction and requires the caller's prior durable revision;
it cannot advance a cursor itself.

Focused native SQLite cases prove confirmed stack/instance plus pending outbox
intent reopen together, revision regression/same-revision conflict rejection,
app/view isolation, unavailable-catalog rejection, offline intent denial,
duplicate intent replay deduplication, rejection removal without holding change,
quantity bounds, duplicate identities and tombstone shape. The corrective cases
also prove receipt→older pull→confirming pull reconciliation, unrelated pending
preservation, order-independent same-revision equivalence, and rollback/commit
with a cursor-side SQLite sentinel in the borrowed transaction, plus borrowed
projection/cursor commit followed by reopen/read reconciliation. They are local
Windows x64 evidence only; they are not peer/backend, Unity, device, A06/A07 or
G3 acceptance.
