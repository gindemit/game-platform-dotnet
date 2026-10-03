# SDK formatting coverage (2026-10-03)

The repository pins .NET SDK `9.0.203` in `global.json`. CI on push to `main`, pull requests and manual dispatch restores the locked solution, then runs:

```sh
dotnet format GamePlatform.sln whitespace --verify-no-changes --no-restore --exclude src/GamePlatform.Storage.Sqlite/Vendor/SQLite.cs
```

This checks first-party C# in all solution projects, including runtime projects, test projects and acceptance harnesses. The vendored `SQLite.cs` is excluded. Package output under `upm/`, generated DLLs, acquisition inputs, build output and Unity imports are outside the solution's editable C# source. `.editorconfig` sets UTF-8/LF/final newline and four-space indentation. Its explicit Allman-brace, expanded single-line layout and `IDE0055` build diagnostic currently apply to `PrivateSyncCoordinator.cs` and `ProgressionService.cs` only. The rest of the repository retains the existing whitespace policy until a reviewed baseline is selected; enabling the same options globally produced 7,365 formatter diagnostics in an isolated checkout.

The pinned `dotnet format` whitespace mode is the whole-solution CI check. `EnforceCodeStyleInBuild` and warnings-as-errors now enforce selected `IDE0055` in the two baseline files: an indentation canary made the netstandard `GamePlatform.Sync` build fail with `IDE0055`, while the clean Release solution build passed with zero warnings or errors. The former global Style-category warning setting was removed because enabling build enforcement with it yielded 169 unrelated diagnostic errors. No new analyzer package was added. This scoped baseline leaves no known whitespace failure in the normal CI command.

In an isolated checkout, an indentation canary in `PrivateSyncCoordinator.cs` made the normal check exit 2. The fixer restored a passing check; a second fix pass made no changes. The excluded SQLite vendor file remained byte-identical. The canary was removed before source integration. This proves the covered C# check detects and repairs a representative violation; it does not prove Unity Editor or native platform behavior.
