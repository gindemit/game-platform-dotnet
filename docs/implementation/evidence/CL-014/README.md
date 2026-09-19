# CL-014 diagnostics evidence

P1 / epoch 0, M1/B; A12 and DI01 scoped SDK evidence. Root coordinator implementation; independent code review recorded in the P1 review report. No external provider, database or Unity output is exercised by these unit tests.

Baseline command: `rtk dotnet test GamePlatform.sln -c Release --logger 'trx;LogFileName=baseline.trx' --results-directory artifacts/p1-baseline` — exit 0, 19 tests passed, 0 warnings.

Implementation command: `rtk dotnet test GamePlatform.sln -c Release --no-restore --logger 'trx;LogFileName=diagnostics.trx' --results-directory artifacts/p1-diagnostics` — exit 0, 29 tests passed, 0 warnings (10 new diagnostics cases). The dedicated filtered verification and raw report are recorded by the final P1 verification run.

Coverage: disabled null sink does not invoke factory; enabled/write exceptions remain transaction-neutral; factory/enumerator failure is contained; strings, sensitive scalars, nested/arbitrary objects, nonfinite values and exception content cannot cross the safe boundary; copied read-only fields and captured context retain owner A after owner B is constructed; invalid names and unlimited input enumeration are bounded; legacy interface callers still compile; invalid constructor arguments fail before business work.

No framework, package, new project, queue or runtime account state was added. Existing IAppLog implementations remain compatible, but arbitrary implementations do not gain redaction automatically: callers must inject SafeAppLog or null. Metadata must be code-defined symbols; numeric fields must be non-personal measurements. Blocking sinks and malicious blocking factories are outside this synchronous port's guarantees. Unity adapter, device logging, allocation benchmarks and global A12 acceptance remain unrun. Rollback replaces the injected sink with null and never changes persisted state.
