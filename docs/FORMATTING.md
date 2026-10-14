# SDK C# formatting coverage

The repository pins .NET SDK `9.0.203` in `global.json`. The root `.editorconfig`
applies four-space indentation, Allman braces and expanded single-line statements
to first-party C# throughout the repository, including the identity
types and optional configuration libraries. Visual Studio reads these project
settings; use **Edit > Advanced > Format Document** (`Ctrl+K`, `Ctrl+D`) to apply
them to the current document.

Simple auto-properties use one line, for example `public string Reason { get; }`
or `public string Name { get; private set; }`. The owner's October 14 follow-up
selected `csharp_preserve_single_line_blocks = true` so the formatter retains
this layout. The switch also preserves other blocks that are already compact;
it does not collapse expanded executable code or require existing multiline
properties to become compact. New auto-properties should use the one-line form.
Accessors with executable bodies, comments or attributes retain their existing
layout. The one-time cleanup changed only simple auto-property accessor lists;
see [the auto-property results](work/autoproperties-2026-10-14/RESULTS.md).

CI on push to `main`, pull requests and manual dispatch restores the locked
solution, then checks its first-party runtime, test and acceptance projects:

```sh
dotnet restore GamePlatform.sln --locked-mode
dotnet format GamePlatform.sln whitespace --verify-no-changes --no-restore --exclude src/GamePlatform.Storage.Sqlite/Vendor/SQLite.cs
```

To repair the solution layout, run the second command without
`--verify-no-changes`. New solution projects receive the same rules. Standalone
integration probes and tools also receive the EditorConfig rules, but are checked
by their own builds or an explicitly selected formatter invocation; the solution
command does not discover projects outside the solution.

`EnforceCodeStyleInBuild`, `IDE0055` warning severity and warnings-as-errors enforce
formatting in first-party builds. Other analyzer severities are unchanged. The
vendored `SQLite.cs` remains excluded from the formatter; its IDE0055 diagnostic
and those of generated `*.g.cs` contracts are disabled. Generated contracts retain
their generator-owned bytes. Package output under `upm/`, generated DLLs, build
output and Unity imports are outside the editable source baseline.

The October 3 baseline covered only `PrivateSyncCoordinator.cs` and
`ProgressionService.cs` with these explicit expanded-layout settings. The owner
selected the broader baseline on October 14 after dense configuration and
identity code was observed in Visual Studio. The cleanup changed whitespace in
202 tracked first-party C# files; Roslyn confirmed unchanged token streams,
directives and ordinary comments. The test validator stub-removal fixture now
accepts both the compact and expanded class layout and asserts that it removes
exactly one stub.

In a disposable source copy, putting two UUID assignments on one line failed the
normal CI formatting check and the Core build (`IDE0055`). The formatter repaired
the canary to the exact original clean bytes. A subsequent pass changed zero
files; vendor, generated-contract and UPM files remained byte-identical.

The formatter does not automatically wrap every long signature or expression,
and this SDK version preserves some compact `catch` blocks and adjacent member
declarations. Wrap those for
readability during review; the screenshot configuration activation file and
adjacent configuration catch blocks were expanded with whitespace-only edits.
No additional style analyzers, API rewrites or game integrations were introduced.
Exact checks, review and limits are recorded in
[the October 14 evidence](work/formatting-2026-10-14/RESULTS.md).
