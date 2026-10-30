# Purchases

The portable coordinator supports server-verified Google Play consumables and
permanent non-consumable entitlements; it never treats client callbacks as value authority. Apple is
unavailable until its real bridge and verifier exist. The legacy
`PurchasesFeature` facade is not the coordinator.

The native purchase coordinator is a portable, explicitly invoked lifecycle. A
host supplies an actual native bridge and authenticated protected-proof store;
neither fake implementation nor native device support is implied by these
contracts. It reserves proof capacity and persists intent and launch marker
before opening a sheet. The visible price must match fresh provider details.
Callbacks without an independently trusted attempt association use their own
protected token identity and recovery key, even when their SKU matches a sheet.
Previously authenticated durable associations remain valid on replay.
Call recovery and intent refresh on startup and resume; no constructor starts
background work. Server acceptance never applies value locally: delivery requires
the same installed private-feed visibility and epoch at the authorized checkpoint.
Presentation claims are durable by grant ID. The claim and checkpoint belong to
different stores: a reset racing a claim can suppress a celebration, but cannot
produce local value or make the claim evidence of delivery.

Network and platform failures leave attempts or protected proofs unresolved.
Unsupported products, provider credentials, native bridge/Keystore runtime,
actual Play verification, Android device execution and production configuration
remain separate acceptance gates.

Growing protected stores implement `IPagedProtectedProofStore`. Its owner-bound
pages contain at most 32 records, retain one high-water sequence, and must advance
monotonically; the final page reaches that high water even when deleted or filtered
rows leave a gap. An empty continuing page is valid only with sequence progress.
Legacy stores explicitly implement `IBoundedProtectedProofStore` and enforce
`MaximumRecords` (1–256) before allocating; an unbounded list-only adapter is
rejected before its read. `VisitAttemptsAsync` and exact `FindAttemptAsync` let
hosts avoid materializing lifetime history. Remote history uses the validated
`ReadPurchasePageAsync`; the convenience full-history API is limited to 200 rows.

A finalized protected record carries its original UUIDv7 identities and immutable
charge ID. The coordinator does not reopen or resubmit its retired secret and does
not infer delivery of a neighboring unknown attempt. Adapter compaction must
preserve unresolved reservations and proof evidence. The optional
`onAttemptAllocated` callback identifies only a candidate local key; hosts must
perform an exact durable lookup before presenting it as a retained attempt.

`ICommercePrivateProjectionSink` installs exact inventory and entitlement rows
with the private cursor transaction. Existing G3 sinks still reject nonempty
commerce rows. These narrow frozen rows do not invent the richer metadata needed
by the independent inventory/entitlement feature services. Permanent native
content has an entitlement ID and no quantity; receipt acceptance alone does not
make the entitlement owned in the game.
