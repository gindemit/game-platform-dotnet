# Accepted decisions

- Game-neutral SDK; MrSquare is the first real consumer, with early Core/Features.Contracts import implemented and full live-game adoption still pending.
- Preserve one canonical declaration per responsibility. The deliberately extracted ID/result/state/outcome/progress contracts now belong to GamePlatform.Core and GamePlatform.Features.Contracts; preserve remaining MrSquare.Platform responsibilities until their own coordinated migration. Do not recreate the removed duplicate contracts.
- New distributed IDs are UUIDv7; preserve existing provider IDs. MessagePack UUID bytes are RFC-order bin16.
- C# `long` carries exact signed-64 values. Diagnostic JSON uses decimal strings; timestamps are bounded Unix milliseconds.
- First account-owned launch provisions online and atomically installs a complete bootstrap before Ready.
- Local projection, sequence allocation and immutable outbox insertion share one transaction.
- One committed private-player feed uses app-authorized opaque cursors; push acknowledgement is not a pull checkpoint. Schema-declared opaque tokens use MessagePack bin12..3072 and canonical unpadded base64url only in diagnostic JSON; the outstanding P2 codec defect does not change this approved contract.
- Application use cases own transactions; RewardFulfillment borrows them and uses operation plus business-source idempotency.
- Domain, wire and SQL types remain distinct. Portable async defaults to Task; Unity concerns stay outside.
- The backend owns the canonical protocol. The current G1-approved core mirror is `0.2.0-core-schema.1`, canonical commit `0109936f0ebf232924cec70adb79c4f790b604bc`, with 90 hash-pinned artifacts. `0.1.0-draft` is historical provenance, not the current core-operation baseline. Future changes require reviewed canonical deltas and exact mirrors; execution-branch coordination state records the authorized version.

Schema approval, qualification codec implementation, real C#/TypeScript interoperability, Unity import and native/device/live-game acceptance are separate. G2 is unpassed at the P2/epoch2 review; product trust/reward/legacy decisions remain unresolved. Read [implementation status](IMPLEMENTATION_STATUS.md), [CODEX_CLIENT.md](../CODEX_CLIENT.md), and the backend [G2 source review](https://github.com/gindemit/game-platform-backend/blob/impl/platform-coordinated-2026-09-19/docs/implementation/coordination/G2_REVIEW_2026-09-19.md) for scoped evidence and the next synchronization actions. This wording update changes no runtime code, pins, product policy or gate status.
