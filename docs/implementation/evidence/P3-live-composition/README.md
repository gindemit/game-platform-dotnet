# P3 bounded SDK live-composition evidence

Date: 2026-09-20

Source base: coordinated branch `28685a1642ec687c9f6da61c580d932b923e29c7`.
The final source commit is the commit containing this evidence.

Implemented bounded scope:

- production canonical `gsc1` fingerprint adapter over the complete durable
  envelope, restricted to the two frozen G3 command kinds;
- neutral `IHttpExecutor` production composition and composed command push;
- fixed-H/server-time/revision-zero propagation to an observed typed private
  projector;
- typed G3 profile/progression/wallet replacement/group decoding with explicit
  removal behavior and fail-closed non-empty inventory/entitlement collections;
- borrowed in-transaction durable feature reads/deletes;
- consumer-owned synchronous CL-108 transaction callback for game checkpoint,
  progress and client-side presentation writes.

Exact verification:

```text
rtk dotnet test tests/GamePlatform.Tests/GamePlatform.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~Cl010PrivateSyncTests
exit 0; 26 passed

rtk dotnet test tests/GamePlatform.Tests/GamePlatform.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~Cl011SqliteFeatureStateTests
exit 0; 5 passed

rtk dotnet test tests/GamePlatform.Tests/GamePlatform.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~Cl108ProgressionServiceTests
exit 0; 12 passed

rtk dotnet test tests/GamePlatform.Tests/GamePlatform.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~CanonicalCommandFingerprintTests|FullyQualifiedName~BoundedHttpClientExecutorTests"
exit 0; 8 passed

rtk dotnet restore GamePlatform.sln --locked-mode
exit 0; 18 projects, 0 errors/warnings

rtk dotnet build GamePlatform.sln -c Release --no-restore
exit 0; 18 projects, 0 errors/warnings

rtk dotnet test GamePlatform.sln -c Release --no-build --no-restore
exit 0; 473 passed, 0 failed/skipped/warnings

rtk python scripts/validate.py
exit 0

rtk python -m unittest discover -s scripts -p 'test_*.py'
exit 0; 51 passed

rtk python docs/implementation/validate_manifest.py
exit 0; 52 tasks, 16 feature ledgers, 21 waves

rtk python -m unittest discover -s docs/implementation -p 'test_*.py'
exit 0; 14 passed
```

Truthful remaining boundaries:

- `invalidation` cannot be stored distinctly by migration v6's raw-state CHECK
  constraint. It remains fail-closed pending the migration-number/registry owner;
- approved G3 receipt DTOs remain outside the production typed mapper and no
  reward-receipt HTTP provider/durable receipt-feed-operation correlator is
  composed yet;
- no bundle was built/imported and no Unity/editor/IL2CPP/device/live backend
  test ran. INT-007, BE-025/INT-010 and G3 remain unpassed.
