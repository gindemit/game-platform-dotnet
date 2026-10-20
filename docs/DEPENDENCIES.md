# Dependencies

## October 17 local audit-repair candidate

The [reviewed preparation order](https://github.com/gindemit/game-platform-workspace/blob/main/plans/UI-FRAMEWORK-SDK-AUDIT-2026-10-17.md)
selects 3.1.9 for the existing MessagePack NU1902 failure. The
[maintainer advisory](https://github.com/MessagePack-CSharp/MessagePack-CSharp/security/advisories/GHSA-qhrr-8q5h-9q3h)
lists this patched version; no application exploit is claimed. The central
version and MessagePack-family entries in six locks change. The packaging
allowlist matches 3.1.9; one negative test uses synthetic unreviewed 3.1.10 and
four prior flow-test formatting diagnostics are repaired without token changes.
Audit/warning policy, wire schema/resolvers, optional navigation source payload
and Unity's full mobile pin remain unchanged. Historical 3.1.8 qualification
remains dated evidence.

Canonical candidate `74dca6773718c6a201e03a8df5c9b1468187b99b` passes normal
locked restore, full solution formatting and Release build (zero warnings/errors),
1,118 solution tests with zero failures and nine explicit recovery skips,
18-project validation, schema regeneration check and all 76 tooling tests.
Local codec self-test passes 73 schema, 32 fingerprint and five hostile JSON cases;
six codec CLI rejection tests pass. This is local codec evidence, not peer or
live-service acceptance. Earlier formatter/build and stale-fixture failures are
retained outside the repository.

Two full bundles built from a clean exact-source copy are byte-identical across
all 103 files plus manifest (24 managed assemblies, three native targets).
Bundle manifest SHA-256 is
`b8b016faca4001a461b79e25b7796b6d6ba4e32114ba4aeb235d7e8936608fbe`.
Two default local-draft UPM exports are likewise identical (145 inventoried files);
manifest SHA-256 is
`0f8195e4e88c0a102875f72c1cfa9b9624f6f34431d98888cf5e992a912af96e`.
Strict bundle/UPM verification passes after each generation; sourceDirty=false,
buildSkipped=false. These artifacts are not published or imported into Unity.

A successful main push would enable the existing automatic full SDK dev-tag
release, requiring separately named owner publication authority after independent
review. No workflow bypass or live harness execution is selected. Remote CI and
Unity/IL2CPP/device qualification of this patched full mobile package remain
unrun; the existing optional navigation source package remains unchanged.

The current MessagePack adapter pins MessagePack-CSharp 3.1.9 and its
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

The bounded production HTTP composition references
`GamePlatform.Serialization.MessagePack`, so its lockfile records the same exact
nine-package MessagePack closure. It adds no new package or version and does not
enable JSON, reflection, runtime code generation, Unity networking or a second
acquisition path.

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
