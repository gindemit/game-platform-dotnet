# Repository tooling

- `validate.py`: topology, allowlist, feature/readme and contract-pin checks.
- `test_validation.py`: seven validator self-tests.
- `sync-contracts.py`: explicit copy from a reviewed backend root.
- `pin-contracts.py`: verify pins; `--write` is an explicit reviewed action.
- `fetch-source-docs.py`: read a path at the pinned Unity commit without modifying it.
- `package-sdk.py`: create local managed DLL/NuGet artifacts and hash manifest; never publishes or claims native libraries.
