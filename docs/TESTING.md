# Testing and acceptance

The current repository gate is:

```sh
python3 scripts/validate.py
python3 -m unittest discover -s scripts -p 'test_*.py'
dotnet restore GamePlatform.sln --locked-mode
dotnet format GamePlatform.sln whitespace --verify-no-changes --no-restore --exclude src/GamePlatform.Storage.Sqlite/Vendor/SQLite.cs
dotnet build GamePlatform.sln -c Release --no-restore
dotnet test GamePlatform.sln -c Release --no-build --no-restore
python3 scripts/package-sdk.py
python3 docs/implementation/validate_manifest.py --ready
python3 -m unittest discover -s docs/implementation -p 'test_*.py'
```

`scripts/validate.py` checks topology, JSON, targets/references, feature catalog/readmes and contract hashes. The xUnit suites cover portable contracts, codecs, HTTP, SQLite, synchronization and bounded feature services. Packaging is deterministic and unpublished; it does not deploy or publish anything.

Exact Unity import, IL2CPP, device, browser, PostgreSQL peer and hosted-service results are separate evidence. The current qualified and open boundaries are summarized in [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md) and the campaign checkpoint. Never infer a runtime pass from source/.NET success or report skipped/unrun environments as passing. Older P1/P2 reproduction scripts and their outputs remain dated evidence under `docs/implementation/evidence/`.
