# CL-007 P3 readiness verification — 2026-09-19

Source commit: `06ba050929117bd48cba5993c28156bbb2c6418c` on
`impl/platform-coordinated-2026-09-19`. This verifies predecessor/tooling
readiness only; CL-007 remains planned and its SQ executor/migration tests do not
yet exist.

| Command | Result |
| --- | --- |
| `dotnet restore GamePlatform.sln --locked-mode` | exit 0; 18 projects, zero errors/warnings |
| `dotnet build GamePlatform.sln -c Release --no-restore` | exit 0; 18 projects, zero errors/warnings |
| `dotnet test GamePlatform.sln -c Release --no-build --no-restore` | exit 0; 236 passed, zero failed |
| `powershell -NoProfile -File integration/unity/sqlite-qualification/run-desktop-probe.ps1` | exit 1 at environment guard; Windows PowerShell 5 has no `$IsWindows` |
| `pwsh -NoProfile -File integration/unity/sqlite-qualification/run-desktop-probe.ps1` | exit 0; real Windows x64 SQLite 3.50.1 rollback/commit/reopen probe passed |

The passing probe used the pinned unity-sqlite-net 1.3.2 candidate at
`08248bd5884d8eb932a837aa56d4ff456daf913f`, native library
`gilzoide-sqlite-net`, .NET 9.0.4 and a disposable temporary database. It
observed one committed row with exact sequence `9007199254740993` and no
rolled-back row.

Unrun: serialized writer/session leases, immutable migrations/checksums,
queue/active cancellation, disposal quiescence, fault matrix, Unity import,
IL2CPP and device execution. No capability is enabled by this preflight.
