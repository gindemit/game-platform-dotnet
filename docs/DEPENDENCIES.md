# Dependencies

The MessagePack adapter pins MessagePack-CSharp 3.1.8 and its
exact transitive closure in generated lockfiles. Other runtime projects remain
package-free. See [the dependency review](implementation/dependencies/CL-004-messagepack.md).
Test packages are centrally pinned in `Directory.Packages.props`.

Neither dependency was installed or validated in historical M0. The initial
Unity SQLite candidate remains gilzoide/unity-sqlite-net. Microsoft.Data.Sqlite is
not an approved substitution. UniTask may appear only at a Unity boundary.
VContainer, reactive frameworks, logging implementations and JSON libraries
require explicit reviewed adoption with license, AOT/runtime and allowlist evidence.

CL-013's early managed bundle deliberately excludes the qualification MessagePack
assembly and its runtime dependencies pending CL-015. Its 14 included assemblies
have verified internal dependency closure, symbols, notices and exact hashes;
only Core and Features.Contracts are selected for the early Unity import.

P1 qualifies `gilzoide/unity-sqlite-net` 1.3.2 at `08248bd5884d8eb932a837aa56d4ff456daf913f` using a real Windows x64 desktop transaction/reopen probe. See the [qualification decision](implementation/dependencies/CL-006-sqlite-candidate.md) and [exact input manifest](../integration/unity/sqlite-qualification/qualification.json).

CL-007 compiles the pinned managed source into `GamePlatform.Storage.Sqlite`.
The portable copy replaces the package's eight UnityEngine preserve bases with
sqlite-net's local preserve marker and unseals that local marker; the reviewed
patch and original/compiled hashes are recorded beside the source. Real tests
use the exact pinned Windows x64 native DLL
`e7e370938925dff66d9a4d50bfac6d18cc9505bd2f3f127a232b410491b46e3e`.
All three MIT notices are retained. This does not install a Unity package or
complete the production native bundle. CL-015 still owns linker preservation,
multi-platform packaging and Unity/IL2CPP/device evidence. Do not install a
duplicate UPM copy.
