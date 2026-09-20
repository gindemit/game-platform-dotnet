# CL-015 complete bundle and AOT inventory evidence

Date: 2026-09-20

Original packaging source commit `fdc3f19285b83bd011c91f4b16ed090125d023d9`
produces one unpublished managed/native artifact through `scripts/package-sdk.py`.
It includes all 15 SDK assemblies, the eight runtime DLLs selected by the exact
nine-package MessagePack 3.1.8 lock (MessagePackAnalyzer is build-only), portable
symbols, NuGet license metadata/notices, all three SQLite notices, deterministic
managed importer metadata, and the reviewed SQLite binaries plus generated
target-isolated metadata for Windows x86-64, macOS universal and Android ARM64.
The correction does not copy the upstream Windows metadata that enabled unrelated
Linux/macOS/WSA targets. Semantic validation requires exact enabled platforms,
exclusions, CPU, Editor OS and Android alignment before packaging succeeds.

The manifest inventories 23 assemblies, nine packages, three native targets,
38 SQLite P/Invoke entry points and 99 hashed files. It forbids duplicate UPM
acquisition. The included MessagePack codec remains explicitly
`qualification-only`; production `IWireCodec` is unavailable. `link.xml` preserves
only the bounded qualification codec types and `SQLite.SQLite3`; the current
storage adapter uses raw SQL and has no ORM generic materialization path.

The former `2357a74cc7ab4efe94a35275a1c5977976c1cdff845e6e149b22fd4e46ae94fe`
anchor is superseded. It came from two builds in one checkout and did not prove
checkout independence. INT-009 subsequently reproduced three different anchors.
The root cause was checkout-dependent source-control discovery: a GitHub-origin
checkout generated Source Link data while a local-origin clone did not. Portable
PDB bytes therefore changed, which changed deterministic MVIDs and DLL hashes.

Replacement packaging source `7c1f9534df3b245c6d0d30f5b56fc183a1f7758a`
pins the canonical repository URL, full Git revision, path map, CI/deterministic
source mode, and a revision-bound Source Link document while disabling ambient
source-control-manager queries. Debug/source integrity is retained; Source Link
is now explicit instead of being removed. Two independently cloned clean
checkouts with deliberately different origin metadata and fresh bin/obj/package
outputs produced byte-identical complete artifacts and manifests with SHA-256
`a4c36e6c8bbb19821b8f79dbd71622da1a22e85f5ea3b51b01d46a3f38a813b7`.
Both artifacts reported `sourceDirty=false` and `buildSkipped=false`. Fresh
`--verify` checked
the complete inventory, hashes, managed closure, exact package pins/notices,
native targets/import metadata and AOT metadata. Ordinary verification now rejects
any `sourceDirty=true` or `buildSkipped=true` artifact. Actual dirty and skipped-
build development artifacts were explicitly inspected with `--verify-development`
and rejected by ordinary `--verify` with `development_bundle_not_importable`.

## Verification

All canonical commands below exited 0 on Windows NT 10.0.26200.0 x86-64 with
.NET SDK 9.0.203, Python 3.10.4 and Git 2.47.0.windows.2:

- locked restore: 18 projects, zero errors/warnings;
- Release build: 18 projects, zero errors/warnings;
- full SDK tests: 335/335;
- `scripts/validate.py`: 15 projects and 16 evidence-aware feature states;
- script tests: 48/48, including 26 packaging positive/negative cases;
- execution manifest: 52 tasks, 16 ledgers, 21 waves;
- manifest tests: 14/14;
- `python scripts/verify-package-reproducibility.py --source C:/Work/git/gindemit/game-platform-dotnet --revision 7c1f9534df3b245c6d0d30f5b56fc183a1f7758a --sqlite-source C:/Work/git/gindemit/game-platform-dotnet/artifacts/acquisition/unity-sqlite-net --evidence-output C:/Work/git/gindemit/game-platform-dotnet/docs/implementation/evidence/CL-015/replacement-anchor`: two independent clean clones, two ordinary verifies, 23 identical MVIDs, 15 identical portable-PDB hashes, 100 identical complete files including the manifest, and the replacement hash above.

Both complete manifests, clone/package/verify logs, the per-file comparison,
MVID inventory and PDB hashes are archived under `replacement-anchor/`. The
regression tool creates independent clones and fresh outputs; it does not reuse
the invoking checkout's bin, obj or package output.

The added negatives mutate Windows cross-platform enablement, Any-platform
exclusions, Windows/macOS Editor OS/CPU, Android CPU and Android 16 KiB alignment,
and reject dirty/skipped artifacts under normal verification. The alignment case
updates both manifest hashes before invoking default bundle verification, proving
the semantic policy—not only hash validation—fails closed.

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
