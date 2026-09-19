# Reviewed backend protocol snapshot

Current G1-reviewed schema: **0.2.0-core-schema.1**, canonical commit
`0109936f0ebf232924cec70adb79c4f790b604bc`. Ninety exact canonical artifacts are
pinned by snapshot.json; the original two policy/primitive files are unchanged.
The complete copy tool validates every source digest/path before writing and
publishes the snapshot last. Core schema approval does not certify codecs,
DTO implementation, game integration or runtime interoperability.
The M0 description below is historical. Current review and all C#/TS mappings:
[G1 epoch 2](../docs/implementation/evidence/CL-003/G1-epoch2/README.md).

Protocol version: `0.1.0-draft`. The canonical files are owned by the sibling `game-platform-backend` repository. This directory is an exact reviewed byte mirror, not an independently editable protocol.

`snapshot.json` pins `platform-policy.json` and `v1/primitive-vectors.json` with SHA-256. Run `python scripts/validate.py` to verify local pins. A protocol change begins in the backend, receives compatibility/version review, then is copied explicitly with `scripts/sync-contracts.py --source <backend-root>` and repinned with `scripts/pin-contracts.py --write`. Never silently accept unexpected drift or depend on a moving peer branch in local CI.

M0 contains logical policy and primitive vectors only. It does not contain or prove a MessagePack implementation, cross-language bytes, semantic fingerprint implementation or deployed API.
