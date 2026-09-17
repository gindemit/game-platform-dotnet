# Reviewed backend protocol snapshot

Protocol version: `0.1.0-draft`. The canonical files are owned by the sibling `game-platform-backend` repository. This directory is an exact reviewed byte mirror, not an independently editable protocol.

`snapshot.json` pins `platform-policy.json` and `v1/primitive-vectors.json` with SHA-256. Run `python scripts/validate.py` to verify local pins. A protocol change begins in the backend, receives compatibility/version review, then is copied explicitly with `scripts/sync-contracts.py --source <backend-root>` and repinned with `scripts/pin-contracts.py --write`. Never silently accept unexpected drift or depend on a moving peer branch in local CI.

M0 contains logical policy and primitive vectors only. It does not contain or prove a MessagePack implementation, cross-language bytes, semantic fingerprint implementation or deployed API.
