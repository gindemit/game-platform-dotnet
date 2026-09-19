# CL-015 Unity package metadata

This directory is the source-controlled policy input for the unpublished SDK
bundle. `aot-inventory.json` and `link.xml` preserve only the bounded codec
candidate and SQLite P/Invoke type used by the current implementation.
`lifecycle-manifest.json` defines manifest-owned install, upgrade, uninstall and
CL-013 rollback without touching saves, migrations, cursors or commands.

`scripts/package-sdk.py` generates deterministic explicit-reference `.meta` files
for every managed DLL and copies the original pinned native plugin `.meta` files.
The bundle is the only acquisition path; adding MessagePack-CSharp or
`com.gilzoide.sqlite-net` through UPM would be a duplicate and is forbidden.

These files are import templates and inventory evidence only. Unity Editor,
managed stripping, IL2CPP, APK inspection and Android device execution remain
unrun and belong to INT-009.
