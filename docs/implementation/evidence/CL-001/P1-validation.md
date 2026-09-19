# CL-001 P1 partial validation evidence

Source branch: `impl/cl-001-p1`  
Starting commit: `91929801c07c28e40610e7e4f9a25a3e5661406a`  
Environment: Python 3.10.4; .NET SDK 9.0.203; Git 2.47.0.windows.2.

All commands ran from the repository root on 2026-09-19. RTK printed its local “No hook installed” notice; that notice did not change command exit status.

| Command | Exit | Raw result |
| --- | ---: | --- |
| `rtk python scripts/validate.py` | 0 | `Validation passed: 15 projects, 16 evidence-aware feature states, direct/transitive references, runtime packages, module READMEs, and 2 pinned contract files.` |
| `rtk python -m unittest discover -s scripts -p 'test_*.py'` | 0 | `Ran 19 tests in 2.159s` / `OK`; full captured output is in `validation-output.txt`. |
| `rtk python docs/implementation/validate_manifest.py` | 0 | `VALID: 52 tasks; 16 feature ledgers; 21 conservative waves` |
| `rtk python -m unittest discover -s docs/implementation -p 'test_*.py'` | 0 | `Ran 14 tests in 0.038s` / `OK` |
| `rtk dotnet restore GamePlatform.sln --locked-mode` | 0 | 17 projects restored; 0 errors, 0 warnings. |
| `rtk dotnet build tests/GamePlatform.Tests/GamePlatform.Tests.csproj -c Release --no-restore` | 0 | 13 projects built; 0 errors, 0 warnings. |
| `rtk proxy dotnet vstest tests/GamePlatform.Tests/bin/Release/net9.0/GamePlatform.Tests.dll --Logger:"trx;LogFileName=cl001.trx" --ResultsDirectory:docs/implementation/evidence/CL-001` | 0 | TRX counters: total 19, executed 19, passed 19, failed 0, skipped 0. The earlier summary-free `dotnet test` invocation skipped because the baseline project does not declare `IsTestProject`; it is not evidence. |
| `rtk git diff --check` | 0 | No diagnostics. |

The Python validator suite includes a sandbox-only concrete feature/behavior-test transition and negative coverage for an unavailable feature relabel, evidence-free claim, zero/missing test report, path escape, unknown status, forbidden direct/transitive reference, direct/locked-transitive runtime package, missing module README and changed pinned contract bytes. These tests validate repository policy only. They do not satisfy A01–A13, Unity import, native/AOT/device, peer codec or feature runtime acceptance. CL-001 remains partial until coordinator API review; exact future signatures remain with their owning tasks and G1.
