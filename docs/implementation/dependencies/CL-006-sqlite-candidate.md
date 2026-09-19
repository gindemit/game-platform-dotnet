# CL-006 SQLite candidate qualification

Status: qualified for dependency-ready adapter work on Windows x86-64 desktop; Unity Editor, IL2CPP and device execution remain unverified.

## Decision

Retain `gilzoide/unity-sqlite-net` and pin release `1.3.2` at commit `08248bd5884d8eb932a837aa56d4ff456daf913f`. Its embedded `praeclarum/sqlite-net` source is submodule commit `5f72241035dd48f4305a7c811eba8e7c955e9840` (`v1.9.172`). The package declares Unity 2021.2 or newer. The inspected MrSquare consumer uses Unity 6000.5.3f1, Android ARM64 and IL2CPP, so no known version/architecture mismatch was found; this is input qualification, not runtime acceptance.

Use one SDK-assembled, versioned managed/native bundle. Compile the pinned `Runtime/sqlite-net/SQLite.cs` into the storage adapter assembly and bundle only the native libraries selected for supported targets, with their original Unity `.meta` importer settings and required notices. Do not also install `com.gilzoide.sqlite-net` through UPM. CL-015 owns the final transitive bundle and Unity import; CL-007 owns the adapter and connection/thread policy. No dependency is added to portable Core and `Microsoft.Data.Sqlite` is not substituted.

The machine-readable pin, acquisition route, input hashes and execution states are in `integration/unity/sqlite-qualification/qualification.json`.

The usable adapter boundary is the synchronous `SQLiteConnection` API: explicit `BeginTransaction`, `Commit`, `Rollback`, parameterized `Execute`/`ExecuteScalar`, `BusyTimeout`, and deterministic `Dispose`. The candidate also exposes an async wrapper based on a task scheduler and pooled connections, but CL-007 should own serialization and expose `Task` at the consumer boundary rather than allow concurrent callers to own connections. SQL row types remain internal to storage.

The modified source selects P/Invoke library name `gilzoide-sqlite-net` on Windows, Linux, macOS and Android, and `__Internal` on iOS/tvOS/visionOS/WebGL. The inspected native build is SQLite3 Multiple Ciphers 2.1.3, based on SQLite 3.50.1. The Windows probe reports `sqlite3_libversion_number` 3050001.

## Integrity and notices

All hashes are SHA-256 from the pinned Git tree:

| Input | SHA-256 |
| --- | --- |
| `LICENSE.txt` | `f029c8aaafe1ceb2dc57fae54ee065f150132530209003da4d6444318d335725` |
| `Runtime/sqlite-net/LICENSE.txt` | `f4f848e81a747b85a1f5c7b761bf12b85098dd6a6a46acebd54958e2fe2dd62d` |
| `Runtime/sqlite-net/SQLite.cs` | `73d35895222b80c54cd5e5531db5db1c0213e35db09e18937054e0e11ce3a30f` |
| Windows x86-64 DLL | `e7e370938925dff66d9a4d50bfac6d18cc9505bd2f3f127a232b410491b46e3e` |
| Windows ARM64 DLL | `97d34848e9504fbb62ca4fda75354e4fc08eadf6c323cea955ce265f08169e08` |
| macOS universal dylib | `39489aa49e2ba5ae0619b0f9f9a1821da058fab95f173bf15399ee2210cfb3d4` |
| Android ARM64 SO | `6d7f5c14cc5eda83c919f13ac75cc342a619090c9d6aee7be039f052fa86621d` |

The package and sqlite-net managed source are MIT licensed. The package README identifies SQLite3 Multiple Ciphers as MIT licensed; its annotated `v2.1.3` tag resolves to commit `632ca1ef4a3457991ec638d5180cc3358620ea93`, whose `LICENSE` is the MIT notice copyright Ulrich Telle, 2019–2024. Distribution must carry the package `LICENSE.txt`, `Runtime/sqlite-net/LICENSE.txt`, and that SQLite3 Multiple Ciphers notice. CL-015 must hash the exact copied notice and every shipped binary; the current package does not contain the SQLite3 Multiple Ciphers notice as a standalone file, so omitting that additional notice is a packaging blocker.

## Conditional platform input matrix

| Target | Candidate input | Import/build condition | Qualification state |
| --- | --- | --- | --- |
| Windows Editor/standalone x86-64 | `Plugins/lib/windows/x86_64/gilzoide-sqlite-net.dll` | Editor OS Windows, CPU x86_64; Win64 standalone | Native .NET process probe passed; Unity Editor unrun |
| macOS Editor/standalone universal | `Plugins/lib/macos/libgilzoide-sqlite-net.dylib` | Editor OS OSX; OSXUniversal; upstream minimum macOS 11 | Inspected and hashed; runner unavailable/unrun |
| Android ARM64 | `Plugins/lib/android/arm64/libgilzoide-sqlite-net.so` | Android CPU ARM64, marked 16 KiB aligned; upstream build uses NDK r27c and API 21 compiler | Inspected and hashed; IL2CPP build/device execution deferred to CL-015/INT-009 |

Other packaged architectures are outside the first MrSquare slice and are not approved merely because upstream includes binaries. iOS, tvOS, visionOS, Linux and WebGL also remain conditional/unverified.

## Reproduction

From the repository root on Windows x86-64 with Git and .NET 9:

```powershell
pwsh -File integration/unity/sqlite-qualification/run-desktop-probe.ps1
```

The script checks out both exact commits, verifies the managed source, license and native DLL hashes, compiles the pinned managed source directly into a temporary .NET probe, and runs a real native file transaction. The probe rolls back one insert, commits another with a signed-64 value above JavaScript's exact integer range, closes the connection, reopens read-only, and verifies the single durable row. It does not alter or exercise the production adapter.
