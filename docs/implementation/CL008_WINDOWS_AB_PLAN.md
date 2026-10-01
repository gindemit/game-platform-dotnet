# CL008 Windows crash-reopen A/B plan

Predeclared, bounded check for the Windows `disk I/O error` seen once on the postcommit reopen of
`Cl008AtomicOutboxTests.AbruptProcessTerminationRollsBackPreCommitAndPreservesPostCommit`
(run 36825045338). Prepared only; the coordinator executes the dispatches.

## Refs

| Arm | Ref | What it records |
| --- | --- | --- |
| A | `1860b88` | Original harness; failure message only. |
| B | current `main` at dispatch time (record the sha) | Lock, process, attribute, copy-open and raw-probe diagnostics on failure. |
| C | `fu-durability` (record the sha) | B plus first-failure file copies, `-shm` truncation probe, `shmReleaseWaitMs` handshake, immediate raw/`OpenAsync` retries, SQLite error log (Windows x64), TRX and diagnostics upload. |

## Fixed conditions

- `windows-latest`, `.github/workflows/ci.yml` of each ref, `global.json` of each ref, the checked-in
  `tests/GamePlatform.Tests/Sqlite/Native/win-x64/gilzoide-sqlite-net.dll` (SQLite 3.50.1); record the
  runner image version from each job log.
- N = 5 `workflow_dispatch` runs per arm, dispatched back to back in the order A, B, C, A, B, C, ...
- No reruns replace a result. Every attempt, including cancelled or infrastructure-failed jobs, is
  listed with run id, attempt, sha and outcome. No selected greens.

## Recorded per run

- Outcome of the CL008 test and of the whole job; the failing message verbatim if any.
- Arm C, for each phase, from the TRX standard output (passes) or `diagnostics.txt` (failures):
  `childExitWaitMs`, `childPidReused`, `shmFirstProbe`, `shmReleaseWaitMs`, `shmReleased`.
- Arm C failures: the `test-diagnostics-attempt-*` artifact (copied `platform.sqlite3`, `-wal`, `-shm`,
  `crash-ready`, `diagnostics.txt`), `rawImmediate` extended code, `retryOpen` result and counts,
  `sqliteLog` entries.

## Success criteria

The A/B succeeds as an investigation when either holds:

1. A failure in arm C carries an extended SQLite code and a Win32/HResult value from the same failure
   (for example extended `0x60A` IOERR_TRUNCATE with `shmFirstProbe=held win32=0x800704C8`, or an
   `os_win.c` log line naming the failing call and its error); or
2. Arm C shows, across its 10 phase observations, whether the first `-shm` probe after the child PID
   is gone reports the file as held (`shmReleaseWaitMs > 0`), and with which HResult.

The mapping-lag hypothesis is supported only if `shmFirstProbe` reports `0x800704C8` on Windows and
no arm C run fails after `shmReleased=True`. It is contradicted if arm C fails after `shmReleased=True`,
or if a failure's extended code is not a `-shm` truncate/map code.

## Not concluded by this plan

- A failure rate. One failure has been observed; five green runs per arm cannot show it is fixed or
  that any arm is better.
- Which process holds a lingering mapping or handle. The probe cannot tell the killed testhost from
  antivirus or indexer activity on the runner.
- Product behaviour on player devices. The handshake is a harness precondition for a finished crash,
  not a production retry, and `src/` is unchanged.
- Anything about macOS or other platforms; local macOS runs are not Windows evidence.
- Acceptance of CL008, durability gates or any release scope.
