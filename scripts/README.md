# Repository tooling

- `validate.py`: topology, allowlist, feature/readme and contract-pin checks.
- `test_validation.py`: validator self-tests for topology, references, package policy, feature evidence and contract pins.
- `sync-contracts.py`: explicit copy from a reviewed backend root.
- `pin-contracts.py`: verify pins; `--write` is an explicit reviewed action.
- `fetch-source-docs.py`: read a path at the pinned Unity commit without modifying it.
- `package-sdk.py`: create and verify the unpublished CL-015 managed/transitive/native bundle, notices, importer metadata and AOT inventory; never publishes it or claims Unity/device execution.
- `verify-package-reproducibility.py`: clone a pinned revision twice into independent temporary checkouts, vary origin metadata, build fresh bundles, verify them, and archive complete hash/MVID/PDB comparisons.
