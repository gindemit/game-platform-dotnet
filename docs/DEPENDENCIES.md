# Dependencies

Runtime projects currently have no third-party NuGet dependencies. Test packages are centrally pinned in `Directory.Packages.props` and locked by generated `packages.lock.json` files.

MessagePack-CSharp is selected for a future codec adapter, and gilzoide/unity-sqlite-net is the initial Unity SQLite candidate; neither is installed or validated in M0. Microsoft.Data.Sqlite is not an approved substitution. UniTask may appear only at a Unity boundary. VContainer, reactive frameworks, logging implementations and JSON libraries require explicit reviewed adoption with license, AOT/runtime and allowlist evidence.
