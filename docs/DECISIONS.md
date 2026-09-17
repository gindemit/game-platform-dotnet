# Accepted decisions

- Game-neutral SDK; MrSquare remains only a future consumer.
- Preserve the existing `MrSquare.Platform` authority until coordinated extraction.
- New distributed IDs are UUIDv7; preserve existing provider IDs. MessagePack UUID bytes are RFC-order bin16.
- C# `long` carries exact signed-64 values. Diagnostic JSON uses decimal strings; timestamps are bounded Unix milliseconds.
- First account-owned launch provisions online and atomically installs a complete bootstrap before Ready.
- Local projection, sequence allocation and immutable outbox insertion share one transaction.
- One committed private-player feed uses app-authorized opaque cursors; push acknowledgement is not a pull checkpoint.
- Application use cases own transactions; RewardFulfillment borrows them and uses operation plus business-source idempotency.
- Domain, wire and SQL types remain distinct. Portable async defaults to Task; Unity concerns stay outside.
- The backend owns the canonical protocol. This repository keeps a reviewed SHA-256-pinned mirror at version `0.1.0-draft`.
