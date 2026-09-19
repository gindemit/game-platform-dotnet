# Managed/native SDK bundle

This unpublished CL-015 bundle contains the exact managed assemblies, portable
symbols, transitive runtime DLLs, actual assembly references, target-framework
metadata, hashes, licenses and source commit listed in
`dependency-manifest.json`. It also carries reviewed SQLite native inputs for
Windows x86-64 and macOS Editors/standalone plus Android ARM64, adjacent upstream
import metadata, and the targeted AOT/linker inventory. A `sourceDirty=true` or
`buildSkipped=true` artifact is development evidence and must not be imported.

The MessagePack assembly and eight runtime package DLLs are included only as a
qualification candidate; the ninth pinned package is a build-only analyzer. The
candidate does not implement production `IWireCodec`. Missing or unknown managed
references, packages, native targets, metadata or notices fail packaging.

Build from a clean committed checkout with
`rtk proxy python scripts/package-sdk.py`. The script acquires the exact SQLite
candidate into ignored `artifacts/acquisition`, or `--sqlite-source` can name an
existing clean checkout at the same revision. Verify with
`rtk proxy python scripts/package-sdk.py --verify`. Output is `artifacts/sdk`;
the prior verified output is retained at `artifacts/sdk-previous`. Run clean
builds twice and compare complete manifests. Byte equality does not certify
Unity or device behavior.

This is not a NuGet feed and nothing is published. Import only assemblies named
by a consumer's explicit precompiled-reference list plus their full declared
closure; never auto-reference every DLL. Install each runtime once and do not
acquire MessagePack-CSharp or unity-sqlite-net again through UPM.

Follow `lifecycle-manifest.json` for install, upgrade, uninstall and the exact
CL-013 rollback anchor. Never rewrite identities, cursors, immutable commands,
migration journals or saves. Unity import/compilation, stripping, IL2CPP/APK
inspection and physical-device execution belong to INT-009 and remain unrun.
