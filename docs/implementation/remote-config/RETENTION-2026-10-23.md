# Retained manifest decoding — 2026-10-23

`ConfigurationClient.Protected` now decodes each unique manifest representation
once per retention pass. Every activation still validates its own closure. The
dictionary is local to the pass, so subsequent commits and CAS retries reread and
revalidate manifests. No unchanged-activation shortcut, pin deletion or cache reset
is introduced. This changes no public API or optional package boundary.

## Validation

Local .NET SDK 9.0.203 was installed at `/tmp/pb-followup/dotnet9`; repository
targets/global.json were preserved. Source baseline: `5b08ad1a46fb6e28f1752f29823c25f19b65c354`.
The inherited unfinished test needed one IDE0055 formatting correction before it
compiled. Baseline focused tests: 2 expected read-count failures and 2 integrity
passes. Expanded baseline: 2 expected failures and 5 passes. Patched focused tests:
7/7 pass, including missing/corrupt manifest detection after a simulated CAS
conflict. A fresh nonauthor child approved source and final tests.

Exact final regression command (canonical checkout, serialized):

```sh
/tmp/pb-followup/dotnet9/dotnet test tests/GamePlatform.Configuration.Client.Tests/GamePlatform.Configuration.Client.Tests.csproj --no-restore --logger 'console;verbosity=detailed' --logger 'trx;LogFileName=client-final.trx' --results-directory /tmp/pb-followup/sdk-results/final
```

Result: **74 passed, 1 failed, 0 skipped (75 total)**. Existing
`FileStoreTests.Overlapping_real_file_writers_conflict_and_cancel_before_replace_preserves_index`
expects `Conflict` but receives `Unavailable`. Reversing only the runtime patch and
running that case reproduces the same failure on baseline; no file-store changes
are included here. The runtime patch was restored before the final regression.

## Bounded allocation evidence

The new fixture creates 23 shared-manifest pins, warms three reselections and
measures seven successful durable activated reselections with
`GC.GetTotalAllocatedBytes(true)`. Its collection disables parallel execution.
Final baseline and patched comparisons, with identical success assertions:

| Source | Allocated bytes/selection |
| --- | ---: |
| Baseline | 8,572,971 |
| Patched | 6,784,038 |

This is about **20.9% fewer managed bytes** in a synthetic .NET/file-store fixture.
It is a process-wide measurement with runtime/test overhead, not an Android
budget or device profiler attribution. Reselection supplies current manifest
bytes in the pending-object map, so both versions perform zero manifest disk
reads in that measurement; the saving includes avoided repeated decoding.
Separate direct-pin tests prove one disk read per unique retained manifest.

Raw logs/TRX remain outside Git under `/tmp/pb-followup`. Unity package staging,
consumer import, IL2CPP builds and matching device minute-cycle measurements are
separate work. No generated payload or consumer pin is changed by this commit.
