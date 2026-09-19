# Pinned sqlite-net source

`SQLite.cs` comes from `gilzoide/unity-sqlite-net` 1.3.2 commit
`08248bd5884d8eb932a837aa56d4ff456daf913f`. The original file SHA-256 is
`73d35895222b80c54cd5e5531db5db1c0213e35db09e18937054e0e11ce3a30f`.

The portable SDK copy has one reviewed source-only adaptation: eight mapping
attributes derive from sqlite-net's own `SQLite.PreserveAttribute` rather than
`UnityEngine.Scripting.PreserveAttribute`, and that local base is unsealed.
This removes a forbidden UnityEngine assembly dependency from the netstandard
adapter. The compiled file SHA-256 is
`2157936811c69cbd235a4545415c467b76e88f4b645853b88ca7c00c7ef21a4d`.
CL-015 now supplies targeted linker preservation for `SQLite.SQLite3`; no Unity
stripping, IL2CPP or device execution has occurred, so this remains metadata and
not AOT/device acceptance.

The three adjacent MIT notices are retained from the pinned package, embedded
sqlite-net source and SQLite3 Multiple Ciphers native source. The latter notice
has SHA-256 `ef17378697b38c803a91cd82430dac4be3d6e610c3569ddb3aa547d8d084f983`.
The Windows x64 native test input remains byte-identical to the CL-006 pin.
CL-015 bundles the reviewed Windows x86-64, macOS universal and Android ARM64
inputs with their original import metadata. Other platforms and all Unity/device
execution remain unsupported or unverified.
