# CL-015 complete bundle and AOT inventory evidence

Date: 2026-09-20

Packaging source commit `bb4136a9e11fb142980c57c0a8d5ca251a634d29`
produces one unpublished managed/native artifact through `scripts/package-sdk.py`.
It includes all 15 SDK assemblies, the eight runtime DLLs selected by the exact
nine-package MessagePack 3.1.8 lock (MessagePackAnalyzer is build-only), portable
symbols, NuGet license metadata/notices, all three SQLite notices, deterministic
managed importer metadata, and the reviewed SQLite binaries plus original
importer metadata for Windows x86-64, macOS universal and Android ARM64.

The manifest inventories 23 assemblies, nine packages, three native targets,
38 SQLite P/Invoke entry points and 99 hashed files. It forbids duplicate UPM
acquisition. The included MessagePack codec remains explicitly
`qualification-only`; production `IWireCodec` is unavailable. `link.xml` preserves
only the bounded qualification codec types and `SQLite.SQLite3`; the current
storage adapter uses raw SQL and has no ORM generic materialization path.

Two clean full builds at the packaging source commit produced byte-identical
`dependency-manifest.json` files with SHA-256
`c151e2b3c89f07985877e1c8da5ab4d37f2cb0bd15256d2bdb0baaabf953c6c7`.
Both artifacts reported `sourceDirty=false` and `buildSkipped=false`; the second
run retained the first under `artifacts/sdk-previous`. Fresh `--verify` checked
the complete inventory, hashes, managed closure, exact package pins/notices,
native targets/import metadata and AOT metadata.

## Verification

All canonical commands below exited 0 on Windows x86-64 with .NET SDK 9.0.203:

- locked restore: 18 projects, zero errors/warnings;
- Release build: 18 projects, zero errors/warnings;
- full SDK tests: 335/335;
- `scripts/validate.py`: 15 projects and 16 evidence-aware feature states;
- script tests: 38/38, including 16 packaging positive/negative cases;
- execution manifest: 52 tasks, 16 ledgers, 21 waves;
- manifest tests: 14/14;
- bundle build twice plus `--verify`: 23 assemblies, three native targets, exact
  identical manifest hash above.

One superseded attempt started build and test concurrently and produced 18
MSBuild destination-copy retry warnings. No command failed; the canonical
sequential restore/build/test rerun above was clean and is the accepted result.

## Limits and rollback

No Unity repository was changed. Unity Editor import/compilation, managed
stripping, IL2CPP generation/link, APK inspection, Android ARM64 database/codec
execution and physical-device behavior were not run. macOS native execution was
also not run. These remain explicit INT-009/device gates and prevent production
codec/native acceptance; CL-015 stays `in_progress` at the packaging-complete
checkpoint.

`lifecycle-manifest.json` defines manifest-owned install, upgrade and uninstall
and pins the prior CL-013 manifest for rollback. Rollback never deletes or
downgrades databases, identities, cursors, immutable commands or player saves.
If an earlier application cannot read the retained schema, rollback is blocked
and forward recovery is required.
