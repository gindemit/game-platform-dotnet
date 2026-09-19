# Testing and acceptance

`scripts/validate.py` checks required topology, JSON, project targets/references, feature catalog/readmes and contract hashes. `scripts/test_validation.py` tests the validator. xUnit tests cover M0 typed fail-closed behavior and basic legacy-compatibility value semantics. `scripts/package-sdk.py` builds local NuGet/DLL artifacts and a dependency manifest without publishing.

A01–A13 remain pending except M0 scaffold evidence. Portable build does not prove native SQLite, Unity import, IL2CPP/AOT, PostgreSQL transactions or any host runtime. Never report skipped/unrun environments as passing.

P1 reproduction: `rtk proxy python docs/implementation/evidence/verify_p1.py` runs architecture/manifest checks and their tests, locked restore, Release build, nonzero full and diagnostics xUnit selections, dependency listing and local non-publishing packaging. It archives raw outputs and TRX counts under `docs/implementation/evidence/P1/`. The separate Windows native probe is `rtk proxy pwsh -NoProfile -File integration/unity/sqlite-qualification/run-desktop-probe.ps1`; it uses pinned upstream source/native hashes and refuses existing database files. Neither command proves Unity/game adoption or cross-language codecs.
