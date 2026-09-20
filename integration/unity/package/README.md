# CL-015 Unity package metadata

This directory is the source-controlled policy input for the unpublished SDK
bundle. `aot-inventory.json` and `link.xml` preserve only the bounded codec
candidate and SQLite P/Invoke type used by the current implementation.
`lifecycle-manifest.json` defines manifest-owned install, upgrade, uninstall and
CL-013 rollback without touching saves, migrations, cursors or commands.

`scripts/package-sdk.py` generates deterministic explicit-reference `.meta` files
for every managed DLL and narrowed native `.meta` files. Native metadata is
semantically validated: Windows x86-64 enables only Windows Editor/Win64, macOS
universal enables only macOS Editor/OSXUniversal, and Android ARM64 enables only
Android ARM64 with 16 KiB alignment. Unsafe broad upstream metadata is not copied.
The bundle is the only acquisition path; adding MessagePack-CSharp or
`com.gilzoide.sqlite-net` through UPM would be a duplicate and is forbidden.

These files are import templates and inventory evidence only. Unity Editor,
managed stripping, IL2CPP, APK inspection and Android device execution remain
unrun and belong to INT-009.

Ordinary verification rejects dirty-source and skipped-build artifacts.
`--verify-development` exists only for explicit non-importable inspection and
prints that status; it cannot qualify an artifact for import or publication.
