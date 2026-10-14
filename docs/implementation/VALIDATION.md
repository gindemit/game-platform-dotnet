# Planning delivery validation — 2026-09-18

Historical record: commands, paths and hashes below describe the original delivery. Current SDK checks are in [TESTING.md](../TESTING.md); shared planning validation now lives in the [workspace](https://github.com/gindemit/game-platform-workspace/blob/main/README.md).

## Scope actually checked

The connected GitHub tools were used to inspect the three private repositories, their baseline refs/branches/PRs, required architecture/contract documents and relevant source/tests/build/packaging/CI. The audit records exact commits and distinguishes full small-file reads from relevant sections/searches of larger documents. No user local worktree was available. A container clone failed with `Could not resolve host: github.com`; there was no local repository checkout or runtime build.

The newly authored execution manifest and its standard-library Python checker/tests were placed in a local planning directory and executed. The commands, from that local `game-platform-dotnet` artifact root, were:

```sh
python docs/implementation/validate_manifest.py --ready
python -m unittest discover -s docs/implementation -p 'test_*.py' -v
```

Both exited 0. The checker reported **52 tasks, 16 feature ledgers and 21 conservative waves**. Initial ready-to-start tasks were **CL-001, CL-006, CL-014, INT-001**, with environment availability still requiring verification. All **14 unit tests passed**: valid plan, duplicate ID, missing dependency, cycle, same-wave shared lock, same-wave overlapping path, dependency wave, missing required field, unsafe path, unknown peer, unsupported task completion, unsupported integration completion, missing feature and mismatched baseline.

The checker validates planning structure, not runtime truth or whether a reported artifact really proves acceptance. Cross-repository task cards, source authority, newly proposed harness names, real versus fake evidence and semantic architecture decisions still require review. No task was marked implemented by adding these files.

Locally tested byte hashes (SHA256):

| File | SHA256 |
| --- | --- |
| execution-manifest.json | `0dc8ab8c5ecf0cc0fac163488da4a11d31d5b9e8e8540846efce7c8e3dca4dc1` |
| validate_manifest.py | `720d7aa6f0a4df9ae0c60cd973d7c9a0e34c7e285120675175bc64251727ddd3` |
| test_manifest.py | `cdf0e77139a60ce7b462cd1bcda9cf5f64b93eef0d7cb25e737c77625a2ee25b` |

## Not run / not established

The original SDK `scripts/validate.py` and its tests, locked dotnet restore/build/test, package-sdk.py, original Unity documentation validators, Unity import/compilation/EditMode/PlayMode, SQLite native/crash runtime tests, peer C#/TypeScript codec execution, PostgreSQL transactions, Supabase/Workers/Node host acceptance, IL2CPP/stripping builds, Android device testing, store verification and real-backend game acceptance were **not run in this planning session**. Repository commands were source-inspected and copied into task runbooks, not reported as successful execution.

Contract SHA256 values in the handoff were inspected from existing snapshot records; the canonical file byte hashes must be recomputed before CL-003 repinning. No compatible production SDK/backend/Unity implementation revision has been established; those fields remain null/blocked. Existing scaffold tests and documentation assertions do not prove runtime features.

## Write boundary

This delivery creates documentation/planning changes on paired review branches, plus the narrowly scoped planning checker and its tests. It changes no production runtime source, packages/locks, canonical backend protocol, secrets, deployment or repository visibility. Main is not intentionally modified or merged. The PR descriptions and final operator summary identify the actual final commits and checks. Unity main pushes trigger distribution, so review/implementation should use the branch without automatically merging or dispatching that workflow.
