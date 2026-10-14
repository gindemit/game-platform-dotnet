# CL008 Windows crash-reopen A/B plan

Predeclared, bounded check for the Windows `disk I/O error` seen once on the postcommit reopen of
`Cl008AtomicOutboxTests.AbruptProcessTerminationRollsBackPreCommitAndPreservesPostCommit`
(run 36825045338). Prepared only; the coordinator executes the dispatches.

## Refs

| Arm | Ref | What it records |
| --- | --- | --- |
| A | `1860b88` | Original harness; failure message only. |
| B | `3105a40` (main at branch point) | Lock, process, attribute, copy-open and raw-probe diagnostics on failure. |
| C | `fu-durability` at the reviewed commit (record the sha) | B plus first-failure file copies, `-shm` truncation probe, `shmReleaseWaitMs` handshake, immediate raw/`OpenAsync` retries, SQLite error log (Windows x64), TRX and diagnostics upload. |

## Fixed conditions

- `windows-latest`, `.github/workflows/ci.yml` of each ref, `global.json` of each ref, the checked-in
  `tests/GamePlatform.Tests/Sqlite/Native/win-x64/gilzoide-sqlite-net.dll` (SQLite 3.50.1); record the
  runner image version from each job log.
- N = 5 `workflow_dispatch` runs per arm, dispatched back to back in the order A, B, C, A, B, C, ...
- `workflow_dispatch` runs a branch or tag, so arms A and C need coordinator-authorized ref pushes
  (for example `ab/cl008-a` at `1860b88` and the reviewed `fu-durability`). `ci.yml` at `1860b88`
  already declares `workflow_dispatch`.
- The 15 runs use the repository's existing Actions minutes; no paid or extra infrastructure.
- No reruns replace a result. Every attempt, including cancelled or infrastructure-failed jobs, is
  listed with run id, attempt, sha and outcome. No selected greens.

## Recorded per run

- Outcome of the CL008 test and of the whole job; the failing message verbatim if any.
- Arm C, for each phase, from the TRX standard output (passes) or `diagnostics.txt` (failures):
  `childExitWaitMs`, `childPidReused`, `shmFirstProbe`, `shmReleaseWaitMs`, `shmReleased`.
- Arm C failures: the `test-diagnostics-attempt-*` artifact (copied `platform.sqlite3`, `-wal`, `-shm`,
  `crash-ready`, `diagnostics.txt`), `rawImmediate` extended code, `retryOpen` result and counts,
  `sqliteLines` entries.
- Arm C, every run: `sqliteLog` (`installed`, `unsupported` or `configResult=<n>`) from the per-phase line.

## Success criteria

The A/B succeeds as an investigation when either holds:

1. A failure in arm C carries an extended SQLite code and a Win32/HResult value from the same failure.
   For the hypothesized path (`winShmMap` first-connection `-shm` truncate) SQLite returns `0x60A`
   IOERR_TRUNCATE without calling `winLogError`, so no `os_win.c` line appears; the decisive signal is
   extended `0x60A` in `rawImmediate` plus `shmAtFailure=held win32=0x800704C8` taken right after.
   `sqliteLines` keeps only lines naming the test directory, so it can show other `os_win.c` failures
   (with their Win32 code) but not VDBE abort lines; or
2. Arm C shows, across its 10 phase observations, whether the first `-shm` probe after the child PID
   is gone reports the file as held (`shmFirstProbe != released`), and with which HResult.

The mapping-lag hypothesis is supported only if `shmFirstProbe` reports `0x800704C8` on Windows and
no arm C run fails after `shmReleased=True`. It is contradicted if arm C fails after `shmReleased=True`,
or if a failure's extended code is not a `-shm` truncate/map code.

## Evidence caveats

- Evidence copies are the state after the handshake and the failed open, not the child's exact leftovers.
- A successful probe writes the last `-shm` byte just before the reopen; a file scanner reacting to that
  write is a possible confound.
- The `retryOpen` and `rawImmediate` opens can recover, checkpoint and remove `-wal`/`-shm`, so the later
  lock, copy-open and raw results in arm C are not comparable with arm B.

## Not concluded by this plan

- A failure rate. One failure has been observed; five green runs per arm cannot show it is fixed or
  that any arm is better.
- Which process holds a lingering mapping or handle. The probe cannot tell the killed testhost from
  antivirus or indexer activity on the runner.
- Product behaviour on player devices. The handshake is a harness precondition for a finished crash,
  not a production retry, and `src/` is unchanged. A green arm C does not close whether an immediate
  relaunch after a crash can hit this IOERR in the product; that needs its own ticket.
- Anything about macOS or other platforms; local macOS runs are not Windows evidence.
- Acceptance of CL008, durability gates or any release scope.
