# Unity package metadata and delivery

This directory contains source-controlled policy input for the generated SDK
bundle and Git UPM package. `aot-inventory.json` and `link.xml` preserve only the
bounded codec candidate and SQLite P/Invoke type used by the current implementation.
`lifecycle-manifest.json` defines manifest-owned install, upgrade, uninstall and
CL-013 rollback without touching saves, migrations, cursors or commands.

`scripts/package-sdk.py` generates deterministic explicit-reference `.meta` files
for every managed DLL and narrowed native `.meta` files. Native metadata is
semantically validated: Windows x86-64 enables only Windows Editor/Win64, macOS
universal enables only macOS Editor/OSXUniversal, and Android ARM64 enables only
Android ARM64 with 16 KiB alignment. Unsafe broad upstream metadata is not copied.
The bundle is the only acquisition path; adding MessagePack-CSharp or
`com.gilzoide.sqlite-net` through UPM would be a duplicate and is forbidden.

`scripts/package-sdk.py` builds the deterministic source bundle;
`scripts/build-upm-package.py` exports that verified bundle to
`upm/com.gindemit.game-platform`. The package includes the complete managed
dependency closure, target-filtered native adapters, stable DLL/native `.meta`
files, AOT metadata, lifecycle metadata, dependency licenses and source
provenance. Managed importer metadata explicitly enables Any and Editor,
excludes WebGL, and requires asmdef references. The GUID overrides in
`managed-guid-overrides.json` preserve the Core and Features.Contracts
serialized references recorded by the Unity consumer. It does not duplicate or
compile the SDK source. The UPM payload is
generated and committed with its input code; do not edit generated files by hand.

Install from Unity's project manifest using a full Git commit and the package
subpath, for example:

```json
{
  "dependencies": {
    "com.gindemit.game-platform": "https://github.com/gindemit/game-platform-dotnet.git?path=upm/com.gindemit.game-platform#<package-delivery-commit>"
  }
}
```

The package-delivery commit is distinct from `sourceCommit` in
`package-content-manifest.json`: the latter identifies the source revision used
to build the binaries and avoids a circular self-hash. The content manifest
binds every package file by path, byte count and SHA-256. Pin consumers to the
Git package-delivery commit; do not add MessagePack-CSharp or
`com.gilzoide.sqlite-net` as second package acquisitions.

The package's generated importer metadata enables only Android ARM64 (16 KiB
aligned) and Windows x86-64 native targets. The pinned macOS dylib remains in
the source bundle for disposable qualification only and is deliberately omitted
from the production UPM payload. WebGL, Linux and other unsupported native
targets are excluded. Consumers must keep platform-only managed references
outside WebLite through reviewed Unity assembly boundaries.

These files are import policy and inventory evidence; package generation and
metadata checks do not prove Unity import, managed stripping, IL2CPP, APK
inspection or Android device execution. Those exact-artifact checks remain the
Unity integrator's acceptance work.

Ordinary verification rejects dirty-source and skipped-build artifacts.
`--verify-development` exists only for explicit non-importable inspection and
prints that status; it cannot qualify an artifact for import or publication.
