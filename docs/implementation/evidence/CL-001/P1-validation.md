# CL-001 P1 validation evidence

Source branch: `impl/cl-001-p1`  
Starting commit: `91929801c07c28e40610e7e4f9a25a3e5661406a`  
Environment: Python 3.10.4; .NET SDK 9.0.203; Git 2.47.0.windows.2.

All commands ran from the repository root on 2026-09-19. RTK printed its local “No hook installed” notice; that notice did not change command exit status.

| Command | Exit | Raw result |
| --- | ---: | --- |
| `rtk python scripts/validate.py` | 0 | `Validation passed: 15 projects, 16 evidence-aware feature states, direct/transitive references, runtime packages, module READMEs, and 2 pinned contract files.` |
| `rtk python -m unittest discover -s scripts -p 'test_*.py'` | 0 | `Ran 14 tests in 1.299s` / `OK` |
| `rtk python docs/implementation/validate_manifest.py` | 0 | `VALID: 52 tasks; 16 feature ledgers; 21 conservative waves` |
| `rtk python -m unittest discover -s docs/implementation -p 'test_*.py'` | 0 | `Ran 14 tests in 0.038s` / `OK` |
| `rtk dotnet test GamePlatform.sln -c Release --no-restore` | 0 | RTK emitted no test summary; the command process returned exit code 0. Existing compiled xUnit selection was not used as evidence for any feature implementation. |
| `rtk git diff --check` | 0 | No diagnostics. |

The Python validator suite includes positive correlated-evidence transition coverage and negative coverage for an evidence-free implemented claim, unknown status, forbidden direct/transitive reference, forbidden runtime package, missing module README and changed pinned contract bytes. These tests validate repository policy only. They do not satisfy A01–A13, Unity import, native/AOT/device, peer codec or feature runtime acceptance.
