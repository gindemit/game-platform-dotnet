# CL-006 Windows x86-64 desktop evidence

Date: 2026-09-19

This evidence applies only to the Windows x86-64 native input and .NET host process. It does not establish Unity Editor, IL2CPP, Android or device compatibility.

Command:

```powershell
pwsh -File integration/unity/sqlite-qualification/run-desktop-probe.ps1
```

Expected checked inputs:

- candidate `1.3.2` / `08248bd5884d8eb932a837aa56d4ff456daf913f`
- sqlite-net `v1.9.172` / `5f72241035dd48f4305a7c811eba8e7c955e9840`
- Windows x86-64 native SHA-256 `e7e370938925dff66d9a4d50bfac6d18cc9505bd2f3f127a232b410491b46e3e`

Exit code: `0`.

Environment: Windows `10.0.26200`, x86-64 process, .NET SDK `9.0.203`, .NET runtime `9.0.4`, Git `2.47.0.windows.2`.

Exact probe output (the database path is a disposable path under the invoking user's temp directory):

```json
{"result":"passed","databasePath":"C:\\Users\\khind\\AppData\\Local\\Temp\\game-platform-cl006-sqlite\\a759abddbf2a4b95841d290783cbe557\\probe.sqlite3","rowCount":1,"sequence":"9007199254740993","payload":"committed-after-rollback","sqliteVersionNumber":3050001,"nativeLibrary":"gilzoide-sqlite-net","processArchitecture":"X64","framework":".NET 9.0.4","os":"Microsoft Windows 10.0.26200"}
```

The one reopened row and payload prove that the rollback did not survive, the committed row did survive close/reopen, and the native binding preserved the checked signed-64 test value. The script verified the source, license and native hashes before execution. Each invocation uses a new GUID-named run directory; the probe itself refuses to overwrite an existing database.

Observed consumer inputs, read-only from `C:\Work\git\gindemit\MrSquareUnity-platform-p1`: Unity `6000.5.3f1`, Android scripting backend `1` (IL2CPP), `AndroidTargetArchitectures: 2` (ARM64), empty serialized per-platform managed stripping and API compatibility maps, and no SQLite package in `Packages/manifest.json` or `Packages/packages-lock.json`. No Unity command or device command was run.
