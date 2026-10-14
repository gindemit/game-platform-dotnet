# R09 local bootstrap recovery probe

This test-only console runs the production HTTP provider, sync coordinator,
MessagePack codec and SQLite store against BE-025's disposable PostgreSQL-backed
HTTPS fixture. The backend injects one 503 on page two. The probe verifies that
page one is staged without a visible projection or Ready, closes SQLite, reopens
the same file, and installs all 1,025 unique inventory entities with a cursor.

It uses synthetic test authentication and a per-run certificate pinned to the
exact local `localhost` endpoint. It does not qualify a phone or real identity.

Build with the repository's pinned .NET 9 SDK and the existing pinned macOS
`unity-sqlite-net` acquisition artifact. Then run the backend BE-025 runner with
`R09_DOTNET` set to that `dotnet` binary and `R09_SDK_CLIENT_DLL` set to the
resulting `R09BootstrapClient.dll`. Without those variables the BE-025 test still
checks backend snapshot recovery, but does not claim SDK recovery.
