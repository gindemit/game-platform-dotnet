# Dependencies

Runtime projects currently have no third-party NuGet dependencies. Test packages are centrally pinned in `Directory.Packages.props` and locked by generated `packages.lock.json` files.

MessagePack-CSharp is selected for a future codec adapter, and gilzoide/unity-sqlite-net is the initial Unity SQLite candidate; neither is installed or validated in M0. Microsoft.Data.Sqlite is not an approved substitution. UniTask may appear only at a Unity boundary. VContainer, reactive frameworks, logging implementations and JSON libraries require explicit reviewed adoption with license, AOT/runtime and allowlist evidence.

P1 qualifies `gilzoide/unity-sqlite-net` 1.3.2 at `08248bd5884d8eb932a837aa56d4ff456daf913f` using a real Windows x64 desktop transaction/reopen probe. See the [qualification decision](implementation/dependencies/CL-006-sqlite-candidate.md) and [exact input manifest](../integration/unity/sqlite-qualification/qualification.json). This is an isolated source/native probe; no runtime SDK dependency or Unity import was installed. CL-015 still must supply the complete bundle/notices and actual Unity/IL2CPP/device evidence. Acquire the managed/native library only through the coordinated SDK bundle; do not also install a duplicate UPM copy.
