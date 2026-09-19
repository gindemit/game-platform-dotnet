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

CL-013's early managed bundle deliberately excludes the qualification MessagePack
assembly and its runtime dependencies pending CL-015. Its 14 included assemblies
have verified internal dependency closure, symbols, notices and exact hashes;
only Core and Features.Contracts are selected for the early Unity import.

P1 qualifies `gilzoide/unity-sqlite-net` 1.3.2 at `08248bd5884d8eb932a837aa56d4ff456daf913f` using a real Windows x64 desktop transaction/reopen probe. See the [qualification decision](implementation/dependencies/CL-006-sqlite-candidate.md) and [exact input manifest](../integration/unity/sqlite-qualification/qualification.json). This is an isolated source/native probe; no runtime SDK dependency or Unity import was installed. CL-015 still must supply the complete bundle/notices and actual Unity/IL2CPP/device evidence. Acquire the managed/native library only through the coordinated SDK bundle; do not also install a duplicate UPM copy.
