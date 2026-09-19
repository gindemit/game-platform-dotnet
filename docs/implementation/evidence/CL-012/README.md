# CL-012 minimal navigation and presenter evidence

Date: 2026-09-19

Source commit: `010d47c07907baf86d8a1c9396f81cd5628d03d2`

CL-012 is complete for its pure SDK scope. The implementation provides only the
accepted minimal Home/play/result/Next/Profile flow: typed immutable routes,
caller return context, bounded history, Next replace semantics, single-owner
modals, bounded deferred popups, unavailable-route outcomes and cancellation of
stale loads. It does not create a tab system or universal window manager.

Presenter lifetime primitives capture owner generation, own and dispose view
leases/subscriptions exactly once across repeated bind/unbind, reject stale
callbacks, and do not dispose borrowed services. Result presentation explicitly
distinguishes pending, confirmed and save-failed state.

## Commands and raw results

```text
rtk dotnet restore GamePlatform.sln --locked-mode
exit 0
ok dotnet restore: 18 projects, 0 errors, 0 warnings (unknown)

rtk dotnet build GamePlatform.sln -c Release --no-restore
exit 0
ok dotnet build: 18 projects, 0 errors, 0 warnings (00:00:01.67)

rtk dotnet test tests/GamePlatform.Tests/GamePlatform.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~Cl012NavigationAndPresenterTests
exit 0
ok dotnet test: 7 tests passed, 0 warnings in 1 projects (568 ms)

rtk dotnet test GamePlatform.sln -c Release --no-build --no-restore
exit 0
ok dotnet test: 271 tests passed, 0 warnings in 1 projects (3.4 s)

rtk python scripts/validate.py
exit 0
Validation passed: 15 projects, 16 evidence-aware feature states, direct/transitive references, runtime packages, module READMEs, and reviewed contract pins.

rtk python -m unittest discover -s scripts -p "test_*.py"
exit 0
Ran 33 tests in 6.913s
OK

rtk python docs/implementation/validate_manifest.py
exit 0
VALID: 52 tasks; 16 feature ledgers; 21 conservative waves

(from docs/implementation) rtk python -m unittest test_manifest
exit 0
Ran 14 tests in 0.038s
OK

rtk git diff --check
exit 0
```

No Unity scene, visual/control PlayMode, game binding or device test was run.
Those are INT-008/consumer acceptance, not part of the pure CL-012 completion.

