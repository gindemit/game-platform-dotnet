# Compact auto-property results — 2026-10-14

Base: `f913f38179ee76e5da7989f73f1dcbb2456ef4f9` in SDK `main`.
Followed [WORK-ORDER.md](WORK-ORDER.md) in a disposable archive copy.

The cleanup puts 1,251 simple auto-property accessor lists on one line in 131
tracked first-party C# files, including `ActivationResult.Reason`. It preserves
`get;`, `get; set;`, access modifiers and initializers. Accessors with executable
bodies, comments or accessor attributes are left intact. A one-time Roslyn C# 9
transformation selected only these accessor spans; no new repository formatter,
runtime dependency or analyzer was introduced.

EditorConfig now preserves single-line blocks, which allows these compact
auto-properties. Allman and expanded-statement preferences remain selected. The
switch also preserves other blocks already on one line; it neither collapses
existing expanded code nor automatically compacts existing multiline properties.
This limitation is documented in [FORMATTING.md](../../FORMATTING.md).

Validation used the pinned .NET SDK `9.0.203`:

| Check | Result |
| --- | --- |
| Before/after Roslyn tokens, directives and ordinary comments | Identical in all 131 changed C# files |
| Executable statement and executable accessor full text | Identical; zero layout or behavior changes |
| Vendor/generated-contract/UPM bytes | All 152 protected files identical |
| Locked solution restore | Passed |
| Normal CI whitespace verify, excluding vendor SQLite | Passed |
| Second solution formatter pass | Zero changed files |
| Compact read-only/private-set properties and multiline executable accessor fixture | One-line properties preserved; executable accessor remained expanded |
| Two statements on one line within a multiline UUID method | Normal verify rejected the canary; formatter repaired the exact original bytes |
| Release solution build | Passed; zero warnings/errors |
| Existing .NET solution tests | 995 passed, 9 existing real-peer recovery skips, zero failures (883 main SDK + 48 configuration + 64 configuration client) |
| SDK `scripts/validate.py` | Passed; 17-project inventory |

The formatter emitted its workspace-loading warning on the successful passes;
the nonempty change selection, clean build and explicit syntax checks establish
the source scope. The fixture is disposable and not part of the repository.
Python test scripts were unchanged; their full suite was not rerun for this
auto-property-only follow-up. No packages were regenerated locally, and no Unity
imports, consumer pins, IL2CPP/device/browser runs, retained databases or
deployments were performed. An ordinary authorized main push retains the existing
CI package-generation behavior; later artifacts need their own qualification.

Fresh nonauthor read-only review approved the change with no findings. The
reviewer independently reconstructed all 1,251 property-only whitespace edits
across 131 C# files from the base archive, confirmed the 152 protected files were
unchanged and reviewed the settings, syntax checks and reported build/test
evidence. Builds/tests were not rerun under the read-only reviewer lease.

Integration applies and stages only the reviewed property patch. The concurrent
field-declaration layout edit in `AccountsLifecycle.cs` is preserved and excluded
from this task's commit. The gindemit identity is used for the requested push to
the verified default branch, `main`.
