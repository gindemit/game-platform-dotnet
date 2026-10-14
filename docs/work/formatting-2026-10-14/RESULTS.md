# SDK formatting results — 2026-10-14

Base SDK source: `e64ef17dd1d9305aae41000c731a0694dffaf508`.
Work followed [WORK-ORDER.md](WORK-ORDER.md) in an isolated `git archive` copy.

## Result

The root EditorConfig now expands dense first-party C# throughout the repository
and enables only IDE0055 for formatting enforcement. The earlier two-file limit
is removed. The baseline reformats 202 existing tracked C# files, including
configuration activation and UUID identity code. A one-time explicit folder
selection also baselines the tracked standalone integration/harness/tool sources;
the normal CI formatter still checks the solution projects.

The screenshot file also has wrapped signatures and guards and an expanded catch
block. Adjacent configuration catch blocks were expanded. These edits change
whitespace only. The validator test fixture no longer removes a stub by matching
its old single-line spelling: it accepts whitespace between tokens and asserts
that exactly one stub was removed. This repairs the existing evidence test after
formatting without weakening production validation.

The existing modified solution, architecture, package/validator implementation,
optional Configuration Client implementation and the other session's changes in
the validator test file are outside this commit. New client files receive the
global rules but their in-progress implementation is not baselined or qualified
by this task. No generated package or Unity consumer pin is changed here.

## Validation

Tooling: .NET SDK `9.0.203`; formatter
`9.0.203-servicing.25165.9+dc7acfa19473e10d9132cd16ffb2f2d66764baf5`.

| Check | Result |
| --- | --- |
| `dotnet restore GamePlatform.sln --locked-mode` | Passed |
| Normal CI whitespace verify, excluding vendored SQLite | Passed |
| Second solution formatter pass | Zero changed files |
| Deliberate two-assignment canary in `UuidIdentity.cs` | Normal verify failed with WHITESPACE; Core Release build failed with IDE0055 |
| Canary repair by normal formatter | Exact original clean file bytes restored |
| Roslyn C# 9 token/directive/ordinary-comment comparison | All 202 changed files preserved; zero changes to these sequences |
| Vendor, generated contracts and UPM byte comparison | All 152 selected protected files unchanged |
| Release solution build | Passed, zero warnings/errors |
| Existing .NET tests | 931 passed, 9 skipped, zero failed (883 main SDK + 48 configuration) |
| `python3 scripts/validate.py` | Passed |
| MessagePack `generate-schema.py --check` | Passed |
| `python3 -m unittest discover -s scripts -p 'test_*.py'` | Initial run exposed the old one-line fixture assumption; final run passed all 71 tests after the scoped fix |

The formatter still preserves some compact catch blocks, adjacent declarations
and long signatures. These are reviewed readability limits, not claims of complete
line-length or structural-style enforcement.

The first formatter passes emitted a workspace-loading warning. The final
diagnostic verification completed with exit 0, no loading warning, and zero of
280 documents formatted. Generated/build documents are included in that document
count; it is not a count of edited first-party files.

The nine .NET skips are the existing joined real-peer recovery tests; no peer
environment was selected. Package regeneration/determinism, Unity imports,
IL2CPP/native/device/browser runs, retained databases and deployments were not
run for this whitespace-only source maintenance. Existing platform acceptance
and publication gates retain their original scope. The existing main-push CI
workflow may generate and tag a Git UPM delivery after this authorized source
push. That later artifact is separate from these local source checks; this task
does not adopt it into a consumer or run the release workflow manually.

## Review and delivery

A fresh nonauthor read-only review approved integration with no blocking
findings. The reviewer independently compared all 202 changed C# lexical token,
literal and comment sequences, confirmed all 152 protected files were identical
and all 34 recorded pre-existing work-in-progress hashes were still preserved,
and reviewed representative activation/identity/transaction/sync/tests, the
fixture fix and the coverage/enforcement documentation. The reviewer did not
rerun builds/tests under the read-only lease. The formatter's remaining adjacent
declaration limitation is documented above. The final Python run passed 71/71.

The ordinary requested source push uses the gindemit GitHub identity and targets
the repository's verified default branch, `main`. Existing source ancestry
includes the pre-existing docs-only remote-config service mirror; its four
documents add no runtime activation. Later CI package output is separate evidence.
