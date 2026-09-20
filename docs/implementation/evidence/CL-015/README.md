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
acquisition. At this packaging checkpoint the included MessagePack codec remained
explicitly `qualification-only`; the promotion follow-up below supersedes that
availability limit. `link.xml` preserves
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

## Production codec promotion follow-up — 2026-09-20

Unity INT-009 commit `ff65dbed550b390c4129f471bf098559be37f5ea`
consumed the independently accepted replacement anchor and passed its complete
pinned approved G2 codec corpus twice on physical Android ARM64 under IL2CPP and
high stripping. SDK source `684ed49c832f203326270546414244bc2adb78e8`
therefore adds the production `MessagePackWireCodec` adapter and targeted linker
entry over the unchanged qualified implementation. A fresh ordinary package and
verify pass with manifest SHA-256
`34febc70a5cc0b1b01f05075b3c26c75e131acc055511c038b033cd40e726893`.

CL-015 remains `in_progress`: the promoted artifact has not yet received the
two-clean-clone reproducibility rerun, and macOS native execution remains unrun.
No package was published.

## HTTP artifact source-byte correction — 2026-09-20

At source `81376de9c0375da7091c59797283678ae527a29d`, the working checkout
manifest `538c133332a91b136758027f7a7dda0ddc78b05583b0607e86f17bfa6b993fce`
differed from fresh clone manifest
`78fc59e55f14728d64e692fea8d57b836416d54bc9b9e9b513f492a620607389`.
The only source-byte difference in the affected projects was the generated
`TypedQualificationCodec.g.cs`: 109094 bytes with 1149 CR characters versus
107945 LF-only bytes, identical after CR removal. Git's LF normalization and the
generator's text-mode check hid this difference. Portable PDB document checksums
changed the MessagePack DLL/PDB/MVID and dependent HTTP DLL/PDB/MVID.

Source `67f79fc600ec90b9465f1c9a78ae123a4fe03c4d` makes the generator write
explicit UTF-8/LF and check exact bytes. The new check rejected the old file;
regeneration passed without a tracked generated-source content change. Symbols,
Source Link and exact package/native/license checks are preserved. Packaging
regressions pass 29/29.

One invocation of `scripts/verify-package-reproducibility.py --source
C:/Work/git/gindemit/game-platform-dotnet --revision
67f79fc600ec90b9465f1c9a78ae123a4fe03c4d --sqlite-source
C:/Work/git/gindemit/game-platform-dotnet/artifacts/acquisition/unity-sqlite-net
--evidence-output C:/Work/git/gindemit/game-platform-dotnet/artifacts/cl015-lf-reproduction`
passes with 100 byte-identical files, 23 equal MVIDs and 15 equal PDB hashes.
The manifest SHA-256 is
`851856125f7109b871699a8a04bbe1ac1ddc8de9cb795d3a69b83eaf041907cc`.
Both clean-clone ordinary verifies pass. The corrected working checkout's ordinary
package and verify also pass and all 100 files match the independent inventory.
An initial local packaging attempt correctly rejected a stale Git stat entry after
LF regeneration; refreshing that unchanged index entry enabled the clean build.
Locked restore/Release rebuild report zero warnings/errors. Logs, manifests and
the complete hash comparison are archived in `lf-reproduction/`.

This anchor supersedes the HTTP artifact above. CL-015 remains `in_progress` for
macOS native execution; new HTTP Unity/device/live acceptance remains unverified.
No runtime behavior, dependency pin, protocol or Unity import changed.

## G3 current-bundle refresh — 2026-09-20

The G3 SDK-head refresh starts from requested coordinated-branch source
`686721a6a5f0fcd3525d4b6a0002406abbad061a`. Its packaging-only corrections
are source commit `b494b6dd0dc9a9cf1692337ba18ce7490838aec6`: the package
manifest/verifier and the generated bundle README now accurately require and
describe the already-promoted production `MessagePackWireCodec`, rather than
incorrectly declaring it unavailable. This does not add a package, a serializer
fallback, a Unity dependency, or a protocol change.

The unpublished artifact at
`C:/Work/git/gindemit/game-platform-dotnet-g3bundle-20260920/artifacts/sdk`
has manifest SHA-256
`c911e88c0d2ebd083c59ece912b792bdc97882b1866f9b61d89064ae7a3bb4fb`.
It contains 23 managed assemblies (all 15 current SDK assemblies and the exact
eight runtime MessagePack closure assemblies; MessagePackAnalyzer remains
build-only), three pinned SQLite native targets, nine pinned package records,
38 P/Invoke declarations and 100 files including the manifest. The closure
inspector accepts every current assembly reference. `GamePlatform.Sync` itself
does not declare a direct serializer reference; its portable references close
inside the bundle, while `GamePlatform.Transport.Http` declares the reviewed
`GamePlatform.Serialization.MessagePack` edge and the complete MessagePack
runtime closure is present exactly once.

Fresh locked restore/Release rebuild, normal package verification, 29 packaging
positive/negative tests, 485 SDK tests, architecture/contract validation and
19 validator tests all passed with zero build warnings/errors. Two independent
clean clones with deliberately different origin metadata rebuilt and normally
verified byte-identical complete artifacts (100 files including manifest); the
external retained reproduction summary is
`C:/Work/git/gindemit/sdk-bundle-evidence-g3-20260920-r2/reproducibility-summary.json`.
See `g3-current-bundle-2026-09-20.json` for exact command and inventory facts.

This is packaging evidence only. The refreshed bundle still needs the real
Unity import/IL2CPP/device/live-host journey before CL-015 or G3 can be marked
complete; no package was published.
