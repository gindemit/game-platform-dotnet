# Managed/native SDK bundle

This unpublished CL-015 bundle contains the exact managed assemblies, portable
symbols, transitive runtime DLLs, actual assembly references, target-framework
metadata, hashes, licenses and source commit listed in
`dependency-manifest.json`. It also carries reviewed SQLite native inputs for
Windows x86-64 and macOS Editors/standalone plus Android ARM64, deterministic
target-isolated import metadata, and the targeted AOT/linker inventory. Unsafe
broad upstream native metadata is not copied. A `sourceDirty=true` or
`buildSkipped=true` artifact is development evidence: ordinary `--verify` rejects
it and it must not be imported.

The MessagePack assembly and eight runtime package DLLs include the promoted
bounded production `MessagePackWireCodec`; the ninth pinned package is a
build-only analyzer. This does not supply a host, credentials, Unity HTTP
executor or live-backend acceptance. Missing or unknown managed references,
packages, native targets, metadata or notices fail packaging.

Build from a clean committed checkout with
`rtk proxy python scripts/package-sdk.py`. The script acquires the exact SQLite
candidate into ignored `artifacts/acquisition`, or `--sqlite-source` can name an
existing clean checkout at the same revision. Verify with
`rtk proxy python scripts/package-sdk.py --verify`. Output is `artifacts/sdk`;
the prior verified output is retained at `artifacts/sdk-previous`. Run clean
builds twice and compare complete manifests. Byte equality does not certify
Unity or device behavior.

`--verify-development` performs only an explicit development inspection and
prints that the artifact remains non-importable and unpublished. It never turns
`--allow-dirty` or `--no-build` output into a release candidate.

This is not a NuGet feed and nothing is published. Import only assemblies named
by a consumer's explicit precompiled-reference list plus their full declared
closure; never auto-reference every DLL. Install each runtime once and do not
acquire MessagePack-CSharp or unity-sqlite-net again through UPM.

Follow `lifecycle-manifest.json` for install, upgrade, uninstall and the exact
CL-013 rollback anchor. Never rewrite identities, cursors, immutable commands,
migration journals or saves. Unity import/compilation, stripping, IL2CPP/APK
inspection and physical-device execution are exact-consumer acceptance gates,
not properties of bundle generation. Consult `docs/IMPLEMENTATION_STATUS.md`
for the qualified package pin and remaining current gates.
