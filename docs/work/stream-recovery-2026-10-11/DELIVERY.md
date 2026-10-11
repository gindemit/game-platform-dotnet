# Stream recovery SDK delivery - 2026-10-11

Reviewed runtime source:
`83b07c6d26f83f383cce031b0c96a9953618aee4` (pushed main).
Established CI [38167553829](https://github.com/gindemit/game-platform-dotnet/actions/runs/38167553829)
passed validation/build/test/package and generated delivery jobs.

Git UPM delivery `0.1.0-dev.138`:
`8429e119e415469bbdb7a35d7ee493b02d44028d`.
Its parent is exactly the reviewed source above. Package content manifest SHA-256:
`0395744b4294e00c8a15faeff54d5e5c1f275f7ba8aa1ebd36b0afd29e219a5e`.
Embedded SDK dependency manifest SHA-256:
`36fdc2447b8d13b1e36a6e6b0302d9e8e11b466b255e038b8653b1b1fec76efc`.
The actual fetched tag has 142 inventoried files plus the manifest, clean source,
buildSkipped false, 15 SDK managed assemblies plus eight managed dependencies and
Windows x86_64/Android ARM64 native payload. Existing native target exclusions remain.

`python scripts/build-upm-package.py --verify --package-version 0.1.0-dev.138`
passed in `C:/Work/sdk-recovery-dev138-20261011`. Fresh nonauthor payload review
independently verified every listed size/hash and found no missing/extra file, and
verified source parent, recovery API metadata and all three SQLite row AOT entries.

All 23 actual dev.138 managed DLLs were copied to the disposable test output at
`C:/Work/sdk-recovery-validation-20261011/tests/GamePlatform.Tests/bin/Release/net9.0`.
`dotnet vstest tests/GamePlatform.Tests/bin/Release/net9.0/GamePlatform.Tests.dll --Logger:trx --ResultsDirectory:artifacts/dev138-runtime-tests`
passed 867/867, zero skips, without rebuilding the libraries. Post-test comparison
confirmed zero DLL hash mismatches against the fetched payload. This is .NET/native
Windows test evidence for the actual delivered managed binaries, not Unity/IL2CPP.

Two independent Windows clean source clones also produced byte-identical draft
packages. Draft bundle SHA-256 `3b1769e8dded8b20c86f54661976390b870f788f4c69ac76145f30c1b830e17e`;
draft UPM manifest SHA-256 `040a10f6df6f1ad833b1d7b5b5c95fdbeec64cd8cdfd56f6b7a0904b308ed203`.
The Linux CI-generated dev.138 managed DLL/PDB hashes differ from that Windows draft;
the separate exact-tag review and generated-DLL tests above qualify the actual tag.

Unity is unchanged and still requests its existing dev.135 delivery. Consumer pin,
host integration, Unity/runtime/device and joined PostgreSQL peer qualification
remain separate. The broader [implementation acceptance limits](IMPLEMENTATION.md)
and workspace
[current receipt](https://github.com/gindemit/game-platform-workspace/blob/main/docs/SDK-RECOVERY-VALIDATION-2026-10-11.md)
remain binding; no held feature or product gate is released. Documentation-only
descendants of the runtime source do not change this exact payload identity.
