# Dependencies

The P2 MessagePack qualification adapter pins MessagePack-CSharp 3.1.8 and its
exact transitive closure in generated lockfiles. Other runtime projects remain
package-free. See [the dependency review](implementation/dependencies/CL-004-messagepack.md).
Test packages are centrally pinned in `Directory.Packages.props`.

Neither dependency was installed or validated in historical M0. The initial
Unity SQLite candidate remains gilzoide/unity-sqlite-net. Microsoft.Data.Sqlite is
not an approved substitution. UniTask may appear only at a Unity boundary.
VContainer, reactive frameworks, logging implementations and JSON libraries
require explicit reviewed adoption with license, AOT/runtime and allowlist evidence.

CL-013's early imported bundle deliberately excluded the qualification MessagePack
assembly and its runtime dependencies. CL-015 now produces a separate unpublished
23-assembly bundle: all 15 SDK assemblies plus the exact eight-assembly runtime
closure for the nine pinned MessagePack packages (the analyzer is build-only).
It carries exact notices, hashes and deterministic explicit-reference Unity
metadata. The later `MessagePackWireCodec` promotion reuses the same bounded
implementation after Android ARM64 IL2CPP/high-stripping corpus acceptance; it
does not alter the existing Core/Features.Contracts consumer import.

P1 qualifies `gilzoide/unity-sqlite-net` 1.3.2 at `08248bd5884d8eb932a837aa56d4ff456daf913f` using a real Windows x64 desktop transaction/reopen probe. See the [qualification decision](implementation/dependencies/CL-006-sqlite-candidate.md) and [exact input manifest](../integration/unity/sqlite-qualification/qualification.json).

CL-007 compiles the pinned managed source into `GamePlatform.Storage.Sqlite`.
The portable copy replaces the package's eight UnityEngine preserve bases with
sqlite-net's local preserve marker and unseals that local marker; the reviewed
patch and original/compiled hashes are recorded beside the source. Real tests
use the exact pinned Windows x64 native DLL
`e7e370938925dff66d9a4d50bfac6d18cc9505bd2f3f127a232b410491b46e3e`.
All three MIT notices are retained. This does not install a Unity package or
complete Unity/device acceptance. CL-015 packaging now includes the reviewed
Windows x86-64, macOS universal and Android ARM64 binaries, generated narrowed
Unity import metadata, all three SQLite notices, the exact 38-entry P/Invoke inventory
and targeted linker metadata. Android ARM64 import, IL2CPP/high stripping, APK
inspection and physical-device database/codec execution subsequently passed under
INT-009. macOS native execution remains unrun. Do not install a duplicate UPM copy.
