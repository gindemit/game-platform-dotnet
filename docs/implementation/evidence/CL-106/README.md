# CL-106 — Entitlements portable service

Source-owned evidence is recorded by the coordinator after integration. This
worker branch implements only the portable feature boundary and focused real
SQLite tests:

- confirmed, revision-monotonic rights with receipt-origin identity;
- active/revoked/expired/unknown presentation, including explicit offline
  `ExpiryUncertain` for active rights carrying a server expiry;
- app-visible catalog validation, captured owner/generation fencing, bounded
  opaque payloads and extension validation;
- no IAP verification, local grant, device-clock eligibility decision, private
  feed adapter, production codec, Unity composition or peer proof.

The test command and exact result are appended by the coordinator from the
committed integrated head. A06/A07 and PEER-VALUE remain unpassed until a real
private transaction group and consumer path are exercised.
